// 오라 포탑. 조준하지 않고 자기 주변 원 안의 모든 적에게 일정 간격으로 지속 피해를 준다.
// 첫 번째 특수 강화는 같은 범위의 적을 감속시키고,
// 두 번째 특수 강화는 몇 번째 공격마다 그 공격 자체를 폭발로 바꿔 몇 배의 피해를 준다.
// 시간 간격이 아니라 공격 횟수로 세는 이유는, 공격 속도 강화가 폭발 빈도에도 반영되게 하려는 것이다.
// 시간으로 세면 오라의 가치가 공격력에만 몰린다.

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class AuraTurret : TurretBase
{
    // ---------------- 특수 강화 1 : 감속 ----------------

    [Header("특수 강화 1 — 감속")]
    [SerializeField] private string specialTitle = "얼어붙어라";

    [TextArea(2, 3)] [SerializeField] private string specialDescription = "";

    [Tooltip("특수 강화 후 적 이동 속도에 곱해지는 값. 0.7이면 30% 감속.")]
    [Range(0.05f, 1f)] [SerializeField] private float slowFactor = 0.7f;

    [Tooltip("감속 지속 시간. 발사 간격보다 길어야 범위 안에 있는 동안 끊기지 않는다.")]
    [SerializeField] private float slowDuration = 0.7f;

    // ---------------- 특수 강화 2 : 주기적 폭발 ----------------

    [Header("특수 강화 2 — 몇 번째 공격마다 폭발")]
    [Tooltip("비워두면 이 포탑에는 두 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special2Title = "";

    [TextArea(2, 3)] [SerializeField] private string special2Description = "";

    [Tooltip("몇 번째 공격이 폭발할지. 6이면 여섯 번째 공격마다 그 공격이 폭발이 된다.")]
    [Min(1)] [SerializeField] private int blastEveryShots = 6;

    [Tooltip("폭발하는 공격의 피해 배율. 4면 그 공격만 평소의 4배로 들어간다.")]
    [Min(0f)] [SerializeField] private float blastDamageMultiplier = 4f;

    // ---------------- 특수 강화 3 : 폭발 주기 단축 ----------------

    [Header("특수 강화 3 — 폭발 주기 단축")]
    [Tooltip("비워두면 이 포탑에는 세 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special3Title = "";

    [TextArea(2, 3)] [SerializeField] private string special3Description = "";

    [Tooltip("세 번째 특수를 먹으면 폭발까지 필요한 공격 횟수에 곱해지는 값. 0.5면 절반이 되어 두 배 자주 터진다.")]
    [Range(0.1f, 1f)] [SerializeField] private float special3BlastIntervalScale = 0.5f;

    /// <summary>실제로 쓰이는 폭발 주기(공격 횟수). 세 번째 특수를 먹으면 짧아진다.</summary>
    private int EffectiveBlastEveryShots =>
        Mathf.Max(1, Mathf.RoundToInt(blastEveryShots * (Special3Level > 0 ? special3BlastIntervalScale : 1f)));

    public override string SpecialTitle => specialTitle;
    public override string SpecialDescription => specialDescription;
    public override string Special2Title => special2Title;
    public override string Special2Description => special2Description;
    public override string Special3Title => special3Title;
    public override string Special3Description => special3Description;

    [Header("폭발 연출")]
    [Tooltip("퍼져나가는 충격파 색. 알파가 시작 진하기다.")]
    [SerializeField] private Color blastColor = new Color(0.55f, 1f, 0.75f, 0.55f);

    [Tooltip("충격파가 사거리 끝까지 퍼지며 사라지는 시간.")]
    [Min(0.05f)] [SerializeField] private float blastVisualDuration = 0.35f;

    [Tooltip("충격파가 시작하는 크기. 사거리 대비 비율.")]
    [Range(0f, 1f)] [SerializeField] private float blastStartRatio = 0.15f;

    [Tooltip("바닥보다 살짝 위여야 파묻히지 않는다. 사거리 채움보다 위에 둔다.")]
    [SerializeField] private float blastHeight = 0.05f;

    [Tooltip("폭발이 터질 때 나는 소리. 오라의 반복음(TurretDef.LoopSfx)과는 별개다. 비우면 조용히 터진다.")]
    [SerializeField] private SfxDef blastSfx;

    // 매 틱마다 리스트를 새로 만들지 않도록 공용 버퍼를 쓴다.
    private static readonly List<EnemyBase> buffer = new List<EnemyBase>(64);

    private Tween auraPulse;

    // 마지막 폭발 이후 쏜 횟수. 이 값이 주기에 닿으면 그 공격이 폭발이 된다.
    private int shotsSinceBlast;

    private MeshRenderer blastDisc;
    private Mesh blastMesh;
    private MaterialPropertyBlock blastBlock;
    private Sequence blastTween;
    private static readonly int SpriteColorId = Shader.PropertyToID("_Color");


    // 오라는 범위가 곧 정체성이라 항상 보여준다.
    protected override bool AlwaysShowRange => true;

    protected override void Fire(EnemyBase target)
    {
        // target은 쓰지 않는다. 범위 안 전체가 대상이다.
        EnemyRegistry.FindAllInRange(transform.position, EffectiveRange, buffer);

        bool slows = SpecialLevel > 0;

        // 이번 공격이 폭발인지 먼저 정한다. 그래야 한 번의 순회로 피해까지 끝난다.
        bool blast = ConsumeBlastShot();
        float damage = blast ? EffectiveDamage * blastDamageMultiplier : EffectiveDamage;

        for (int i = 0; i < buffer.Count; i++)
        {
            EnemyBase enemy = buffer[i];
            if (enemy == null || !enemy.IsAlive) continue;

            if (slows) enemy.ApplySlow(slowFactor, slowDuration);

            // 데미지를 나중에 준다. 먼저 주면 이번 틱에 죽는 적에게 감속이 안 걸린다.
            enemy.TakeDamage(damage, transform.position, Def);
        }

        if (!blast) return;

        SfxManager.Play(blastSfx, transform.position);
        PlayBlastVisual();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        blastTween?.Kill();
        if (blastMesh != null) Destroy(blastMesh);
    }

    // ---------------------------------------------------------------- 몇 번째 공격마다 폭발

    /// <summary>
    /// 이번 공격이 폭발이면 true. 세는 것은 두 번째 특수를 먹은 뒤부터다.
    /// 부르는 즉시 카운터를 소모하므로 한 공격에 한 번만 불러야 한다.
    /// </summary>
    private bool ConsumeBlastShot()
    {
        if (Special2Level <= 0) return false;

        shotsSinceBlast++;
        if (shotsSinceBlast < EffectiveBlastEveryShots) return false;

        shotsSinceBlast = 0;
        return true;
    }

    /// <summary>사거리 끝까지 퍼지며 옅어지는 충격파 원판.</summary>
    private void PlayBlastVisual()
    {
        if (blastDisc == null) BuildBlastDisc();
        if (blastDisc == null) return;

        Transform t = blastDisc.transform;

        // 포탑을 집으면 부모가 커지므로 그만큼 나눠 사거리를 정확히 맞춘다.
        float parentScale = transform.lossyScale.x;
        float radius = parentScale > 0.0001f ? EffectiveRange / parentScale : EffectiveRange;

        Vector3 center = transform.position;
        center.y = blastHeight;
        t.position = center;

        blastTween?.Kill();
        blastDisc.enabled = true;

        t.localScale = new Vector3(radius * blastStartRatio, 1f, radius * blastStartRatio);
        SetBlastAlpha(blastColor.a);

        blastTween = DOTween.Sequence();
        blastTween.Join(t.DOScale(new Vector3(radius, 1f, radius), blastVisualDuration).SetEase(Ease.OutQuad));
        blastTween.Join(DOVirtual.Float(blastColor.a, 0f, blastVisualDuration, SetBlastAlpha));
        blastTween.OnComplete(() => blastDisc.enabled = false);
    }

    private void SetBlastAlpha(float alpha)
    {
        if (blastDisc == null) return;

        Color color = blastColor;
        color.a = alpha;

        blastDisc.GetPropertyBlock(blastBlock);
        blastBlock.SetColor(SpriteColorId, color);
        blastDisc.SetPropertyBlock(blastBlock);
    }

    private void BuildBlastDisc()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;

        blastMesh = BuildDisc(48);

        GameObject go = new GameObject("BlastWave");
        go.transform.SetParent(transform, false);

        go.AddComponent<MeshFilter>().sharedMesh = blastMesh;

        blastDisc = go.AddComponent<MeshRenderer>();
        blastDisc.sharedMaterial = new Material(shader);
        blastDisc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blastDisc.receiveShadows = false;
        blastDisc.enabled = false;

        blastBlock = new MaterialPropertyBlock();
    }

    /// <summary>반지름 1인 XZ 평면 원판. Sprites/Default 는 Cull Off 라 감김 방향을 신경 쓰지 않아도 된다.</summary>
    private static Mesh BuildDisc(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        colors[0] = Color.white;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            colors[i + 1] = Color.white;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        Mesh mesh = new Mesh { name = "AuraBlastDisc" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    // 오라는 방향이 없으므로 조준 회전은 하지 않는다.
    protected override void AimAt(Vector3 worldPosition) { }

    // 틱마다 치는 맥동. 방향이 없는 포탑이라 앞뒤로 눌리는 반동 대신 사방으로 균등하게 부푼다.
    // 균등 배율이어야 하는 이유가 하나 더 있다. 사거리 채움 원판은 부모 스케일의 x로만 반지름을 되돌리므로,
    // z만 눌리면 원이 타원으로 찌그러진다.
    protected override void PlayRecoil()
    {
        if (IsDragScaling) return;

        auraPulse?.Kill(true);
        auraPulse = transform.DOPunchScale(Vector3.one * recoilStrength, recoilDuration, 6, 0.8f);
    }

    // 틱마다 발사음을 내면 기관총이 된다. 오라의 소리는 TurretDef의 LoopSfx가 맡는다.
    protected override void PlayFireSfx() { }
}
