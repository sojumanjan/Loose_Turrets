// 적 프리팹별로 풀을 하나씩 들고 있는 전역 접근점. 스포너가 프리팹을 넘겨 소환한다.
//
// 후반 웨이브는 초당 수십 마리가 나고 죽는다. 그때마다 Instantiate / Destroy 하면
// 컴포넌트 초기화(GetComponentsInChildren, MaterialPropertyBlock 할당)와 GC 비용이 그대로 프레임에 얹힌다.
// WebGL은 싱글 스레드라 이게 특히 아프다. 총알이 쓰던 SimplePool을 적에게도 그대로 쓴다.
//
// EnemyBase는 OnEnable에서 체력과 상태를 전부 초기화하고 OnDisable에서 등록을 해제하므로,
// 껐다 켜는 것만으로 재사용에 필요한 정리가 끝난다.

using System.Collections.Generic;
using UnityEngine;

public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    [Header("미리 만들어둘 적 (없어도 첫 소환 때 자동 생성됨)")]
    [SerializeField] private EnemyBase[] prewarmPrefabs;

    [Tooltip("프리팹마다 미리 만들어 둘 개수. 0이면 미리 만들지 않고 필요할 때마다 늘어난다.")]
    [Min(0)] [SerializeField] private int prewarmCount = 40;

    private readonly Dictionary<EnemyBase, SimplePool<EnemyBase>> pools =
        new Dictionary<EnemyBase, SimplePool<EnemyBase>>();

    private Transform holder;

    private void Awake()
    {
        Instance = this;

        holder = new GameObject("Enemies").transform;
        holder.SetParent(transform);

        if (prewarmPrefabs == null || prewarmCount <= 0) return;

        // 부모가 꺼져 있으면 자식의 OnEnable이 돌지 않는다.
        // 이 순서가 아니면 미리 만드는 동안 적이 등록소에 들어가고 스폰 효과음이 우수수 난다.
        holder.gameObject.SetActive(false);

        for (int i = 0; i < prewarmPrefabs.Length; i++)
        {
            if (prewarmPrefabs[i] != null) GetPool(prewarmPrefabs[i], prewarmCount);
        }

        holder.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private SimplePool<EnemyBase> GetPool(EnemyBase prefab, int prewarm = 0)
    {
        SimplePool<EnemyBase> pool;
        if (pools.TryGetValue(prefab, out pool)) return pool;

        pool = new SimplePool<EnemyBase>(prefab, prewarm, holder);
        pools.Add(prefab, pool);
        return pool;
    }

    /// <summary>적 하나를 소환한다. 해당 프리팹의 풀이 없으면 그 자리에서 만든다.</summary>
    public EnemyBase Spawn(EnemyBase prefab, Vector3 position)
    {
        if (prefab == null) return null;

        SimplePool<EnemyBase> pool = GetPool(prefab);

        EnemyBase enemy = pool.Get(position, Quaternion.identity);
        enemy.BindPool(pool);

        return enemy;
    }
}
