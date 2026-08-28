// 포탑·적에 딸리지 않는 공용 효과음 모음. 게임 흐름 / 플레이어 / UI 소리를 한 에셋에 모아둔다.
// SfxManager에 하나만 연결하면 어디서든 SfxManager.Common 으로 꺼내 쓸 수 있다.

using UnityEngine;

[CreateAssetMenu(fileName = "CommonSfx", menuName = "Game Data/Common Sfx")]
public class CommonSfx : ScriptableObject
{
    [Header("플레이어")]
    public SfxDef PlayerHit;
    public SfxDef PlayerDeath;

    [Header("게임 흐름")]
    public SfxDef WaveStart;
    public SfxDef StageClear;
    public SfxDef GameOver;
    public SfxDef LevelUp;

    [Tooltip("다음 웨이브 스폰 구역이 깜빡일 때 나는 경고음.")]
    public SfxDef ZoneWarning;

    [Header("UI / 조작")]
    [Tooltip("레벨업 카드를 골랐을 때.")]
    public SfxDef CardSelect;

    [Tooltip("메뉴 버튼을 눌렀을 때.")]
    public SfxDef ButtonClick;

    [Tooltip("포탑을 마우스로 집었을 때.")]
    public SfxDef TurretPickUp;

    [Tooltip("포탑을 내려놨을 때.")]
    public SfxDef TurretDrop;

    [Tooltip("레벨업으로 새 포탑이 필드에 나타났을 때. TurretDef의 SpawnSfx가 비어 있을 때만 쓰인다.")]
    public SfxDef TurretSpawn;
}
