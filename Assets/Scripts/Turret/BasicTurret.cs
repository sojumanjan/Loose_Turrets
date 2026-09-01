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

    [Tooltip("총구가 늘어날 때 총알끼리 벌어지는 좌우 간격.")]
    [SerializeField] private float barrelSpacing = 0.24f;

    [Tooltip("총구가 늘어날 때 벌어지는 각도(도). 0이면 평행하게 나간다.")]
    [SerializeField] private float spreadAngle = 4f;

    // ---------------- 특수 강화 2 : 연사 뻥튀기 ----------------

    [Header("특수 강화 2 — 연사 뻥튀기")]
    [Tooltip("비워두면 이 포탑에는 두 번째 특수가 없는 것으로 보고 카드를 내지 않는다.")]
    [SerializeField] private string special2Title = "";

    [TextArea(2, 3)] [SerializeField] private string special2Description = "";

    [Tooltip("두 번째 특수를 먹으면 연사에 곱해지는 배율. 2.5면 250%, 즉 2.5배 빨라진다.")]
    [Min(1f)] [SerializeField] private float special2FireRate = 2.5f;

    public override string SpecialTitle => specialTitle;
    public override string SpecialDescription => specialDescription;
    public override string Special2Title => special2Title;
    public override string Special2Description => special2Description;

    private int BulletsPerSpecial => Mathf.Max(1, bulletsPerSpecial);

    // 두 번째 특수를 먹기 전에는 1이라 아무 영향이 없다.
    protected override float Special2FireRateMultiplier =>
        Special2Level > 0 ? special2FireRate : 1f;

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

            Vector3 shotOrigin = origin + right * (t * barrelSpacing * (shots - 1));
            Vector3 shotDirection = Quaternion.Euler(0f, t * spreadAngle * (shots - 1), 0f) * baseDirection;

            BulletPool.Instance.Fire(bulletPrefab, shotOrigin, shotDirection, EffectiveDamage, Def);
        }
    }
}
