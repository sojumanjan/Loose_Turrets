// 모든 포탑이 똑같이 쓰는 값. 포탑마다 다를 이유가 없는 것만 여기 모은다.
// 에셋 하나만 만들어 네 프리팹이 같이 참조한다. 링 굵기 한 번 바꾸면 전 포탑에 반영된다.
//
// 여기 넣지 말아야 하는 것:
//   포탑마다 다른 값  -> TurretDef (SO). 예: 반동 세기, 사거리 원 색, 특수 강화 문구
//   그 포탑에만 있는 값 -> 프리팹.      예: 총알 프리팹, 레이저 색, 폭발 반경

using UnityEngine;

[CreateAssetMenu(fileName = "TurretCommonSettings", menuName = "Game Data/Turret Common Settings")]
public class TurretCommonSettings : ScriptableObject
{
    [Header("동작")]
    [Tooltip("사거리 안 최근접 적을 다시 찾는 주기(초). 짧으면 반응이 좋고 길면 가볍다.")]
    [Min(0.01f)] public float TargetRefreshInterval = 0.15f;

    [Tooltip("초당 회전 각도. 조준이 얼마나 빨리 따라붙는지.")]
    [Min(1f)] public float TurnSpeed = 720f;

    [Tooltip("에디터에서 사거리 기즈모를 그릴지.")]
    public bool DrawRangeGizmo = true;

    [Header("들었을 때 하이라이트")]
    [Tooltip("고유색에 곱하는 밝기 증가량. 0이면 변화 없음, 1이면 두 배로 밝아진다. " +
             "흰색을 섞지 않고 곱하므로 텍스처로 색을 내는 모델도 색조를 잃지 않고 밝아진다.")]
    [Range(0f, 1f)] public float HeldBrightness = 0.45f;

    [Header("사거리 원 (모양 · 색은 TurretDef에서)")]
    public Material RangeRingMaterial;

    [Min(0.001f)] public float RingWidth = 0.09f;

    [Tooltip("들고 있을 때 테두리 진하기.")]
    [Range(0f, 1f)] public float RingAlpha = 0.8f;

    [Tooltip("들지 않았는데도 원을 보여주는 포탑(오라형)이 쓸 흐린 투명도.")]
    [Range(0f, 1f)] public float RingIdleAlpha = 0.3f;

    [Tooltip("원을 몇 조각으로 그릴지. 클수록 매끄럽다.")]
    [Min(8)] public int RingSegments = 64;

    [Tooltip("바닥(y=0)보다 살짝 위에 그려야 파묻히지 않는다.")]
    public float RingHeight = 0.04f;

    [Header("반복 효과음")]
    [Tooltip("사거리 안에 적이 들어오고 나갈 때 소리가 붙었다 사라지는 데 걸리는 시간.")]
    [Min(0f)] public float LoopFadeDuration = 0.25f;
}
