// 플레이어. WASD로 XZ 평면을 이동하고 HP / 피격 / 사망을 처리한다. 직접 공격은 하지 않는다.

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, IDamageable
{
    /// <summary>적이 추적 대상을 찾을 때 쓰는 전역 참조. 토이 프로젝트라 싱글턴으로 단순화.</summary>
    public static PlayerController Instance { get; private set; }

    [Header("이동")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 45f;

    [Header("생존 — 체력은 칸 단위다. 적의 공격력과 무관하게 무조건 한 칸씩 닳는다")]
    [Tooltip("최대 체력 칸 수. 이만큼 맞으면 죽는다.")]
    [Min(1)] [SerializeField] private int maxHearts = 4;

    [Tooltip("한 대 맞은 뒤 무적으로 버티는 시간. 한 번에 여러 대 겹쳐 맞는 것을 막는다.")]
    [SerializeField] private float invincibleDuration = 1f;

    [Header("자동 회복")]
    [Tooltip("마지막으로 맞고 나서 이 시간이 지나야 회복이 시작된다.")]
    [Min(0f)] [SerializeField] private float regenDelay = 15f;

    [Tooltip("회복이 시작된 뒤 한 칸 차는 데 걸리는 시간.")]
    [Min(0.05f)] [SerializeField] private float regenInterval = 1f;

    private HitFeedback feedback;
    private Vector3 velocity;
    private int hearts;
    private float invincibleUntil;
    private float nextRegenTime;
    private bool dead;

#if UNITY_EDITOR
    /// <summary>디버그 무적(에디터 전용). 켜져 있으면 데미지를 아예 받지 않는다. F2가 켜고 끈다.</summary>
    public bool Invincible { get; private set; }

    public void SetInvincible(bool on) => Invincible = on;
#endif

    public bool IsAlive => !dead;
    public Transform Transform => transform;

    /// <summary>남은 체력 칸 수.</summary>
    public float Hp => hearts;
    public float MaxHp => maxHearts;
    public float HpRatio => maxHearts <= 0 ? 0f : Mathf.Clamp01((float)hearts / maxHearts);

    private void Awake()
    {
        Instance = this;
        feedback = GetComponent<HitFeedback>();
        hearts = maxHearts;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (dead) return;

        TickRegen();

        Move();
    }

    private void Move()
    {
        Vector3 desiredVelocity = ReadMoveInput() * moveSpeed;
        velocity = Vector3.MoveTowards(velocity, desiredVelocity, acceleration * Time.deltaTime);

        Vector3 next = transform.position + velocity * Time.deltaTime;
        // 아레나 크기는 ArenaBounds 한 곳에서만 정한다.
        Vector2 bounds = ArenaBounds.HalfSize;
        next.x = Mathf.Clamp(next.x, -bounds.x, bounds.x);
        next.z = Mathf.Clamp(next.z, -bounds.y, bounds.y);
        next.y = 0f;

        transform.position = next;
    }

    private Vector3 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector3.zero;

        float x = 0f;
        float z = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;

        Vector3 direction = new Vector3(x, 0f, z);
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    /// <summary>amount는 무시한다. 어떤 적에게 맞든 한 칸이다.</summary>
    public void TakeDamage(float amount, Vector3 hitFrom)
    {
#if UNITY_EDITOR
        if (Invincible) return;
#endif
        if (dead || Time.time < invincibleUntil) return;

        hearts--;
        invincibleUntil = Time.time + invincibleDuration;

        // 맞을 때마다 회복 대기가 처음부터 다시 시작된다.
        nextRegenTime = Time.time + regenDelay;

        // 적과 동일하게, 치명타면 PlayDeath가 피격 깜빡까지 같이 처리한다.
        if (hearts <= 0)
        {
            hearts = 0;
            Die();
            return;
        }

        if (feedback != null) feedback.PlayHit();
        SfxManager.Play(SfxManager.Common?.PlayerHit, transform.position);
    }

    // ---- 레벨업 업그레이드에서 호출한다 ----

    public void AddMoveSpeed(float amount)
    {
        moveSpeed += amount;
    }

    /// <summary>맞고 나서 한동안 안 맞으면 한 칸씩 저절로 찬다.
    /// 어쩌다 한 대 맞은 것으로 판이 끝나지 않게 해주되, 몰린 상황에서는 회복이 따라오지 못한다.</summary>
    private void TickRegen()
    {
        if (dead || hearts >= maxHearts) return;
        if (Time.time < nextRegenTime) return;

        hearts++;
        nextRegenTime = Time.time + regenInterval;
    }

    private void Die()
    {
        dead = true;
        velocity = Vector3.zero;

        if (feedback != null) feedback.PlayDeath(null);
        SfxManager.Play(SfxManager.Common?.PlayerDeath, transform.position);

        // 실제 게임오버 처리는 GameManager가 IsAlive 를 보고 한다. 이 로그는 확인용이라 빌드에서는 뺀다.
#if UNITY_EDITOR
        Debug.Log("[Player] 사망");
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Vector2 bounds = ArenaBounds.HalfSize;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.x * 2f, 0.1f, bounds.y * 2f));
    }
}
