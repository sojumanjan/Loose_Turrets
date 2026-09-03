// 15웨이브에 단 하나만 등장하는 보스. 잡몹과 달리 웨이브 시간이 아니라 이 녀석의 죽음으로 웨이브가 끝난다.
//
// 이 게임의 실력은 회피가 아니라 배치라서, 보스도 탄막이 아니라 배치를 시험한다.
//   1페이즈: 플레이어를 쫓는다. 포탑을 끌어다 길목을 막게 만든다.
//   2페이즈: 체력이 절반 밑으로 떨어지면 각성한다. 그 자리에 잠깐 굳어 무적이 되고,
//            풀리는 순간부터 빨라진 속도로 다시 플레이어를 쫓는다.
//            각성 뒤로는 맞을 때마다 복어처럼 부풀어서, 남은 체력이 눈으로 보인다.
//
// 보스전에 화면으로 나가는 문구는 전부 아래 "문구" 항목에 모아뒀다. 프리팹 인스펙터에서 고치면 된다.
// 경고 / 등장 / 처치 문구는 보스 인스턴스가 없거나 이미 죽은 뒤에 떠야 하므로,
// GameManager가 인스턴스가 아니라 프리팹에서 직접 읽어 간다.

using DG.Tweening;
using UnityEngine;

public class BossEnemy : EnemyBase
{
    /// <summary>지금 살아있는 보스. HUD의 체력바와 GameManager의 웨이브 판정이 이걸 본다.</summary>
    public static BossEnemy Current { get; private set; }

    [Header("문구")]
    [Tooltip("보스 웨이브 직전 쉬는 시간에 화면 중앙에 뜨는 경고.")]
    [TextArea(1, 2)] [SerializeField] private string warningMessage = "아주 강력한 적이 다가오고 있습니다!!";

    [Tooltip("보스가 나오는 순간의 배너.")]
    [SerializeField] private string appearBanner = "보스 등장";

    [SerializeField] private Color appearBannerColor = new Color(1f, 0.35f, 0.35f);

    [Tooltip("체력 절반을 넘겨 2페이즈에 들어갈 때의 배너.")]
    [SerializeField] private string phase2Banner = "보스가 각성했다!";

    [SerializeField] private Color phase2BannerColor = new Color(1f, 0.4f, 0.35f);

    [Tooltip("보스를 잡았을 때의 배너. 포탑 슬롯 보상을 알리는 문구다.")]
    [SerializeField] private string defeatBanner = "포탑을 하나씩 더 놓을 수 있다!";

    [SerializeField] private Color defeatBannerColor = new Color(1f, 0.86f, 0.36f);

    [Header("페이즈")]
    [Tooltip("체력이 이 비율 이하로 떨어지면 2페이즈. 0.5면 절반.")]
    [Range(0.05f, 0.95f)] [SerializeField] private float phase2HpRatio = 0.5f;

    [Header("2페이즈 — 각성")]
    [Tooltip("각성하는 동안 제자리에 굳어 있는 시간(초). 이 동안은 피해도 들어가지 않는다. "
             + "끝나는 순간부터 다시 플레이어를 쫓는다.")]
    [Min(0f)] [SerializeField] private float phase2PauseDuration = 2f;

    [Tooltip("굳은 게 풀리는 순간의 이동 속도. EnemyDef를 연결해 뒀어도 이 값이 이긴다.")]
    [Min(0f)] [SerializeField] private float phase2StartSpeed = 4f;

    [Tooltip("가속이 끝난 뒤의 이동 속도. 여기 도달하면 더 안 빨라진다.")]
    [Min(0f)] [SerializeField] private float phase2EndSpeed = 6f;

    [Tooltip("시작 속도에서 최종 속도까지 가는 데 걸리는 시간(초). "
             + "질질 끌수록 도망칠 수 없게 만드는 장치라, 목표 클리어 시간보다 길게 잡는다.")]
    [Min(0.1f)] [SerializeField] private float phase2SpeedRampDuration = 75f;

    [Header("몸통")]
    [Tooltip("플레이어와 닿았다고 볼 거리. 보이는 몸 반경 + 플레이어 반경(0.5)으로 잡는다. "
             + "보스는 몸이 3칸짜리라 반경 1.5 + 0.5 = 2가 기준이다. "
             + "EnemyDef를 연결해 뒀어도 이 값이 이기고, 부풀면 같이 커진다.")]
    [Min(0f)] [SerializeField] private float bodyContactRadius = 2f;

