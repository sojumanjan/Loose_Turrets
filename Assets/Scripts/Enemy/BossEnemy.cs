// 15웨이브에 단 하나만 등장하는 보스. 잡몹과 달리 웨이브 시간이 아니라 이 녀석의 죽음으로 웨이브가 끝난다.
//
// 이 게임의 실력은 회피가 아니라 배치라서, 보스도 탄막이 아니라 배치를 시험한다.
//   1페이즈: 아주 느리게 플레이어를 쫓는다. 포탑을 끌어다 길목을 막게 만든다.
//   2페이즈: 추적을 멈추고 네 꼭짓점을 순간이동한다. 자리를 옮길 때마다 포탑을 다시 끌어야 한다.
//
// 체력은 EnemyDef가 아니라 이 프리팹의 maxHp를 그대로 쓴다(def를 비워둔다).
// 웨이브 체력 배율도 타지 않으므로, 밸런싱할 숫자가 하나뿐이다.

using DG.Tweening;
using UnityEngine;

public class BossEnemy : EnemyBase
{
    /// <summary>지금 살아있는 보스. HUD의 체력바와 GameManager의 웨이브 판정이 이걸 본다.</summary>
    public static BossEnemy Current { get; private set; }

    [Header("페이즈")]
    [Tooltip("체력이 이 비율 이하로 떨어지면 2페이즈. 0.5면 절반.")]
    [Range(0.05f, 0.95f)] [SerializeField] private float phase2HpRatio = 0.5f;

    [Header("2페이즈 — 꼭짓점 순간이동")]
    [Tooltip("한 자리에 머무는 시간(초). 짧을수록 포탑을 자주 옮겨야 한다.")]
    [Min(1f)] [SerializeField] private float teleportInterval = 6f;

    [Tooltip("사라지기 전 예고 시간(초). 이 동안 깜빡여서 다음 이동을 알린다.")]
    [Min(0.1f)] [SerializeField] private float telegraphDuration = 1.2f;

    [Tooltip("꼭짓점을 아레나 안쪽으로 얼마나 당길지. 1이면 벽에 딱 붙고, 0.8이면 20% 안쪽이다. "
             + "너무 바깥이면 포탑 사거리가 안 닿는다.")]
    [Range(0.3f, 1f)] [SerializeField] private float cornerInset = 0.8f;

    [Header("연출")]
    [SerializeField] private Color phase2Tint = new Color(1f, 0.4f, 0.35f);
    [Min(0.05f)] [SerializeField] private float phaseChangeShake = 0.35f;

    /// <summary>2페이즈에 들어섰는가. HUD가 체력바 색을 바꾸는 데도 쓴다.</summary>
    public bool InPhase2 { get; private set; }

    private float nextTeleportTime;
    private int cornerIndex = -1;
    private Tween telegraphTween;

    protected override void Awake()
    {
        base.Awake();

        // 보스는 EnemyDef 없이 프리팹 값만 쓴다. 웨이브 체력 배율이 끼어들면 밸런싱 숫자가 둘이 된다.
        Current = this;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Current = this;
        InPhase2 = false;
        cornerIndex = -1;
        nextTeleportTime = 0f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        telegraphTween?.Kill();
        if (Current == this) Current = null;
    }

    // ---------------- 이동 ----------------

    protected override void Move(Vector3 targetPosition)
    {
        // 2페이즈는 쫓지 않는다. 자리는 순간이동으로만 바뀐다.
        if (InPhase2) return;

        MoveStraightTo(targetPosition);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsAlive || Time.timeScale <= 0f) return;

        if (!InPhase2)
        {
            if (HpRatio <= phase2HpRatio) EnterPhase2();
            return;
        }

        TickTeleport();
    }

    // ---------------- 2페이즈 ----------------

    private void EnterPhase2()
    {
        InPhase2 = true;

        // 첫 순간이동은 예고를 거쳐 나가도록 지금부터 한 주기를 준다.
        nextTeleportTime = Time.time + teleportInterval;

        if (feedback != null) feedback.SetTint(phase2Tint, 0.6f);

        transform.DOShakeScale(phaseChangeShake, 0.4f, 10, 90f);
        GameHud.Instance?.ShowBanner("보스가 각성했다!", phase2Tint);
    }

    private void TickTeleport()
    {
        if (Time.time < nextTeleportTime - telegraphDuration) return;

        // 예고 구간. 깜빡임은 한 번만 걸고 놔둔다.
        if (telegraphTween == null || !telegraphTween.IsActive())
            telegraphTween = transform.DOPunchScale(Vector3.one * 0.15f, telegraphDuration, 12, 0.4f);

        if (Time.time < nextTeleportTime) return;

        Teleport();
        nextTeleportTime = Time.time + teleportInterval;
    }

    private void Teleport()
    {
        telegraphTween?.Kill(true);

        // 직전과 다른 꼭짓점으로만 간다. 같은 자리에 다시 나오면 옮길 이유가 없어진다.
        int next = cornerIndex < 0
            ? Random.Range(0, 4)
            : (cornerIndex + 1 + Random.Range(0, 3)) % 4;

        cornerIndex = next;
        transform.position = CornerPosition(next);

        // 도착을 눈에 띄게. 0에서 부풀어 오르는 편이 갑자기 나타나는 것보다 읽기 쉽다.
        transform.localScale = BaseVisualScale * 0.4f;
        transform.DOScale(BaseVisualScale, 0.25f).SetEase(Ease.OutBack);
    }

    /// <summary>네 꼭짓점 좌표. 벽에 딱 붙으면 포탑 사거리가 안 닿으므로 안쪽으로 당긴다.</summary>
    private Vector3 CornerPosition(int index)
    {
        Vector2 half = ArenaBounds.HalfSize * cornerInset;

        float x = (index == 0 || index == 3) ? -half.x : half.x;
        float z = (index == 0 || index == 1) ? half.y : -half.y;

        return new Vector3(x, 0f, z);
    }

    /// <summary>HitFeedback이 기억하는 원래 크기. 순간이동 연출이 크기를 망가뜨리지 않게 기준을 맞춘다.</summary>
    private Vector3 BaseVisualScale => feedback != null ? feedback.BaseScale : Vector3.one;

    // ---------------- 죽음 ----------------

    protected override void OnDeath()
    {
        base.OnDeath();

        if (GameManager.Instance != null) GameManager.Instance.OnBossDefeated();
    }
}
