// 가장 기본 포탑. 사거리 안 최근접 적에게 총알을 쏜다.
// 특수 강화(총구 추가)를 받을 때마다 한 번에 나가는 총알이 한 발씩 늘어나고, 좌우로 나란히 퍼진다.

using UnityEngine;

public class BasicTurret : TurretBase
{
    [Header("총알")]
    [SerializeField] private Bullet bulletPrefab;

    // ---------------- 특수 강화 1 : 총구 추가 ----------------

    [Header("특수 강화 1 — 총구 추가")]
    [SerializeField] private string specialTitle = "총알 복사";

    [TextArea(2, 3)] [SerializeField] private string specialDescription = "";

    [Tooltip("특수 강화 1회당 늘어나는 총알 수.")]
    [Min(1)] [SerializeField] private int bulletsPerSpecial = 1;

    [Tooltip("총구가 늘어날 때 총알끼리 벌어지는 좌우 간격. 게틀링이 되기 전까지 쓴다.")]
    [SerializeField] private float barrelSpacing = 0.4f;

    [Tooltip("총구가 늘어날 때 벌어지는 각도(도). 0이면 평행하게 나간다.")]
    [SerializeField] private float spreadAngle = 4f;

    // ---------------- 특수 강화 2 : 연사 뻥튀기 ----------------

    [Header("특수 강화 2 — 연사 뻥튀기")]
    [Tooltip("비워두면 이 포탑에는 두 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special2Title = "";

    [TextArea(2, 3)] [SerializeField] private string special2Description = "";

    [Tooltip("두 번째 특수를 먹으면 연사에 곱해지는 배율. 2.5면 250%, 즉 2.5배 빨라진다.")]
    [Min(1f)] [SerializeField] private float special2FireRate = 2.5f;

    [Tooltip("평소에 보이는 모델. 두 번째 특수를 먹으면 꺼진다.")]
    [SerializeField] private GameObject baseModel;

    [Tooltip("두 번째 특수를 먹으면 켜지는 게틀링건 모델. 프리팹에서는 꺼둔 채로 둔다.")]
    [SerializeField] private GameObject special2Model;

    [Tooltip("두 번째 특수를 먹은 뒤의 발사음. 비우면 TurretDef 의 FireSfx 를 그대로 쓴다.")]
    [SerializeField] private SfxDef special2FireSfx;

    [Tooltip("두 번째 특수를 먹은 뒤 반동 펀치에 곱하는 값. 연사가 몇 배로 빨라져서 " +
             "원래 세기 그대로 두면 포탑이 계속 덜덜 떨린다.")]
    [Range(0f, 1f)] [SerializeField] private float special2RecoilScale = 0.5f;

    [Tooltip("두 번째 특수를 먹은 뒤의 총알 좌우 간격. 게틀링건은 총구가 훨씬 넓게 벌어져 있어서, " +
             "위의 barrelSpacing 그대로 두면 총알이 총구가 아닌 몸통에서 나가는 것처럼 보인다.")]
    [SerializeField] private float special2BarrelSpacing = 1f;

    // ---------------- 특수 강화 3 : 관통 ----------------

    [Header("특수 강화 3 — 관통")]
    [Tooltip("비워두면 이 포탑에는 세 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special3Title = "";

    [TextArea(2, 3)] [SerializeField] private string special3Description = "";

    [Tooltip("세 번째 특수를 먹으면 총알 한 발이 때릴 수 있는 적 수. 3이면 두 명을 관통해 총 3마리를 때린다.")]
    [Min(2)] [SerializeField] private int special3PierceTargets = 3;

    /// <summary>지금 쏠 총알이 관통할 인원. 0이면 관통하지 않고 첫 적에게서 멈춘다.</summary>
    private int PierceTargets => Special3Level > 0 ? Mathf.Max(2, special3PierceTargets) : 0;

    public override string SpecialTitle => specialTitle;
    public override string SpecialDescription => specialDescription;
    public override string Special2Title => special2Title;
    public override string Special2Description => special2Description;
    public override string Special3Title => special3Title;
    public override string Special3Description => special3Description;

    private int BulletsPerSpecial => Mathf.Max(1, bulletsPerSpecial);

    /// <summary>지금 모델에 맞는 총알 좌우 간격. 게틀링으로 바뀌면 총구가 넓어지므로 같이 벌어진다.</summary>
    private float CurrentBarrelSpacing => Special2Level > 0 ? special2BarrelSpacing : barrelSpacing;

    // 두 번째 특수를 먹기 전에는 1이라 아무 영향이 없다.
    protected override float Special2FireRateMultiplier =>
        Special2Level > 0 ? special2FireRate : 1f;

    // 게틀링이 되면 펀치를 줄인다. 먹기 전에는 1이라 아무 영향이 없다.
    protected override float RecoilScale =>
        Special2Level > 0 ? special2RecoilScale : 1f;

    // 지금 게틀링건 모델을 보여주고 있는가. 상태가 바뀐 프레임에만 실제로 손대려고 들고 있다.
    private bool showingSpecial2Model;

    /// <summary>
    /// 두 번째 특수를 먹으면 모델을 게틀링건으로 바꾼다.
    /// 강화는 포탑 종류 단위(static)라 알림이 따로 없고, 카드를 고른 뒤 새로 소환된 포탑도 바뀌어 있어야 한다.
    /// 그래서 매 프레임 확인하되 달라졌을 때만 손댄다.
    /// </summary>
    protected override void Update()
    {
        base.Update();

        RefreshSpecial2Model();
    }

    private void RefreshSpecial2Model()
    {
        bool want = Special2Level > 0;
        if (want == showingSpecial2Model) return;

        showingSpecial2Model = want;

        if (baseModel != null) baseModel.SetActive(!want);
        if (special2Model != null) special2Model.SetActive(want);
    }

    /// <summary>게틀링건이 되면 총소리도 바뀐다. 비워두면 원래 소리를 그대로 쓴다.</summary>
    protected override void PlayFireSfx()
    {
        if (Special2Level <= 0 || special2FireSfx == null)
        {
            base.PlayFireSfx();
            return;
        }

        SfxManager.Play(special2FireSfx, muzzle != null ? muzzle.position : transform.position);
    }

    protected override void Fire(EnemyBase target)
    {
        if (target == null || bulletPrefab == null || BulletPool.Instance == null) return;

        int shots = 1 + SpecialLevel * BulletsPerSpecial;

        Vector3 origin = muzzle.position;
        Vector3 baseDirection = target.transform.position - origin;
        baseDirection.y = 0f;
        if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = transform.forward;
        baseDirection.Normalize();

        // 총구 방향 기준 오른쪽. 총알을 좌우로 나란히 놓는 데 쓴다.
        Vector3 right = Vector3.Cross(Vector3.up, baseDirection);

        for (int i = 0; i < shots; i++)
        {
            // -0.5 ~ +0.5 로 대칭 배치. 한 발일 때는 정확히 0.
            float t = shots == 1 ? 0f : i / (float)(shots - 1) - 0.5f;

            Vector3 shotOrigin = origin + right * (t * CurrentBarrelSpacing * (shots - 1));
            Vector3 shotDirection = Quaternion.Euler(0f, t * spreadAngle * (shots - 1), 0f) * baseDirection;

            BulletPool.Instance.Fire(bulletPrefab, shotOrigin, shotDirection, EffectiveDamage, Def,
                                     1f, PierceTargets);
        }
    }
}
