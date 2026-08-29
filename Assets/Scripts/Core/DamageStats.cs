// 포탑 종류별로 실제로 넣은 피해를 누적한다. 결과 화면에서 어느 포탑이 활약했는지 보여주고 밸런싱 근거로 쓴다.
// 과잉 피해는 세지 않는다. 체력 10 남은 적에게 30을 넣어도 10만 쌓인다.

using System.Collections.Generic;

public static class DamageStats
{
    private static readonly Dictionary<TurretDef, float> totals = new Dictionary<TurretDef, float>();

    // 딕셔너리 순회 순서는 보장되지 않으므로, 처음 피해를 넣은 순서를 따로 들고 있는다.
    private static readonly List<TurretDef> order = new List<TurretDef>(8);

    /// <summary>모든 포탑이 넣은 피해의 합.</summary>
    public static float Total { get; private set; }

    // static은 씬을 다시 로드해도 남는다. 새 판을 시작할 때 반드시 비운다.
    public static void Clear()
    {
        totals.Clear();
        order.Clear();
        Total = 0f;
    }

    /// <summary>실제로 깎인 체력만 넘겨야 한다. 과잉 피해를 그대로 넘기면 합계가 부풀려진다.</summary>
    public static void Add(TurretDef source, float applied)
    {
        if (source == null || applied <= 0f) return;

        float current;
        if (totals.TryGetValue(source, out current))
        {
            totals[source] = current + applied;
        }
        else
        {
            totals.Add(source, applied);
            order.Add(source);
        }

        Total += applied;
    }

    public static float Get(TurretDef source)
    {
        float value;
        return source != null && totals.TryGetValue(source, out value) ? value : 0f;
    }

    /// <summary>피해가 많은 순서로 채워 넣는다. 결과 화면이 순위표로 쓴다.</summary>
    public static void FillRanking(List<TurretDef> buffer)
    {
        buffer.Clear();
        buffer.AddRange(order);

        // 포탑 종류는 많아야 네댓 개라 단순 삽입 정렬로 충분하다.
        for (int i = 1; i < buffer.Count; i++)
        {
            TurretDef key = buffer[i];
            float keyValue = Get(key);

            int j = i - 1;
            while (j >= 0 && Get(buffer[j]) < keyValue)
            {
                buffer[j + 1] = buffer[j];
                j--;
            }

            buffer[j + 1] = key;
        }
    }
}
