// 레벨업 요구 XP 표. [0]이 레벨1->2에 필요한 XP.
// 표를 다 쓰면 마지막 값에서 AddStepAfterTable 만큼씩 커지는 덧셈으로 이어간다.

using UnityEngine;

[CreateAssetMenu(fileName = "LevelTable", menuName = "Game Data/Level Table")]
public class LevelTable : ScriptableObject
{
    [Tooltip("[0]=레벨1->2, [1]=레벨2->3 ... 에 필요한 XP. TSV에서 덮어쓴다.")]
    public int[] XpPerLevel = { 5, 7, 9, 12, 15, 19, 24, 30, 37, 45 };

    [Tooltip("표를 다 쓴 뒤부터 한 레벨마다 더해지는 양. 더하는 양 자체가 매 레벨 이만큼씩 커진다. " +
             "50이면 +50, +100, +150, +200 ... 식으로 불어난다. CSV에는 없고 여기서만 조절한다.")]
    [Min(0)] public int AddStepAfterTable = 50;

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 XP.</summary>
    public int GetRequirement(int level)
    {
        int index = Mathf.Max(0, level - 1);

        if (XpPerLevel == null || XpPerLevel.Length == 0)
            return Mathf.Max(1, Mathf.RoundToInt(5f * Mathf.Pow(1.3f, index)));

        if (index < XpPerLevel.Length) return Mathf.Max(1, XpPerLevel[index]);

        // 표 밖으로 몇 칸 나왔는지. 1, 2, 3 ...
        int extraSteps = index - (XpPerLevel.Length - 1);

        // 더하는 양이 매 레벨 커진다. +50, +100, +150 ... 을 다 더하면 50 x n(n+1)/2.
        long added = (long)AddStepAfterTable * extraSteps * (extraSteps + 1) / 2;
        long value = XpPerLevel[XpPerLevel.Length - 1] + added;

        if (value < 1) return 1;
        if (value > int.MaxValue) return int.MaxValue;

        return (int)value;
    }
}
