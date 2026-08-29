// 효과음 재생을 한 곳에서 관리한다. AudioSource를 풀로 돌려쓰고, 최소 간격 · 동시 개수 · 우선순위로 겹침을 걸러낸다.
// 포탑과 적은 SfxManager.Play(def, position) 한 줄만 부르면 된다. 매니저가 없거나 클립이 없으면 조용히 넘어간다.

using System.Collections.Generic;
using UnityEngine;

public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("보이스 풀")]
    [Tooltip("동시에 울릴 수 있는 소리의 총 개수. Unity 기본 Real Voices가 32라 그 아래로 두는 게 안전하다.")]
    [SerializeField, Range(4, 32)] private int voiceCount = 20;

    [Header("전체 볼륨 (설정 UI 값이 여기에 곱해진다)")]
    [Tooltip("프로젝트 기준값. 플레이어가 설정에서 고른 값은 SoundSettings 가 따로 들고 있다.")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    [Header("공용 효과음 (게임 흐름 · 플레이어 · UI)")]
    [Tooltip("포탑/적에 딸리지 않는 소리를 모아둔 에셋. 여기 하나만 연결하면 전부 이어진다.")]
    [SerializeField] private CommonSfx common;

    [Header("id로 찾아 쓸 효과음 목록")]
    [Tooltip("여기 넣어둔 SfxDef는 SfxManager.Play(id, pos) 로 부를 수 있다. " +
             "TurretDef/EnemyDef처럼 직접 참조해서 쓰는 것들은 굳이 넣지 않아도 된다.")]
    [SerializeField] private SfxDef[] bank;

    [Header("디버그")]
    [Tooltip("클립이 안 꽂힌 SfxDef를 재생하려 하면 경고를 한 번씩 띄운다. 사운드 파일 채워넣는 동안 켜두면 편하다.")]
    [SerializeField] private bool warnOnMissingClip;

    private AudioSource[] voices;
    private SfxDef[] voiceDef;      // 각 보이스가 지금 재생 중인 효과음
    private float[] voiceStart;     // 언제 시작했는지. 밀어낼 때 가장 오래된 것을 고른다.

    private readonly Dictionary<SfxDef, float> lastPlayTime = new Dictionary<SfxDef, float>();
    private readonly Dictionary<SfxDef, int> lastClipIndex = new Dictionary<SfxDef, int>();
    private readonly Dictionary<string, SfxDef> byId = new Dictionary<string, SfxDef>();
    private readonly HashSet<SfxDef> warned = new HashSet<SfxDef>();

    private Camera cam;

    /// <summary>반복 재생 소리도 이 값을 곱해 쓰라고 열어둔다.
    /// 인스펙터의 기준값에 설정 UI에서 고른 전체·효과음 볼륨을 곱한 최종 배율이다.</summary>
    public static float MasterVolume =>
        (Instance != null ? Instance.masterVolume : 1f) * SoundSettings.SfxVolume;

    /// <summary>공용 효과음 모음. 매니저가 없거나 연결이 안 됐으면 null이므로 ?. 로 접근한다.</summary>
    public static CommonSfx Common => Instance != null ? Instance.common : null;

    // ---------------------------------------------------------------- 외부에서 부르는 곳

    /// <summary>월드 위치에서 효과음을 낸다. 매니저가 없어도 안전하다.</summary>
    public static void Play(SfxDef def, Vector3 position)
    {
        if (Instance != null) Instance.PlayNow(def, position, true);
    }

    /// <summary>위치가 필요 없는 UI 소리 등. 좌우 팬 없이 정중앙에서 난다.</summary>
    public static void Play(SfxDef def)
    {
        if (Instance != null) Instance.PlayNow(def, Vector3.zero, false);
    }

    /// <summary>bank에 등록해둔 효과음을 id로 재생한다.</summary>
    public static void Play(string id, Vector3 position)
    {
        if (Instance == null) return;

        SfxDef def = Instance.Find(id);
        if (def != null) Instance.PlayNow(def, position, true);
    }

    /// <summary>bank에서 id로 찾는다. 없으면 null.</summary>
    public SfxDef Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return byId.TryGetValue(id, out SfxDef def) ? def : null;
    }

    /// <summary>재생 중인 소리를 전부 끊는다. 씬을 다시 불러올 때 쓴다.</summary>
    public void StopAll()
    {
        if (voices == null) return;

        for (int i = 0; i < voices.Length; i++)
        {
            if (voices[i] == null) continue;

            voices[i].Stop();
            voices[i].clip = null;
            voiceDef[i] = null;
        }

        lastPlayTime.Clear();
    }

    // ---------------------------------------------------------------- 준비

    private void Awake()
    {
        Instance = this;

        BuildVoices();
        BuildBank();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildVoices()
    {
        voices = new AudioSource[voiceCount];
        voiceDef = new SfxDef[voiceCount];
        voiceStart = new float[voiceCount];

        for (int i = 0; i < voiceCount; i++)
        {
            GameObject go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            voices[i] = source;
        }
    }

    private void BuildBank()
    {
        if (bank == null) return;

        foreach (SfxDef def in bank)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) continue;

            if (byId.ContainsKey(def.Id))
            {
                Debug.LogWarning("[SfxManager] id 가 중복입니다: " + def.Id + " · 먼저 등록된 것을 씁니다.", def);
                continue;
            }

            byId.Add(def.Id, def);
        }
    }

    // ---------------------------------------------------------------- 재생

    private void PlayNow(SfxDef def, Vector3 position, bool usePosition)
    {
        if (def == null) return;

        if (!def.HasClip)
        {
            WarnMissing(def);
            return;
        }

        // 반복 재생 소리는 오브젝트 본인이 들고 있어야 한다. 풀로 재생하면 멈출 방법이 없다.
        if (def.Loop)
        {
            Debug.LogWarning("[SfxManager] " + def.name + " 은 Loop가 켜져 있습니다. "
                             + "반복 재생은 오브젝트의 AudioSource에 ApplyToLoopSource()로 붙이세요.", def);
            return;
        }

        // timeScale이 0인 레벨업/결과 화면에서도 시간이 흘러야 하므로 unscaled를 쓴다.
        float now = Time.unscaledTime;

        if (lastPlayTime.TryGetValue(def, out float last) && now - last < def.MinInterval) return;

        int active = CountActive(def);
        if (active >= def.MaxConcurrent) return;

        int voice = TakeVoice(def.Important, now);
        if (voice < 0) return;

        AudioSource source = voices[voice];

        source.clip = PickClip(def);
        // 설정 UI의 전체·효과음 볼륨까지 곱해진 값을 써야 한다. private masterVolume 만 쓰면 설정이 먹지 않는다.
        source.volume = def.Volume * RandomJitter(def.VolumeJitter) * CrowdScale(def, active) * MasterVolume;
        source.pitch = def.Pitch * RandomJitter(def.PitchJitter);
        source.spatialBlend = def.SpatialBlend;
        source.panStereo = usePosition ? PanFor(position, def) : 0f;

        if (def.SpatialBlend > 0f) source.transform.position = position;

        source.Play();

        voiceDef[voice] = def;
        voiceStart[voice] = now;
        lastPlayTime[def] = now;
    }

    // 같은 클립이 연달아 두 번 나오지 않게 고른다. 클립이 하나뿐이면 그냥 그것.
    private AudioClip PickClip(SfxDef def)
    {
        int count = def.Clips.Length;
        if (count == 1) return def.Clips[0];

        int index = Random.Range(0, count);

        if (lastClipIndex.TryGetValue(def, out int previous) && index == previous)
        {
            index = (index + 1) % count;
        }

        lastClipIndex[def] = index;
        return def.Clips[index];
    }

    // 빈 보이스를 찾는다. 없는데 중요한 소리라면, 안 중요한 것 중 가장 오래된 것을 밀어낸다.
    private int TakeVoice(bool important, float now)
    {
        for (int i = 0; i < voices.Length; i++)
        {
            if (!voices[i].isPlaying) return i;
        }

        if (!important) return -1;

        int oldest = -1;
        float oldestStart = now;

        for (int i = 0; i < voices.Length; i++)
        {
            if (voiceDef[i] != null && voiceDef[i].Important) continue;   // 중요한 소리끼리는 안 뺏는다
            if (voiceStart[i] > oldestStart) continue;

            oldestStart = voiceStart[i];
            oldest = i;
        }

        if (oldest >= 0) voices[oldest].Stop();
        return oldest;
    }

    private int CountActive(SfxDef def)
    {
        int count = 0;

        for (int i = 0; i < voices.Length; i++)
        {
            if (voiceDef[i] == def && voices[i].isPlaying) count++;
        }

        return count;
    }

    // 같은 소리가 n개 겹치면 진폭이 그대로 더해져 찢어진다. 개수만큼 각각을 줄여 총합을 잡아둔다.
    private static float CrowdScale(SfxDef def, int active)
    {
        if (active <= 0 || def.CrowdFalloff <= 0f) return 1f;

        float damped = 1f / Mathf.Sqrt(active + 1f);
        return Mathf.Lerp(1f, damped, def.CrowdFalloff);
    }

    private static float RandomJitter(float amount)
    {
        return amount <= 0f ? 1f : 1f + Random.Range(-amount, amount);
    }

    // 화면 가로 위치를 그대로 좌우 팬으로 쓴다. 2D 사운드여도 방향감이 생긴다.
    private float PanFor(Vector3 world, SfxDef def)
    {
        if (def.StereoPan <= 0f) return 0f;

        // 씬을 다시 불러오면 예전 카메라는 파괴된다. Unity의 == null 은 파괴된 것도 true라 자동으로 다시 잡힌다.
        if (cam == null) cam = Camera.main;
        if (cam == null) return 0f;

        float viewportX = cam.WorldToViewportPoint(world).x;
        return Mathf.Clamp((viewportX - 0.5f) * 2f, -1f, 1f) * def.StereoPan;
    }

    private void WarnMissing(SfxDef def)
    {
        if (!warnOnMissingClip || !warned.Add(def)) return;

        Debug.LogWarning("[SfxManager] " + def.name + " 에 AudioClip이 없습니다. 재생을 건너뜁니다.", def);
    }
}