    [Header("2페이즈 — 부풀기")]
    [Tooltip("각성 뒤 맞을 때마다 커진다. 아래 체력 비율에서 원래 크기의 몇 배가 될지.")]
    [Min(1f)] [SerializeField] private float puffMaxScale = 1.6f;

    [Tooltip("이 체력 비율에서 최대 크기가 된다. 0.1이면 10% 남았을 때 가장 크다. "
             + "그 밑으로 더 깎여도 이 크기에서 멈춘다.")]
    [Range(0f, 0.9f)] [SerializeField] private float puffMaxHpRatio = 0.1f;

    [Tooltip("한 번 맞았을 때 새 크기까지 부푸는 데 걸리는 시간(초).")]
    [Min(0f)] [SerializeField] private float puffGrowDuration = 0.18f;

    [Header("연출")]
    [SerializeField] private Color phase2Tint = new Color(1f, 0.4f, 0.35f);
    [Range(0f, 1f)] [SerializeField] private float phase2TintStrength = 0.6f;

    [Tooltip("굳어 있는 동안 이 색으로 깜빡인다.")]
    [SerializeField] private Color invincibleTint = new Color(1f, 0.95f, 0.6f);

    [Tooltip("깜빡임이 한 번 바뀌는 데 걸리는 시간(초).")]
    [Min(0.02f)] [SerializeField] private float invincibleBlinkInterval = 0.12f;

    [Min(0.05f)] [SerializeField] private float phaseChangeShake = 0.35f;

    /// <summary>2페이즈에 들어섰는가. HUD가 체력바 색을 바꾸는 데도 쓴다.</summary>
    public bool InPhase2 { get; private set; }

    /// <summary>각성 직후 굳어 있는 구간인가. 이 동안은 움직이지도, 피해를 받지도 않는다.</summary>
    public bool IsStunned => InPhase2 && Time.time < phase2ResumeTime;

    // GameManager가 보스가 없는 시점(경고 / 등장 / 처치 직후)에도 읽는다.
    // 그래서 인스턴스가 아니라 프리팹에서도 꺼낼 수 있어야 한다.
    public string WarningMessage => warningMessage;
    public string AppearBanner => appearBanner;
    public Color AppearBannerColor => appearBannerColor;
    public string DefeatBanner => defeatBanner;
    public Color DefeatBannerColor => defeatBannerColor;

    // 굳은 게 풀리는 시각. 이때부터 속도가 오르기 시작한다.
    private float phase2ResumeTime;

    // 가속을 시작한 시각. 음수면 아직 안 움직이기 시작한 것이다.
    private float speedRampStartTime = -1f;

    // 부풀기 계산의 기준이 되는, 각성 직전의 크기.
    private Vector3 puffBaseScale = Vector3.one;

    // 깜빡임이 지금 밝은 쪽인지. 굳은 게 풀리면 2페이즈 색으로 되돌린다.
    private bool blinkBright;
    private float nextBlinkTime;

    protected override void Awake()
    {
        base.Awake();

        Current = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Current = this;
        InPhase2 = false;
        phase2ResumeTime = 0f;
        speedRampStartTime = -1f;
        blinkBright = false;
        nextBlinkTime = 0f;

        // HitFeedback이 OnDisable에서 기준 크기를 원래대로 되돌려 놓는다. 그 값을 받아둔다.
        puffBaseScale = feedback != null ? feedback.BaseScale : transform.localScale;

        // ApplyDef는 Awake에서 돈다. 여기서 덮어야 EnemyDef 값이 아니라 이 값이 남는다.
        contactRadius = bodyContactRadius;
    }

    /// <summary>보스는 오라 포탑의 감속에 걸리지 않는다. 느려진 보스는 그냥 안 오는 보스다.</summary>
    public override void ApplySlow(float factor, float duration)
    {
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (Current == this) Current = null;
    }

    // ---------------- 이동 ----------------

    protected override void Move(Vector3 targetPosition)
    {
        // 각성 직후에는 그 자리에 굳는다. 풀리면 다시 쫓는다.
        if (IsStunned) return;

        MoveStraightTo(targetPosition);
    }

    // ---------------- 페이즈 ----------------

    protected override void Update()
    {
        base.Update();

        if (!IsAlive || Time.timeScale <= 0f) return;

        if (!InPhase2)
        {
            if (HpRatio <= phase2HpRatio) EnterPhase2();
            return;
        }

        TickStunBlink();
        TickSpeedRamp();
    }

