// 레벨업 요구 XP 표. [0]이 레벨1->2에 필요한 XP.
// 표를 다 쓰면 마지막 값에서 GrowthAfterTable 배율로 이어간다.

using UnityEngine;

[CreateAssetMenu(fileName = "LevelTable", menuName = "Game Data/Level Table")]
public class LevelTable : ScriptableObject
{
    [Tooltip("[0]=레벨1->2, [1]=레벨2->3 ... 에 필요한 XP. TSV에서 덮어쓴다.")]
    public int[] XpPerLevel = { 5, 7, 9, 12, 15, 19, 24, 30, 37, 45 };

    [Tooltip("표를 다 쓴 뒤부터 한 레벨마다 곱해질 배율. TSV에는 없고 여기서만 조절한다.")]
    [Min(1.01f)] public float GrowthAfterTable = 1.25f;

    /// <summary>해당 레벨에서 다음 레벨까지 필요한 XP.</summary>
    public int GetRequirement(int level)
    {
        int index = Mathf.Max(0, level - 1);

        if (XpPerLevel == null || XpPerLevel.Length == 0)
            return Mathf.Max(1, Mathf.RoundToInt(5f * Mathf.Pow(1.3f, index)));

        if (index < XpPerLevel.Length) return Mathf.Max(1, XpPerLevel[index]);

        int extraSteps = index - (XpPerLevel.Length - 1);
        float value = XpPerLevel[XpPerLevel.Length - 1] * Mathf.Pow(Mathf.Max(1.01f, GrowthAfterTable), extraSteps);
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }
}
