// 적 한 종류의 데이터. 숫자는 TSV(엑셀)에서 덮어쓰고, Prefab 참조는 여기서 한 번만 연결한다.
// 임포터는 Prefab 필드를 절대 건드리지 않는다.

using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDef", menuName = "Game Data/Enemy Def")]
public class EnemyDef : ScriptableObject
{
    [Header("식별 (TSV의 id 열과 일치해야 함)")]
    public string Id = "enemy";

    [Header("프리팹 — TSV 임포트가 건드리지 않는 유일한 필드")]
    public EnemyBase Prefab;

    [Header("스탯 (TSV에서 덮어씀)")]
    public float MaxHp = 10f;
    public float MoveSpeed = 2.5f;
    public int XpReward = 1;

    [Header("플레이어 접촉 데미지")]
    public float ContactDamage = 10f;
    public float ContactRadius = 0.9f;
    public float ContactInterval = 0.8f;

    [Header("적끼리 밀어내기")]
    public float SeparationRadius = 0.85f;
    public float SeparationStrength = 4f;
}
