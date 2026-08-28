// 포탑이 쏘는 탄. 직진하며 적을 때리고 풀로 돌아간다. 콜라이더 없이 거리 검사로 판정한다.
// 관통 모드를 켜면 사라지지 않고 반경 안의 모든 적을 한 번씩 때리며 계속 날아간다. (대포 포탄용)

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("탄도")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float hitRadius = 0.55f;
    [SerializeField] private float lifeTime = 2.5f;

    [Header("관통 (대포용)")]
    [Tooltip("켜면 적을 맞혀도 사라지지 않고 관통한다. 같은 적을 두 번 때리지는 않는다.")]
    [SerializeField] private bool piercing = false;

    [Header("효과음")]
    [Tooltip("적에게 맞았을 때. 발사음만큼 자주 울리므로 볼륨을 낮게, MinInterval을 넉넉히 잡는다. " +
             "비워두면 명중음 없이 발사음만 난다.")]
    [SerializeField] private SfxDef impactSfx;

    [Header("연출")]
    [SerializeField] private float spawnStretchDuration = 0.12f;
    [Tooltip("전진축 기준 초당 회전 각도. 큰 포탄에 무게감을 준다. 0이면 회전 없음.")]
    [SerializeField] private float spinSpeed = 0f;

    // 프레임마다 새 리스트를 만들지 않도록 공용 버퍼를 쓴다.
    private static readonly List<EnemyBase> hitBuffer = new List<EnemyBase>(64);

    private readonly List<EnemyBase> alreadyHit = new List<EnemyBase>(16);
    private Vector3 baseScale;
    private Vector3 direction;
    private float damage;
    private float despawnTime;
    private SimplePool<Bullet> owner;
    private Tween scaleTween;
    private bool consumed;

    public bool IsPiercing => piercing;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    /// <summary>풀에서 꺼낸 직후 호출한다. 방향/데미지/돌아갈 풀을 세팅하고 발사한다.</summary>
    public void Launch(Vector3 dir, float damageAmount, SimplePool<Bullet> pool)
    {
        dir.y = 0f;
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;

        damage = damageAmount;
        owner = pool;
        consumed = false;
        despawnTime = Time.time + lifeTime;
        alreadyHit.Clear();

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        scaleTween?.Kill();
        transform.localScale = new Vector3(baseScale.x * 0.5f, baseScale.y * 0.5f, baseScale.z * 1.6f);
        scaleTween = transform.DOScale(baseScale, spawnStretchDuration).SetEase(Ease.OutQuad);
    }

    private void Update()
    {
        if (consumed) return;

        transform.position += direction * (speed * Time.deltaTime);

        if (spinSpeed != 0f)
            transform.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.Self);

        if (piercing) ApplyPiercingHits();
        else if (ApplySingleHit()) return;

        if (Time.time >= despawnTime) Despawn();
    }

    /// <summary>맞은 적이 있으면 데미지를 주고 소멸시킨다. 소멸했으면 true.</summary>
    private bool ApplySingleHit()
    {
        EnemyBase hit = EnemyRegistry.FindNearest(transform.position, hitRadius);
        if (hit == null) return false;

        SfxManager.Play(impactSfx, transform.position);
        hit.TakeDamage(damage, transform.position);
        Despawn();
        return true;
    }

    private void ApplyPiercingHits()
    {
        EnemyRegistry.FindAllInRange(transform.position, hitRadius, hitBuffer);

        for (int i = 0; i < hitBuffer.Count; i++)
        {
            EnemyBase enemy = hitBuffer[i];
            if (alreadyHit.Contains(enemy)) continue;

            alreadyHit.Add(enemy);

            // 관통탄은 한 번에 여러 마리를 때린다. 겹침은 SfxDef의 MinInterval이 걸러준다.
            SfxManager.Play(impactSfx, transform.position);
            enemy.TakeDamage(damage, transform.position);
        }
    }

    private void Despawn()
    {
        consumed = true;

        scaleTween?.Kill();
        transform.localScale = baseScale;
        alreadyHit.Clear();

        if (owner != null) owner.Release(this);
        else gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}
