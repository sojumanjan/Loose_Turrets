// 탄 프리팹별로 풀을 하나씩 들고 있는 전역 접근점. 포탑이 자기 탄 프리팹을 넘겨 발사한다.

using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance { get; private set; }

    [Header("미리 만들어둘 탄 (없어도 첫 발사 때 자동 생성됨)")]
    [SerializeField] private Bullet[] prewarmPrefabs;
    [SerializeField] private int prewarmCount = 48;

    private readonly Dictionary<Bullet, SimplePool<Bullet>> pools = new Dictionary<Bullet, SimplePool<Bullet>>();
    private Transform holder;

    private void Awake()
    {
        Instance = this;

        holder = new GameObject("Bullets").transform;
        holder.SetParent(transform);

        if (prewarmPrefabs == null) return;

        for (int i = 0; i < prewarmPrefabs.Length; i++)
        {
            if (prewarmPrefabs[i] != null) GetPool(prewarmPrefabs[i], prewarmCount);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private SimplePool<Bullet> GetPool(Bullet prefab, int prewarm = 0)
    {
        SimplePool<Bullet> pool;
        if (pools.TryGetValue(prefab, out pool)) return pool;

        pool = new SimplePool<Bullet>(prefab, prewarm, holder);
        pools.Add(prefab, pool);
        return pool;
    }

    /// <summary>탄 한 발을 발사한다. 해당 프리팹의 풀이 없으면 그 자리에서 만든다.</summary>
    public void Fire(Bullet prefab, Vector3 position, Vector3 direction, float damage,
                     TurretDef source = null, float scale = 1f, int pierceTargets = 0)
    {
        if (prefab == null) return;

        Bullet bullet = GetPool(prefab).Get(position, Quaternion.identity);
        bullet.Launch(direction, damage, GetPool(prefab), source, scale, pierceTargets);
    }
}
