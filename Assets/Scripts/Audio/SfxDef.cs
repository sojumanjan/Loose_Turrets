// 효과음 한 종류의 데이터. 클립 여러 개 · 볼륨/피치 랜덤 · 겹침 제어를 전부 여기서 정한다.
// 실제 재생은 SfxManager가 하고, 이 에셋은 "어떻게 들릴지"만 들고 있다.

using UnityEngine;

[CreateAssetMenu(fileName = "SfxDef", menuName = "Game Data/Sfx Def")]
public class SfxDef : ScriptableObject
{
    [Header("식별 (id로 찾아 쓰고 싶을 때만 채우면 된다)")]
    public string Id = "sfx";

    [Header("클립 — 여러 개 넣으면 재생할 때마다 랜덤으로 하나 고른다")]
    [Tooltip("비어 있으면 조용히 넘어간다. 사운드 파일을 아직 안 넣어도 게임은 그대로 돌아간다.")]
    public AudioClip[] Clips;

    [Header("볼륨")]
    [Range(0f, 1f)] public float Volume = 0.7f;

    [Tooltip("매번 볼륨에 더해지는 랜덤 폭. 0.05면 ±5%.")]
    [Range(0f, 0.5f)] public float VolumeJitter = 0.05f;

    [Header("피치")]
    [Range(0.1f, 3f)] public float Pitch = 1f;

    [Tooltip("매번 피치에 곱해지는 랜덤 폭. 같은 소리가 겹칠 때 나는 '삐-' 하는 위상 간섭을 없애준다. " +
             "0.08이면 ±8%. 발사음처럼 연달아 나는 소리는 반드시 0보다 크게 둔다.")]
    [Range(0f, 0.5f)] public float PitchJitter = 0.08f;

    [Header("겹침 제어 — 초당 수십 번 울리는 소리는 여기가 핵심")]
    [Tooltip("이 시간(초) 안에 다시 요청되면 버린다. 0.04면 초당 25개로 제한된다. " +
             "사람 귀는 초당 20개를 넘어가면 어차피 구분하지 못한다.")]
    [Min(0f)] public float MinInterval = 0.04f;

    [Tooltip("이 소리가 동시에 울릴 수 있는 최대 개수. 넘으면 버린다.")]
    [Min(1)] public int MaxConcurrent = 4;

    [Tooltip("겹칠수록 각각의 볼륨을 얼마나 줄일지. 0이면 안 줄여서 그대로 더해지고(찢어진다), 1이면 최대로 줄인다.")]
    [Range(0f, 1f)] public float CrowdFalloff = 0.6f;

    [Header("우선순위")]
    [Tooltip("켜면 빈 보이스가 없을 때 덜 중요한 소리를 밀어내고 재생한다. " +
             "플레이어 피격 · 레벨업 · 웨이브 시작처럼 놓치면 안 되는 소리에만 켠다.")]
    public bool Important;

    [Header("공간감")]
    [Tooltip("0이면 2D. 탑뷰 직교 카메라는 리스너가 하늘 높이 있어서 3D로 두면 전부 멀고 밋밋하게 들린다. 0 권장.")]
    [Range(0f, 1f)] public float SpatialBlend;

    [Tooltip("화면 좌우 위치에 따라 스테레오를 얼마나 벌릴지. 2D여도 방향감이 산다. 0이면 정중앙 고정.")]
    [Range(0f, 1f)] public float StereoPan = 0.6f;

    [Header("반복 재생 (오라 포탑처럼 계속 울리는 소리)")]
    [Tooltip("이런 소리는 오브젝트와 수명이 같아야 해서 풀로 관리하지 않는다. " +
             "오브젝트가 자기 AudioSource에 ApplyToLoopSource()로 이 설정을 씌워 쓴다.")]
    public bool Loop;

    public bool HasClip => Clips != null && Clips.Length > 0;

    /// <summary>인덱스로 클립을 꺼낸다. 범위를 벗어나면 null.</summary>
    public AudioClip GetClip(int index)
    {
        if (!HasClip) return null;
        if (index < 0 || index >= Clips.Length) return null;
        return Clips[index];
    }

    /// <summary>반복 재생용. 오브젝트가 직접 들고 있는 AudioSource에 이 설정을 씌운다.</summary>
    public void ApplyToLoopSource(AudioSource source)
    {
        if (source == null || !HasClip) return;

        source.clip = Clips[0];
        source.loop = true;
        source.playOnAwake = false;
        source.pitch = Pitch;
        source.spatialBlend = SpatialBlend;
        source.panStereo = 0f;

        // 볼륨은 페이드 인/아웃을 걸 수 있게 호출한 쪽이 정한다. 여기서는 기준값만 넣어둔다.
        source.volume = Volume * SfxManager.MasterVolume;
    }
}
