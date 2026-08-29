// 배경음 전용 플레이어. 씬을 다시 불러와도 음악이 끊기지 않도록 혼자 살아남는다.
// 효과음과 수명이 다르므로 SfxManager의 보이스 풀을 쓰지 않고 자기 AudioSource를 들고 있는다.

using UnityEngine;

public class BgmPlayer : MonoBehaviour
{
    public static BgmPlayer Instance { get; private set; }

    [Header("브금")]
    [Tooltip("반복 재생할 음악. Loop를 켠 SfxDef를 넣는다. 비우면 조용히 넘어간다.")]
    [SerializeField] private SfxDef bgm;

    private AudioSource source;

    private void Awake()
    {
        // 씬을 다시 불러오면 새 씬에도 이 오브젝트가 하나 들어있다.
        // 먼저 자리를 잡은 쪽만 남기고 나중 것은 스스로 사라져야 음악이 겹치지 않는다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad는 루트 오브젝트에만 걸린다.
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Build();
    }

    private void OnEnable()
    {
        SoundSettings.Changed += ApplyVolume;
    }

    private void OnDisable()
    {
        SoundSettings.Changed -= ApplyVolume;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        if (bgm == null || !bgm.HasClip) return;

        source = gameObject.AddComponent<AudioSource>();
        bgm.ApplyToLoopSource(source);

        // 메뉴와 레벨업에서 timeScale이 0이어도 계속 흘러야 한다.
        // AudioSource는 원래 timeScale을 타지 않지만, 리스너가 멈추는 경우까지 막아둔다.
        source.ignoreListenerPause = true;

        ApplyVolume();
        source.Play();
    }

    /// <summary>설정 UI에서 볼륨을 움직이면 즉시 반영된다.</summary>
    private void ApplyVolume()
    {
        if (source == null || bgm == null) return;

        source.volume = bgm.Volume * SoundSettings.BgmVolume;
    }
}
