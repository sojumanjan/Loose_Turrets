// 모든 적의 공통 베이스. HP / 피격 / 사망 / 플레이어 접촉 데미지 / 적끼리 밀어내기를 처리하고, 이동 방식만 자식이 Move()로 구현한다.

using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    /// <summary>웨이브별 체력 뻥튀기 배율. 스폰 순간에만 적용되므로 이미 나온 적은 영향받지 않는다.</summary>
    public static float HpMultiplier { get; private set; } = 1f;

    public static void SetHpMultiplier(float value) => HpMultiplier = Mathf.Max(0.01f, value);

    // static은 씬을 다시 로드해도 남으므로 새 판을 시작할 때 반드시 되돌린다.
    public static void ResetHpMultiplier() => HpMultiplier = 1f;

    [Header("데이터")]
    [Tooltip("연결하면 아래 스탯을 Awake에서 이 에셋 값으로 덮어쓴다. 비우면 아래 인스펙터 값을 그대로 쓴다.")]
    [SerializeField] private EnemyDef def;

    [Header("스탯 (EnemyDef가 연결되면 덮어써짐)")]
    [SerializeField] protected float maxHp = 10f;
    [SerializeField] protected float moveSpeed = 2.5f;
    [SerializeField] protected int xpReward = 1;

    [Header("플레이어 접촉 데미지")]
    [SerializeField] protected float contactDamage = 10f;
    [SerializeField] protected float contactRadius = 0.9f;
    [SerializeField] protected float contactInterval = 0.8f;

    [Header("감속 표시")]
    [Tooltip("느려졌을 때 물드는 색. 오라 포탑의 CHILL FIELD 등이 걸었을 때 보인다.")]
    [SerializeField] private Color slowTintColor = new Color(0.45f, 0.75f, 1f);
    [SerializeField, Range(0f, 1f)] private float slowTintStrength = 0.55f;

    [Header("적끼리 밀어내기")]
    [SerializeField] protected float separationRadius = 0.85f;
    [SerializeField] protected float separationStrength = 4f;

    [Tooltip("밀어내기를 몇 프레임에 한 번 계산할지. 이 계산은 적 수의 제곱으로 커져서 "
             + "200마리면 프레임당 4만 번을 넘는다. 3이면 비용이 1/3이 되고 밀리는 느낌은 거의 같다.")]
    [Min(1)] [SerializeField] protected int separationInterval = 3;

    protected Transform target;

    private HitFeedback feedback;
    private Collider bodyCollider;
    private float hp;
    private float effectiveMaxHp;
    private float nextContactTime;

    // 밀어내기 질의용 공용 버퍼. 한 번에 적 하나만 계산하므로 전부 나눠 써도 된다.
    private static readonly List<EnemyBase> separationBuffer = new List<EnemyBase>(32);

    // 이 적이 나온 풀. 죽거나 맵을 벗어나면 파괴하지 않고 여기로 돌려보낸다.
    private SimplePool<EnemyBase> pool;

    // 밀어내기를 건너뛰는 프레임을 적마다 다르게 흩어, 같은 프레임에 전부 몰리지 않게 한다.
    private int separationPhase;

    // 실제로 밀어낸 마지막 시각. 건너뛴 프레임만큼을 한 번에 반영해야 세기가 안 변한다.
    private float lastSeparationTime;

    // 몸통 중심까지의 높이(월드). Awake에서 렌더러를 보고 한 번만 잰다.
    private float aimHeight;
    private bool dying;

    // 오라 포탑 등이 거는 감속. 만료되면 저절로 풀린다.
    private float slowFactor = 1f;
    private float slowUntil;
    private bool slowVisualOn;

    public bool IsAlive => !dying && hp > 0f;
    public Transform Transform => transform;
    public int XpReward => xpReward;
    public float HpRatio => effectiveMaxHp <= 0f ? 0f : Mathf.Clamp01(hp / effectiveMaxHp);
    public float MaxHp => effectiveMaxHp;

    /// <summary>몸통 한가운데의 월드 좌표. 루트는 지면(y=0)에 있고 몸은 그 위에 얹혀 있으므로,
    /// 레이저처럼 눈에 보이는 선을 그릴 때 루트를 노리면 발밑을 쏘는 그림이 된다.</summary>
    public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;

    /// <summary>감속이 반영된 실제 이동 속도. 이동 계산은 전부 이 값을 써야 한다.</summary>
    protected float CurrentSpeed
    {
        get
        {
            if (Time.time > slowUntil)
            {
                slowFactor = 1f;
                return moveSpeed;
            }

            return moveSpeed * slowFactor;
        }
    }

    /// <summary>지금 감속에 걸려 있는가. CurrentSpeed와 달리 상태를 바꾸지 않는다.</summary>
    public bool IsSlowed => Time.time <= slowUntil && slowFactor < 1f;

    /// <summary>속도에 factor를 곱한다. 이미 더 센 감속이 걸려 있으면 그쪽이 유지된다.</summary>
    public void ApplySlow(float factor, float duration)
    {
        factor = Mathf.Clamp(factor, 0.05f, 1f);

        // 만료됐으면 새로 시작하고, 살아있으면 더 강한 쪽을 남긴다.
        if (Time.time > slowUntil || factor < slowFactor) slowFactor = factor;

        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    protected virtual void Awake()
    {
        // OnEnable에서 체력을 계산하므로 반드시 그 전인 Awake에서 값을 받아둔다.
        ApplyDef();

        feedback = GetComponent<HitFeedback>();
        bodyCollider = GetComponent<Collider>();

        // 밀어내기 계산이 한 프레임에 몰리면 그 프레임만 튄다. 적마다 다른 프레임에 돌게 흩어놓는다.
        separationPhase = Random.Range(0, 1000);

        MeasureAimHeight();
    }

    /// <summary>몸통 렌더러의 한가운데 높이를 재둔다. 피격 연출이 스케일을 흔들기 전에 재야 값이 안정적이다.</summary>
    private void MeasureAimHeight()
    {
        Renderer body = GetComponentInChildren<Renderer>();
        if (body == null) return;

        aimHeight = body.bounds.center.y - transform.position.y;
    }

    private void ApplyDef()
    {
        if (def == null) return;

        maxHp = def.MaxHp;
        moveSpeed = def.MoveSpeed;
        xpReward = def.XpReward;

        contactDamage = def.ContactDamage;
        contactRadius = def.ContactRadius;
        contactInterval = def.ContactInterval;

        separationRadius = def.SeparationRadius;
        separationStrength = def.SeparationStrength;
    }

    protected virtual void OnEnable()
    {
        // 스폰 시점의 웨이브 배율을 적용한다.
        effectiveMaxHp = maxHp * HpMultiplier;
        hp = effectiveMaxHp;
        dying = false;
        nextContactTime = 0f;
        target = null;
        slowFactor = 1f;
        slowUntil = 0f;
        slowVisualOn = false;

        if (bodyCollider != null) bodyCollider.enabled = true;

        EnemyRegistry.Register(this);
        if (feedback != null) feedback.PlaySpawn();

        if (def != null) SfxManager.Play(def.SpawnSfx, transform.position);
    }

    protected virtual void OnDisable()
    {
        EnemyRegistry.Unregister(this);
    }

    protected virtual void Update()
    {
        if (!IsAlive) return;

        // 맵 밖으로 새어나간 적은 조용히 치운다. 남겨두면 마지막 웨이브가 영원히 안 끝난다.
        if (ArenaBounds.IsOutsideKillBounds(transform.position))
        {
            DespawnEscaped();
            return;
        }

        if (target == null)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || !player.IsAlive) return;
            target = player.transform;
        }

        Move(target.position);
        TickSeparation();
        TryContactDamage();
        UpdateSlowVisual();

        // 모든 판정이 3D 거리 기반이라 y가 어긋나면 총알이 영원히 빗나간다.
        Vector3 p = transform.position;
        if (p.y != 0f) transform.position = new Vector3(p.x, 0f, p.z);
    }

    /// <summary>감속 상태가 바뀔 때만 색을 갱신한다. 매 프레임 부르면 낭비다.</summary>
    private void UpdateSlowVisual()
    {
        bool slowed = IsSlowed;
        if (slowed == slowVisualOn) return;

        slowVisualOn = slowed;
        if (feedback != null) feedback.SetTint(slowTintColor, slowed ? slowTintStrength : 0f);
    }

    /// <summary>자식이 구현하는 이동. targetPosition은 플레이어 위치.</summary>
    protected abstract void Move(Vector3 targetPosition);

    /// <summary>XZ 평면에서 목표로 직진하는 기본 이동. 자식이 그대로 쓰면 된다.</summary>
    protected void MoveStraightTo(Vector3 targetPosition, float speedMultiplier = 1f)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        transform.position += direction * (CurrentSpeed * speedMultiplier * Time.deltaTime);
    }

    /// <summary>가까운 적들을 서로 밀어낸다. 없으면 전부 한 점에 겹쳐 한 마리처럼 보인다.</summary>
    /// <summary>밀어내기는 적 수의 제곱으로 비싸진다. 매 프레임이 아니라 몇 프레임에 한 번만 돌린다.</summary>
    private void TickSeparation()
    {
        if (separationInterval <= 1)
        {
            ApplySeparation();
            return;
        }

        if ((Time.frameCount + separationPhase) % separationInterval != 0) return;

        ApplySeparation();
    }

    private void ApplySeparation()
    {
        if (separationStrength <= 0f || separationRadius <= 0f) return;

        // 격자가 반경 안 후보만 걸러준다. 예전에는 살아있는 적 전체를 훑어서 적 수의 제곱으로 비싸졌다.
        EnemyRegistry.FindAllInRange(transform.position, separationRadius, separationBuffer);

        Vector3 self = transform.position;
        Vector3 push = Vector3.zero;

        for (int i = 0; i < separationBuffer.Count; i++)
        {
            EnemyBase other = separationBuffer[i];
            if (other == null || other == this) continue;

            Vector3 delta = self - other.transform.position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance < 0.0001f)
            {
                // 완전히 겹치면 방향이 없으므로 아무 방향으로나 흩어놓는다.
                Vector2 jitter = Random.insideUnitCircle.normalized;
                push += new Vector3(jitter.x, 0f, jitter.y);
                continue;
            }

            float distance = Mathf.Sqrt(sqrDistance);
            push += delta / distance * (1f - distance / separationRadius);
        }

        // 건너뛴 프레임이 있으므로 Time.deltaTime이 아니라 지난번 밀어낸 뒤로 흐른 시간을 쓴다.
        float step = lastSeparationTime > 0f ? Time.time - lastSeparationTime : Time.deltaTime;
        lastSeparationTime = Time.time;

        if (push.sqrMagnitude < 0.0001f) return;

        transform.position += push * (separationStrength * Mathf.Min(step, 0.1f));
    }

    private void TryContactDamage()
    {
        if (Time.time < nextContactTime) return;

        PlayerController player = PlayerController.Instance;
        if (player == null || !player.IsAlive) return;

        Vector3 delta = player.transform.position - transform.position;
        delta.y = 0f;

        if (delta.sqrMagnitude > contactRadius * contactRadius) return;

        nextContactTime = Time.time + contactInterval;
        player.TakeDamage(contactDamage, transform.position);
    }

    public virtual void TakeDamage(float amount, Vector3 hitFrom)
    {
        TakeDamage(amount, hitFrom, null);
    }

    /// <summary>source를 넘기면 그 포탑이 넣은 피해로 집계된다. 적의 자폭·충돌 피해는 null로 둔다.</summary>
    public virtual void TakeDamage(float amount, Vector3 hitFrom, TurretDef source)
    {
        if (!IsAlive) return;

        // 남은 체력보다 크게 때려도 실제로 깎인 만큼만 기록한다. 과잉 피해는 합계를 부풀린다.
        DamageStats.Add(source, Mathf.Min(amount, hp));

        hp -= amount;

        // 치명타일 때는 PlayHit을 부르지 않는다. PlayDeath가 흰색 깜빡부터 축소까지 한 번에 처리한다.
        if (hp <= 0f)
        {
            hp = 0f;
            Die();
            return;
        }

        if (feedback != null) feedback.PlayHit();
        if (def != null) SfxManager.Play(def.HitSfx, transform.position);
    }

    /// <summary>EnemyPool이 소환하면서 자기 풀을 알려준다. 비어 있으면 예전처럼 파괴한다.</summary>
    public void BindPool(SimplePool<EnemyBase> owner)
    {
        pool = owner;
    }

    /// <summary>풀로 돌려보낸다. 풀이 없으면(에디터에서 직접 놓은 적 등) 그냥 파괴한다.</summary>
    private void ReturnToPool()
    {
        if (pool != null) pool.Release(this);
        else Destroy(gameObject);
    }

    /// <summary>맵을 벗어나 사라지는 경우. 플레이어가 잡은 게 아니므로 경험치도 연출도 없다.</summary>
    private void DespawnEscaped()
    {
        dying = true;
        EnemyRegistry.Unregister(this);
        ReturnToPool();
    }

    protected virtual void Die()
    {
        dying = true;

        if (bodyCollider != null) bodyCollider.enabled = false;
        EnemyRegistry.Unregister(this);

        // 사망음은 이 오브젝트가 풀로 돌아간 뒤에도 계속 울려야 하므로 SfxManager가 대신 재생한다.
        if (def != null) SfxManager.Play(def.DeathSfx, transform.position);

        OnDeath();

        if (feedback != null) feedback.PlayDeath(ReturnToPool);
        else ReturnToPool();
    }

    /// <summary>사망 처리. 경험치를 바로 지급한다. 자식이 추가 처리를 붙이려면 오버라이드한다.</summary>
    protected virtual void OnDeath()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnEnemyKilled(xpReward);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, contactRadius);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
}
