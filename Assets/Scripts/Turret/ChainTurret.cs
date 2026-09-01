// 연쇄 레이저 포탑. 최근접 적을 쏘고, 맞은 적을 기준으로 사거리 안의 다음 적에게 계속 튄다.
// 연쇄 사거리는 포탑 사거리와 동일하며, 그 안에 남은 적이 없으면 거기서 끊긴다.

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ChainTurret : TurretBase
{
    [Header("연쇄")]
    [Tooltip("첫 대상을 포함한 최대 타격 수. 4면 처음 1명 + 연쇄 3명.")]
    [SerializeField] private int maxChainTargets = 4;

    [Tooltip("한 번 튈 때마다 곱해지는 데미지 비율. 1이면 감쇠 없음.")]
    [SerializeField] private float damageFalloff = 0.8f;

    // ---------------- 특수 강화 1 : 연쇄 추가 ----------------

    [Header("특수 강화 1 — 연쇄 추가")]
    [SerializeField] private string specialTitle = "더 많이 지지기";

    [TextArea(2, 3)] [SerializeField] private string specialDescription = "";

    [Tooltip("특수 강화 1회당 추가로 연쇄되는 적 수.")]
    [Min(1)] [SerializeField] private int chainPerSpecial = 1;

    // ---------------- 특수 강화 2 : 무한 연쇄 ----------------

    [Header("특수 강화 2 — 무한 연쇄")]
    [Tooltip("비워두면 이 포탑에는 두 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special2Title = "";

    [TextArea(2, 3)] [SerializeField] private string special2Description = "";

    [Tooltip("두 번째 특수를 먹으면 추가로 연쇄되는 적 수.")]
    [Min(0)] [SerializeField] private int chainPerSpecial2 = 27;

    [Tooltip("두 번째 특수를 먹으면 레이저 굵기에 곱해지는 배율.")]
    [Min(1f)] [SerializeField] private float special2LaserWidth = 1.4f;

    // ---------------- 특수 강화 3 : 연쇄 폭발 ----------------

    [Header("특수 강화 3 — 연쇄 폭발")]
    [Tooltip("비워두면 이 포탑에는 세 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special3Title = "";

    [TextArea(2, 3)] [SerializeField] private string special3Description = "";

    [Tooltip("몇 마리째 연쇄될 때마다 폭발이 일어날지. 10이면 10 · 20 · 30번째 적 자리에서 터진다.")]
    [Min(1)] [SerializeField] private int blastEveryChain = 10;

    [Tooltip("폭발 반경(월드 단위).")]
    [Min(0.1f)] [SerializeField] private float blastRadius = 2.5f;

    [Tooltip("폭발 피해. 체인 공격력에 곱해진다. 1이면 레이저 한 발과 같은 피해.")]
    [Min(0f)] [SerializeField] private float blastDamageMultiplier = 1f;

    [Header("연쇄 폭발 연출")]
    [Tooltip("퍼져나가는 원의 색. 알파가 시작 진하기다.")]
    [SerializeField] private Color blastColor = new Color(0.45f, 0.9f, 1f, 0.5f);

    [Min(0.05f)] [SerializeField] private float blastVisualDuration = 0.3f;

    [Tooltip("원이 시작하는 크기. 폭발 반경 대비 비율.")]
    [Range(0f, 1f)] [SerializeField] private float blastStartRatio = 0.2f;

    [Tooltip("바닥보다 살짝 위여야 파묻히지 않는다.")]
    [SerializeField] private float blastHeight = 0.06f;

    [Tooltip("한 번 쏠 때 폭발이 여러 곳에서 동시에 터질 수 있으므로 원판을 몇 장 돌려쓸지.")]
    [Min(1)] [SerializeField] private int blastDiscCount = 4;

    [Tooltip("폭발 소리. 비우면 조용히 터진다.")]
    [SerializeField] private SfxDef blastSfx;

    public override string SpecialTitle => specialTitle;
    public override string SpecialDescription => specialDescription;
    public override string Special2Title => special2Title;
    public override string Special2Description => special2Description;
    public override string Special3Title => special3Title;
    public override string Special3Description => special3Description;

    // 연쇄 수와 감쇠는 체인 포탑만 쓰는 값이라 CSV/SO에 두지 않고 여기서 관리한다.
    private int BaseChainTargets => Mathf.Max(1, maxChainTargets);
    private float Falloff => damageFalloff;
    private int ChainPerSpecial => Mathf.Max(1, chainPerSpecial);

    /// <summary>기본 연쇄 수 + 두 단계의 특수 강화로 늘어난 수.</summary>
    private int EffectiveMaxChainTargets =>
        BaseChainTargets + SpecialLevel * ChainPerSpecial + Special2Level * chainPerSpecial2;

    /// <summary>두 번째 특수를 먹으면 레이저가 굵어진다.</summary>
    private float EffectiveLaserWidth =>
        laserWidth * (Special2Level > 0 ? special2LaserWidth : 1f);

    [Header("레이저 연출")]
    [SerializeField] private Color laserColor = new Color(0.35f, 0.75f, 1f);
    [SerializeField] private float laserWidth = 0.14f;
    [Tooltip("한 발이 보이는 시간. 짧게 두어야 '찡' 하고 번쩍이는 느낌이 난다.")]
    [SerializeField] private float laserFadeDuration = 0.14f;

    private static readonly int SpriteColorId = Shader.PropertyToID("_Color");

    // 폭발 판정에 쓰는 공용 버퍼. 매번 리스트를 새로 만들지 않는다.
    private static readonly List<EnemyBase> blastBuffer = new List<EnemyBase>(64);

    // 원판을 돌려쓴다. 한 번 쏠 때 폭발이 여러 곳에서 터질 수 있다.
    private readonly List<MeshRenderer> blastDiscs = new List<MeshRenderer>(4);
    private readonly List<Tween> blastTweens = new List<Tween>(4);
    private MaterialPropertyBlock blastBlock;
    private Mesh blastMesh;
    private int nextDisc;

    private LineRenderer line;
    private readonly List<EnemyBase> chain = new List<EnemyBase>(8);
    private readonly List<Vector3> chainPoints = new List<Vector3>(8);
    private float laserTimer;

    protected override void Awake()
    {
        base.Awake();

        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.startWidth = EffectiveLaserWidth;
        line.endWidth = EffectiveLaserWidth;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        // 발사 전에는 완전히 꺼둔다. 알파만 0으로 두면 잔상이 남는 원인이 된다.
        line.enabled = false;
        laserTimer = 0f;
    }

    protected override void Update()
    {
        base.Update();
        UpdateLaser();
    }

    protected override void Fire(EnemyBase target)
    {
        if (target == null) return;

        chain.Clear();
        chainPoints.Clear();

        EnemyBase current = target;
        float currentDamage = EffectiveDamage;
        Vector3 from = muzzle.position;

        while (current != null && chain.Count < EffectiveMaxChainTargets)
        {
            // 데미지를 주기 전에 위치를 확보해둔다. 죽으면 축소 연출로 자리가 흔들린다.
            // 연쇄 탐색과 넉백은 지면 좌표를 쓰고, 눈에 보이는 선만 몸통 한가운데를 잇는다.
            // 루트를 이으면 적 발밑을 지나가 몸통에 가려진다.
            Vector3 hitPoint = current.transform.position;

            chain.Add(current);
            chainPoints.Add(current.AimPoint);

            current.TakeDamage(currentDamage, from, Def);
            currentDamage *= Falloff;

            // 세 번째 특수: 정해진 횟수째 연쇄에서 그 적 자리에 원형 폭발을 터뜨린다.
            if (Special3Level > 0 && chain.Count % Mathf.Max(1, blastEveryChain) == 0)
                ChainBlast(hitPoint);

            from = hitPoint;

            // 다음 대상은 "직전에 맞은 적" 기준으로 포탑 사거리 안에서 찾는다. 이미 맞은 적은 제외.
            current = EnemyRegistry.FindNearestExcluding(from, EffectiveRange, chain);
        }

        ShowLaser();
    }

    private void ShowLaser()
    {
        if (chainPoints.Count == 0) return;

        line.positionCount = chainPoints.Count + 1;
        line.SetPosition(0, muzzle.position);

        for (int i = 0; i < chainPoints.Count; i++)
            line.SetPosition(i + 1, chainPoints[i]);

        // 특수 강화는 판 도중에 붙으므로 쏠 때마다 굵기를 다시 맞춘다.
        line.startWidth = EffectiveLaserWidth;
        line.endWidth = EffectiveLaserWidth;

        line.enabled = true;
        laserTimer = laserFadeDuration;
        SetLaserAlpha(1f);
    }

    private void UpdateLaser()
    {
        if (!line.enabled) return;

        laserTimer -= Time.deltaTime;

        if (laserTimer <= 0f)
        {
            line.enabled = false;
            line.positionCount = 0;
            return;
        }

        // 포탑을 들고 움직이는 중에도 시작점이 총구를 따라오게 매 프레임 갱신한다.
        // 이걸 안 하면 레이저가 옛 위치에 남아 길게 늘어진 잔상으로 보인다.
        line.SetPosition(0, muzzle.position);

        SetLaserAlpha(laserTimer / laserFadeDuration);
    }

    private void SetLaserAlpha(float alpha)
    {
        Color color = laserColor;
        color.a = Mathf.Clamp01(alpha);

        line.startColor = color;
        line.endColor = color;
    }


    // ---------------------------------------------------------------- 연쇄 폭발 (3특)

    /// <summary>
    /// 연쇄가 정해진 횟수에 닿을 때마다 그 적 자리에서 원형 폭발.
    /// 이미 레이저를 맞은 적도 이 피해를 추가로 받는다. 그래서 제외 목록을 쓰지 않는다.
    /// </summary>
    private void ChainBlast(Vector3 center)
    {
        float damage = EffectiveDamage * blastDamageMultiplier;

        EnemyRegistry.FindAllInRange(center, blastRadius, blastBuffer);

        for (int i = 0; i < blastBuffer.Count; i++)
        {
            EnemyBase enemy = blastBuffer[i];
            if (enemy == null || !enemy.IsAlive) continue;

            enemy.TakeDamage(damage, center, Def);
        }

        SfxManager.Play(blastSfx, center);
        PlayBlastVisual(center);
    }

    /// <summary>반경까지 퍼지며 옅어지는 원판. 포탑을 끌어도 흔들리지 않게 부모 없이 월드에 둔다.</summary>
    private void PlayBlastVisual(Vector3 center)
    {
        if (blastDiscs.Count == 0) BuildBlastDiscs();
        if (blastDiscs.Count == 0) return;

        int slot = nextDisc % blastDiscs.Count;
        nextDisc = (nextDisc + 1) % blastDiscs.Count;

        MeshRenderer disc = blastDiscs[slot];
        if (disc == null) return;

        blastTweens[slot]?.Kill();

        Transform t = disc.transform;
        center.y = blastHeight;
        t.position = center;

        disc.enabled = true;

        float start = blastRadius * blastStartRatio;
        t.localScale = new Vector3(start, 1f, start);
        SetDiscAlpha(disc, blastColor.a);

        Sequence sequence = DOTween.Sequence();
        sequence.Join(t.DOScale(new Vector3(blastRadius, 1f, blastRadius), blastVisualDuration).SetEase(Ease.OutQuad));
        sequence.Join(DOVirtual.Float(blastColor.a, 0f, blastVisualDuration, a => SetDiscAlpha(disc, a)));
        sequence.OnComplete(() => disc.enabled = false);

        blastTweens[slot] = sequence;
    }

    private void SetDiscAlpha(MeshRenderer disc, float alpha)
    {
        if (disc == null) return;

        Color color = blastColor;
        color.a = alpha;

        disc.GetPropertyBlock(blastBlock);
        blastBlock.SetColor(SpriteColorId, color);
        disc.SetPropertyBlock(blastBlock);
    }

    private void BuildBlastDiscs()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        blastMesh = BuildDisc(40);
        blastBlock = new MaterialPropertyBlock();

        Material material = new Material(shader);

        for (int i = 0; i < Mathf.Max(1, blastDiscCount); i++)
        {
            GameObject go = new GameObject("ChainBlast" + i);

            go.AddComponent<MeshFilter>().sharedMesh = blastMesh;

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            blastDiscs.Add(renderer);
            blastTweens.Add(null);
        }
    }

    /// <summary>반지름 1인 XZ 평면 원판. Sprites/Default 는 Cull Off 라 감김 방향을 신경 쓰지 않아도 된다.</summary>
    private static Mesh BuildDisc(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        Mesh mesh = new Mesh { name = "ChainBlastDisc" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    // 원판을 부모 없이 만들었으므로 포탑이 사라질 때 직접 치운다.
    private void OnDestroy()
    {
        for (int i = 0; i < blastDiscs.Count; i++)
        {
            blastTweens[i]?.Kill();
            if (blastDiscs[i] != null) Destroy(blastDiscs[i].gameObject);
        }

        blastDiscs.Clear();
        blastTweens.Clear();

        if (blastMesh != null) Destroy(blastMesh);
    }
}
