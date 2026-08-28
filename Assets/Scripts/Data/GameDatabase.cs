// 모든 게임 데이터의 입구. 씬에서는 이 에셋 하나만 참조하면 되고, TSV 임포터도 여기서 id로 찾아 들어간다.

using UnityEngine;

[CreateAssetMenu(fileName = "GameDatabase", menuName = "Game Data/Game Database")]
public class GameDatabase : ScriptableObject
{
    [Header("밸런스 시트 기준값 (게임에는 영향 없음 · CSV 수식의 기준)")]
    [Tooltip("사거리 계수 = range / 이 값")]
    public float RangeBaseline = 4f;

    [Tooltip("속도 계수 = moveSpeed / 이 값")]
    public float SpeedBaseline = 2.5f;

    [Header("데이터")]
    public EnemyDef[] Enemies;
    public TurretDef[] Turrets;
    public WaveTable Waves;
    public LevelTable Levels;

    public EnemyDef FindEnemy(string id)
    {
        if (Enemies == null || string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < Enemies.Length; i++)
        {
            if (Enemies[i] != null && Enemies[i].Id == id) return Enemies[i];
        }

        return null;
    }

    public TurretDef FindTurret(string id)
    {
        if (Turrets == null || string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < Turrets.Length; i++)
        {
            if (Turrets[i] != null && Turrets[i].Id == id) return Turrets[i];
        }

        return null;
    }
}