    private void EnterPhase2()
    {
        InPhase2 = true;

        // 각성 순간에는 잠깐 손을 못 대게 한다. 절반을 깎던 화력이 끊김 없이 이어지면 전환이 안 보인다.
        phase2ResumeTime = Time.time + phase2PauseDuration;
        speedRampStartTime = -1f;

        if (feedback != null) feedback.SetTint(phase2Tint, phase2TintStrength);

        transform.DOShakeScale(phaseChangeShake, 0.4f, 10, 90f);
        GameHud.Instance?.ShowBanner(phase2Banner, phase2BannerColor);
    }

    /// <summary>
    /// 굳은 게 풀린 뒤로 시간에 비례해 속도를 올린다.
    /// 오래 끌수록 빨라지므로, 도망 다니며 시간을 버는 전략이 저절로 막힌다.
    /// 이동 속도는 EnemyDef가 아니라 이 프리팹 값을 쓴다. 밸런싱할 숫자를 한곳에 둔다.
    /// </summary>
    private void TickSpeedRamp()
    {
        if (IsStunned) return;

        // 움직이기 시작한 순간을 0초로 잡는다. 굳어 있는 동안 빨라져 봐야 보이지 않는다.
        if (speedRampStartTime < 0f) speedRampStartTime = Time.time;

        float t = Mathf.Clamp01((Time.time - speedRampStartTime) / phase2SpeedRampDuration);
        moveSpeed = Mathf.Lerp(phase2StartSpeed, phase2EndSpeed, t);
    }

    /// <summary>굳어 있는 동안 몸을 깜빡인다. 풀리면 2페이즈 색으로 되돌린다.</summary>
    private void TickStunBlink()
    {
        if (feedback == null) return;

        if (!IsStunned)
        {
            if (!blinkBright) return;

            blinkBright = false;
            feedback.SetTint(phase2Tint, phase2TintStrength);
            return;
        }

        if (Time.time < nextBlinkTime) return;

        nextBlinkTime = Time.time + invincibleBlinkInterval;
        blinkBright = !blinkBright;

        feedback.SetTint(blinkBright ? invincibleTint : phase2Tint,
                         blinkBright ? 1f : phase2TintStrength);
    }

    // ---------------- 피격 / 죽음 ----------------

    /// <summary>
    /// 굳어 있는 동안에는 어떤 피해도 들어가지 않는다. 인수가 하나인 쪽도 결국 여기로 들어온다.
    /// 각성 뒤에는 맞은 만큼 몸집을 키운다.
    /// </summary>
    public override void TakeDamage(float amount, Vector3 hitFrom, TurretDef source)
    {
        if (IsStunned) return;

        // 부풀기를 base보다 먼저 처리한다. base가 부르는 피격 펀치는 그 시점의 기준 크기에서
        // 출발해 같은 자리로 돌아오므로, 나중에 키우면 펀치가 끝나면서 도로 작아진다.
        if (InPhase2 && IsAlive) ApplyPuff(ProjectedHpRatio(amount));

        base.TakeDamage(amount, hitFrom, source);
    }

    /// <summary>이 피해가 들어간 뒤의 체력 비율. hp는 EnemyBase의 private이라 공개된 값으로 되짚는다.</summary>
    private float ProjectedHpRatio(float amount)
    {
        if (MaxHp <= 0f) return 0f;

        return Mathf.Clamp01(HpRatio - amount / MaxHp);
    }

    /// <summary>체력이 phase2HpRatio에서 puffMaxHpRatio로 갈수록 puffMaxScale까지 부푼다.</summary>
    private void ApplyPuff(float hpRatio)
    {
        if (feedback == null) return;

        float t = Mathf.InverseLerp(phase2HpRatio, puffMaxHpRatio, hpRatio);
        float multiplier = Mathf.Lerp(1f, puffMaxScale, t);

        feedback.SetBaseScale(puffBaseScale * multiplier, puffGrowDuration);

        // 몸이 커졌으면 닿는 범위도 같이 커져야 한다. 안 그러면 부풀수록 보이는 것과 어긋난다.
        contactRadius = bodyContactRadius * multiplier;
    }

    protected override void OnDeath()
    {
        base.OnDeath();

        if (GameManager.Instance != null) GameManager.Instance.OnBossDefeated();
    }
}
