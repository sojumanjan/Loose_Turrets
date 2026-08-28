// 살아있는 적을 static 리스트로 들고 있는 등록소. 포탑이 매 프레임 FindObjectsByType을 도는 것을 막기 위한 것.

using System.Collections.Generic;
using UnityEngine;

public static class EnemyRegistry
{
    private static readonly List<EnemyBase> alive = new List<EnemyBase>(128);

    public static IReadOnlyList<EnemyBase> Alive => alive;
    public static int Count => alive.Count;

    public static void Register(EnemyBase enemy)
    {
        if (enemy != null && !alive.Contains(enemy)) alive.Add(enemy);
    }

    public static void Unregister(EnemyBase enemy)
    {
        alive.Remove(enemy);
    }

    /// <summary>static은 씬을 다시 로드해도 살아남으므로 게임 재시작 시 반드시 비워야 한다.</summary>
    public static void Clear()
    {
        alive.Clear();
    }

    /// <summary>
    /// from 기준 range 안에서 가장 가까운 살아있는 적. 없으면 null.
    /// 높이(y)는 무시하고 XZ 평면 거리로만 잰다. 들어올린 포탑이나 공중을 나는 총알도
    /// 지상의 적을 정상적으로 맞히게 하기 위한 것이며, 탑뷰라 y 차이는 게임적으로 의미가 없다.
    /// </summary>
    public static EnemyBase FindNearest(Vector3 from, float range)
    {
        EnemyBase nearest = null;
        float bestSqrDistance = range * range;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase enemy = alive[i];

            if (enemy == null)
            {
                alive.RemoveAt(i);
                continue;
            }
            if (!enemy.IsAlive) continue;

            Vector3 delta = enemy.transform.position - from;
            delta.y = 0f;

            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    /// <summary>
    /// FindNearest와 같지만 exclude에 들어있는 적은 건너뛴다. 연쇄 공격이 같은 적을 다시 때리지 않게 하는 용도.
    /// </summary>
    public static EnemyBase FindNearestExcluding(Vector3 from, float range, List<EnemyBase> exclude)
    {
        EnemyBase nearest = null;
        float bestSqrDistance = range * range;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase enemy = alive[i];

            if (enemy == null)
            {
                alive.RemoveAt(i);
                continue;
            }
            if (!enemy.IsAlive) continue;
            if (exclude != null && exclude.Contains(enemy)) continue;

            Vector3 delta = enemy.transform.position - from;
            delta.y = 0f;

            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    /// <summary>range 안의 살아있는 적을 전부 results에 담는다. 관통탄의 광역 판정용.</summary>
    public static void FindAllInRange(Vector3 from, float range, List<EnemyBase> results)
    {
        results.Clear();
        float sqrRange = range * range;

        for (int i = alive.Count - 1; i >= 0; i--)
        {
            EnemyBase enemy = alive[i];

            if (enemy == null)
            {
                alive.RemoveAt(i);
                continue;
            }
            if (!enemy.IsAlive) continue;

            Vector3 delta = enemy.transform.position - from;
            delta.y = 0f;

            if (delta.sqrMagnitude <= sqrRange) results.Add(enemy);
        }
    }
}
