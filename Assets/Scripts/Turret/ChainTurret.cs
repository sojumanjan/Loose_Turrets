// 연쇄 레이저 포탑. 최근접 적을 쏘고, 맞은 적을 기준으로 사거리 안의 다음 적에게 계속 튄다.
// 연쇄 사거리는 포탑 사거리와 동일하며, 그 안에 남은 적이 없으면 거기서 끊긴다.

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ChainTurret : TurretBase
{
    [Header("연쇄")]
    [Tooltip("첫 대상을 포함한 최대 타격 수. 4면 처음 1명 + 연쇄 3명.")]
    [SerializeField] private int maxChainTargets = 4;

    [Tooltip("한 번 튈 때마다 곱해지는 데미지 비율. 1이면 감쇠 없음.")]
    [SerializeField] private float damageFalloff = 0.8f;

    [Tooltip("특수 강화(EXTRA BOUNCE) 1회당 늘어나는 연쇄 대상 수.")]
    [Min(1)] [SerializeField] private int chainPerSpecial = 1;

    /// <summary>기본 최대 타격 수 + 특수 강화로 늘어난 수.</summary>
    // TurretDef가 연결돼 있으면 CSV 값이 이기고, 없으면 위 인스펙터 값을 쓴다.
    private int BaseChainTargets => Def != null ? Mathf.Max(1, Def.AoeTargets) : maxChainTargets;
    private float Falloff => Def != null ? Def.AoeFalloff : damageFalloff;
    private int ChainPerSpecial => Def != null ? Mathf.Max(1, Def.SpecialAmount) : chainPerSpecial;

    /// <summary>기본 연쇄 수 + 특수 강화로 늘어난 수.</summary>
    private int EffectiveMaxChainTargets => BaseChainTargets + SpecialLevel * ChainPerSpecial;

    [Header("레이저 연출")]
    [SerializeField] private Color laserColor = new Color(0.35f, 0.75f, 1f);
    [SerializeField] private float laserWidth = 0.14f;
    [Tooltip("한 발이 보이는 시간. 짧게 두어야 '찡' 하고 번쩍이는 느낌이 난다.")]
    [SerializeField] private float laserFadeDuration = 0.14f;

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
        line.startWidth = laserWidth;
        line.endWidth = laserWidth;
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

}
