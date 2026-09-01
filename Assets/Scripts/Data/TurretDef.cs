// 포탑 한 종류의 데이터. 기본 스탯 / 강화 수치 / 레벨업 카드 표시가 전부 여기 모인다.
// 숫자와 문구는 TSV(엑셀)에서 덮어쓰고, Prefab 참조는 여기서 한 번만 연결한다.

using UnityEngine;

[CreateAssetMenu(fileName = "TurretDef", menuName = "Game Data/Turret Def")]
public class TurretDef : ScriptableObject
{
    [Header("식별 (TSV의 id 열과 일치해야 함)")]
    public string Id = "turret";

    [Header("프리팹 / 아이콘 — TSV 임포트가 건드리지 않는 오브젝트 참조")]
    public TurretBase Prefab;

    [Tooltip("레벨업 카드 좌측 상단에 띄울 아이콘. 어느 포탑 카드인지 한눈에 구분하려고 쓴다. " +
             "비워두면 아이콘 없이 그린다.")]
    public Sprite CardIcon;

    [Header("카드 표시")]
    public string DisplayName = "TURRET";
    public string Description = "Place one more turret";

    [Tooltip("이 포탑 관련 카드를 칠할 색. 포탑 머티리얼 색과 맞춰둔다.")]
    public Color CardColor = new Color(0.35f, 0.6f, 0.95f);

    [Header("기본 스탯 (TurretBase가 Awake에서 읽어간다)")]
    public float Range = 6f;
    public float FireInterval = 0.5f;
    public float Damage = 5f;

    [Header("사거리 원 색 (모양은 TurretCommonSettings에서)")]
    [Tooltip("사거리 원 테두리 색. 포탑 고유색과 맞춰둔다.")]
    public Color RangeColor = new Color(0.35f, 0.6f, 0.95f);

    [Tooltip("원 내부를 채우는 색. 알파가 곧 진하기이고, 알파가 0이면 아예 채우지 않는다. " +
             "항상 원을 보여주는 오라형 포탑만 쓴다.")]
    public Color RangeFillColor = new Color(0.4f, 0.81f, 0.39f, 0.1f);

    [Header("발사 반동 (포탑마다 손맛이 다르다)")]
    [Tooltip("발사 순간 앞뒤로 움츠러드는 정도. 클수록 세게 눌린다.")]
    [Min(0f)] public float RecoilStrength = 0.18f;

    [Min(0.01f)] public float RecoilDuration = 0.12f;

    [Header("이 포탑 전용 강화 수치")]
    public float DamageStep = 0.4f;
    public float FireRateStep = 0.4f;
    public float RangeStep = 0.5f;

    [Tooltip("필드에 동시에 존재할 수 있는 최대 개수. 누적 뽑기 횟수가 아니라 지금 살아있는 수를 센다. " +
             "이 수에 도달하면 NEW 카드가 안 나오고, 포탑이 사라지면 다시 나온다.")]
    [Min(1)] public int MaxCount = 6;

    [Tooltip("이 포탑에 쓸 수 있는 일반 강화(데미지/연사/사거리) 최대 횟수. 특수 강화는 여기 포함되지 않는다.")]
    [Min(1)] public int MaxUpgrades = 5;

    [Header("특수 강화")]
    [Tooltip("이 포탑 관련 강화를 몇 번 쌓아야 특수 강화가 확정 등장하는지. " +
             "카드 제목과 설명은 엑셀이 아니라 포탑 프리팹에서 관리한다.")]
    [Min(1)] public int SpecialThreshold = 3;

    [Header("효과음 — CSV가 아니라 여기서 직접 연결한다 (Prefab과 같은 규칙)")]
    [Tooltip("한 발 쏠 때마다 나는 소리. 초당 수십 번 울리므로 SfxDef의 MinInterval을 꼭 챙긴다. " +
             "오라 포탑처럼 계속 도는 것은 여기를 비우고 LoopSfx를 쓴다.")]
    public SfxDef FireSfx;

    [Tooltip("레벨업으로 이 포탑이 필드에 새로 나타날 때. 비우면 CommonSfx의 TurretSpawn을 쓴다.")]
    public SfxDef SpawnSfx;

    [Tooltip("포탑이 살아있는 동안 계속 도는 소리(오라 포탑용). SfxDef의 Loop를 켜야 한다. " +
             "이 소리만은 포탑 본인의 AudioSource가 재생한다.")]
    public SfxDef LoopSfx;
}
