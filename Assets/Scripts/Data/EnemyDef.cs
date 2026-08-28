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

    [Header("효과음 — CSV가 아니라 여기서 직접 연결한다 (Prefab과 같은 규칙)")]
    [Tooltip("맞았지만 죽지는 않았을 때. 아주 자주 울리므로 볼륨을 낮게 잡는다.")]
    public SfxDef HitSfx;

    [Tooltip("죽을 때. 무한 모드에서는 초당 수십 마리가 죽으므로 MinInterval을 꼭 챙긴다.")]
    public SfxDef DeathSfx;

    [Tooltip("스폰될 때. 초당 20마리씩 쏟아지면 시끄러우므로 보통은 비워둔다.")]
    public SfxDef SpawnSfx;
}
