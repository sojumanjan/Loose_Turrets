// 가장 기본 포탑. 사거리 안 최근접 적에게 총알을 쏜다.
// 특수 강화(총구 추가)를 받을 때마다 한 번에 나가는 총알이 한 발씩 늘어나고, 좌우로 나란히 퍼진다.

using UnityEngine;

public class BasicTurret : TurretBase
{
    [Header("총알")]
    [SerializeField] private Bullet bulletPrefab;

    [Header("특수 강화: 총구 추가")]
    [Tooltip("총구가 늘어날 때 총알끼리 벌어지는 좌우 간격.")]
    [SerializeField] private float barrelSpacing = 0.24f;

    [Tooltip("총구가 늘어날 때 벌어지는 각도(도). 0이면 평행하게 나간다.")]
    [SerializeField] private float spreadAngle = 4f;

    [Tooltip("특수 강화(TWIN BARREL) 1회당 늘어나는 총알 수.")]
    [Min(1)] [SerializeField] private int bulletsPerSpecial = 1;

    // TurretDef가 연결돼 있으면 CSV 값이 이긴다.
    private int BulletsPerSpecial => Def != null ? Mathf.Max(1, Def.SpecialAmount) : bulletsPerSpecial;

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
