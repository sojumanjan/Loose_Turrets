// 플레이어를 쫓지 않고 아레나 안을 배회하는 적. 경계에 닿으면 반사각으로 튕겨 나가고, 가끔 방향을 새로 고른다.
// 아레나 밖에서 스폰되므로 처음 안으로 들어올 때까지는 반사 판정을 하지 않는다.

using UnityEngine;

public class WandererEnemy : EnemyBase
{
    [Header("배회")]
    [Tooltip("이 시간마다 방향을 새로 고른다. 0이면 벽에 튕길 때만 방향이 바뀐다.")]
    [SerializeField] private float directionChangeInterval = 2.5f;

    [Tooltip("아레나로 처음 진입할 때 중앙 방향에서 벌어질 수 있는 최대 각도.")]
    [SerializeField] private float entryAngleSpread = 35f;

    private Vector3 direction;
    private float retargetTimer;
    private bool insideArena;

    protected override void OnEnable()
    {
        base.OnEnable();

        insideArena = false;
        retargetTimer = directionChangeInterval;

        // 스폰 지점은 화면 밖이다. 우선 중앙 쪽으로 향하게 해서 아레나 안으로 들어오게 한다.
        Vector3 toCenter = -transform.position;
        toCenter.y = 0f;

        direction = toCenter.sqrMagnitude > 0.0001f ? toCenter.normalized : RandomDirection();
        direction = Quaternion.Euler(0f, Random.Range(-entryAngleSpread, entryAngleSpread), 0f) * direction;
    }

    // targetPosition(플레이어 위치)은 쓰지 않는다. 이 적은 플레이어를 쫓지 않는다.
    protected override void Move(Vector3 targetPosition)
    {
        TickRetarget();

        Vector3 next = transform.position + direction * (CurrentSpeed * Time.deltaTime);

        if (!insideArena)
        {
            // 아직 진입 전이면 매 프레임 중앙 쪽으로 방향을 다시 잡는다.
            // 밀어내기에 떠밀려 바깥으로 흘러가면 영영 못 들어와서 맵 밖을 떠돌게 된다.
            if (IsInside(next)) insideArena = true;
            else AimAtCenter();
        }
        else
        {
            Reflect(ref next);
        }

        next.y = 0f;
        transform.position = next;
    }

    private void TickRetarget()
    {
        if (directionChangeInterval <= 0f || !insideArena) return;

        retargetTimer -= Time.deltaTime;
        if (retargetTimer > 0f) return;

        retargetTimer = directionChangeInterval;
        direction = RandomDirection();
    }

    /// <summary>경계를 넘으면 해당 축의 방향을 뒤집고 경계 안으로 되돌린다.</summary>
    private void Reflect(ref Vector3 next)
    {
        Vector2 bounds = ArenaBounds.HalfSize;

        if (next.x < -bounds.x || next.x > bounds.x)
        {
            direction.x = -direction.x;
            next.x = Mathf.Clamp(next.x, -bounds.x, bounds.x);
        }

        if (next.z < -bounds.y || next.z > bounds.y)
        {
            direction.z = -direction.z;
            next.z = Mathf.Clamp(next.z, -bounds.y, bounds.y);
        }
    }

    /// <summary>중앙을 향해 방향을 다시 잡는다. 아레나 밖에 있는 동안에만 쓴다.</summary>
    private void AimAtCenter()
    {
        Vector3 toCenter = -transform.position;
        toCenter.y = 0f;

        if (toCenter.sqrMagnitude < 0.0001f) return;

        direction = toCenter.normalized;
    }

    private bool IsInside(Vector3 position)
    {
        Vector2 bounds = ArenaBounds.HalfSize;
        return Mathf.Abs(position.x) <= bounds.x && Mathf.Abs(position.z) <= bounds.y;
    }

    private static Vector3 RandomDirection()
    {
        float angle = Random.value * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = new Color(1f, 1f, 0.4f, 0.8f);
        Gizmos.DrawLine(transform.position, transform.position + direction * 2f);
    }
}
