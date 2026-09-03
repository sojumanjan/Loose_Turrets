// 레벨업 요구 XP 표. [0]이 레벨1->2에 필요한 XP.
// 표를 다 쓰면 마지막 값에서 이어가는데, 요구치에 일정량을 더하는 게 아니라
// "더하는 양" 자체가 매 레벨 커진다. 220 -> 240(+20) -> 280(+40) -> 340(+60) 식이다.

using UnityEngine;

[CreateAssetMenu(fileName = "LevelTable", menuName = "Game Data/Level Table")]
public class LevelTable : ScriptableObject
{
    [Tooltip("[0]=레벨1->2, [1]=레벨2->3 ... 에 필요한 XP. TSV에서 덮어쓴다.")]
    public int[] XpPerLevel = { 5, 7, 9, 12, 15, 19, 24, 30, 37, 45 };

    [Header("표를 다 쓴 뒤 (CSV에는 없고 여기서만 조절한다)")]
    [Tooltip("요구치가 StepThreshold 보다 작을 때, 한 레벨마다 '더하는 양'이 이만큼씩 커진다. "
             + "20이면 증가분이 20, 40, 60 ... 으로 늘어난다.")]
    [Min(0)] public int EarlyStep = 20;

    [Tooltip("증가분이 커지는 속도가 LateStep 으로 넘어가는 기준 요구치.")]
    [Min(1)] public int StepThreshold = 500;

    [Tooltip("요구치가 StepThreshold 이상일 때, 한 레벨마다 '더하는 양'이 이만큼씩 커진다. "
             + "EarlyStep 과 같은 값을 넣으면 끝까지 한 기울기로 간다.")]
    [Min(0)] public int LateStep = 30;

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 XP.</summary>
    public int GetRequirement(int level)
    {
        int index = Mathf.Max(0, level - 1);

        if (XpPerLevel == null || XpPerLevel.Length == 0)
            return Mathf.Max(1, Mathf.RoundToInt(5f * Mathf.Pow(1.3f, index)));

        if (index < XpPerLevel.Length) return Mathf.Max(1, XpPerLevel[index]);

        // 표 밖으로 몇 칸 나왔는지. 1, 2, 3 ...
        int extraSteps = index - (XpPerLevel.Length - 1);

        // 더하는 양이 매 레벨 커지고, 얼마나 커질지는 지금 요구치가 기준 위냐 아래냐로 갈린다.
        // 한 번에 계산할 공식이 없으므로 한 레벨씩 밟아 올라간다.
        long value = XpPerLevel[XpPerLevel.Length - 1];
        long step = 0;

        for (int i = 0; i < extraSteps; i++)
        {
            step += GrowthFor(value);
            value += step;

            if (value > int.MaxValue) return int.MaxValue;
        }

        if (value < 1) return 1;
        return (int)value;
    }

    /// <summary>이 요구치에서 다음 레벨로 갈 때, 더하는 양이 얼마나 커질지.</summary>
    private int GrowthFor(long current)
    {
        return current < StepThreshold ? EarlyStep : LateStep;
    }
}
