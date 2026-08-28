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

    [Header("생존")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float invincibleDuration = 0.4f;

    private HitFeedback feedback;
    private Vector3 velocity;
    private float hp;
    private float invincibleUntil;
    private bool dead;

    /// <summary>디버그 무적. 켜져 있으면 데미지를 아예 받지 않는다.</summary>
    public bool Invincible { get; private set; }

    public void SetInvincible(bool on) => Invincible = on;

    public bool IsAlive => !dead;
    public Transform Transform => transform;
    public float Hp => hp;
    public float MaxHp => maxHp;
    public float HpRatio => maxHp <= 0f ? 0f : Mathf.Clamp01(hp / maxHp);

    private void Awake()
    {
        Instance = this;
        feedback = GetComponent<HitFeedback>();
        hp = maxHp;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (dead) return;

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

    public void TakeDamage(float amount, Vector3 hitFrom)
    {
        if (dead || Invincible || Time.time < invincibleUntil) return;

        hp -= amount;
        invincibleUntil = Time.time + invincibleDuration;

        // 적과 동일하게, 치명타면 PlayDeath가 피격 깜빡까지 같이 처리한다.
        if (hp <= 0f)
        {
            hp = 0f;
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

    public void AddMaxHp(float amount)
    {
        maxHp += amount;
        Heal(amount);
    }

    public void Heal(float amount)
    {
        if (dead) return;
        hp = Mathf.Min(maxHp, hp + amount);
    }

    private void Die()
    {
        dead = true;
        velocity = Vector3.zero;

        if (feedback != null) feedback.PlayDeath(null);
        SfxManager.Play(SfxManager.Common?.PlayerDeath, transform.position);

        // 7단계에서 GameManager의 게임오버 처리로 교체한다.
        Debug.Log("[Player] 사망");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Vector2 bounds = ArenaBounds.HalfSize;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.x * 2f, 0.1f, bounds.y * 2f));
    }
}
