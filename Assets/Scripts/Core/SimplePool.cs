// 프리팹 하나를 재사용하는 초경량 오브젝트 풀. 총알처럼 초당 수십 개가 켜졌다 꺼지는 것에 쓴다.

using System.Collections.Generic;
using UnityEngine;

public class SimplePool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Queue<T> idle = new Queue<T>();

    public int IdleCount => idle.Count;

    public SimplePool(T prefab, int prewarmCount = 0, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < prewarmCount; i++)
        {
            T instance = Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            idle.Enqueue(instance);
        }
    }

    /// <summary>풀에서 하나 꺼내 활성화한다. 남은 게 없으면 새로 만든다.</summary>
    public T Get(Vector3 position, Quaternion rotation)
    {
        T instance = null;

        // 씬 전환 등으로 이미 파괴된 오브젝트가 큐에 남아있을 수 있으니 살아있는 게 나올 때까지 뺀다.
        while (idle.Count > 0 && instance == null)
            instance = idle.Dequeue();

        if (instance == null)
            instance = Object.Instantiate(prefab, parent);

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        return instance;
    }

    /// <summary>다 쓴 오브젝트를 비활성화해 풀로 되돌린다.</summary>
    public void Release(T instance)
    {
        if (instance == null) return;

        instance.gameObject.SetActive(false);
        idle.Enqueue(instance);
    }
}
