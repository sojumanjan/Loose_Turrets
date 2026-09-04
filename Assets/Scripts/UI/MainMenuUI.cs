// 시작 화면. UI는 씬의 Canvas에 직접 배치하고, 이 스크립트는 참조만 받아 동작시킨다.
// 문구와 배치는 인스펙터/씬에서 자유롭게 고치면 된다.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    [Header("씬 참조")]
    [Tooltip("메뉴 전체를 켜고 끄는 오브젝트. 보통 반투명 배경 패널.")]
    [SerializeField] private GameObject panel;

    [Tooltip("등장할 때 튀어나오는 애니메이션이 걸릴 대상. 비워두면 애니메이션만 생략된다.")]
    [SerializeField] private RectTransform box;

    [SerializeField] private Button startButton;

    [Tooltip("설정창을 여는 버튼. 비워두면 메인 메뉴에서는 설정을 열 수 없다.")]
    [SerializeField] private Button settingsButton;

    [SerializeField] private Button exitButton;

    /// <summary>보스를 잡아 열리는 "중간부터 시작" 버튼 하나.</summary>
    [System.Serializable]
    public class SkipStart
    {
        [Tooltip("이 버튼을 여는 보스의 웨이브 번호. 그 보스를 한 번이라도 잡았으면 버튼이 나타난다.")]
        [Min(1)] public int RequiresBossWave = 10;

        [Tooltip("시작할 웨이브 번호.")]
        [Min(1)] public int StartWave = 11;

        [Tooltip("시작 시 맞출 레벨. 여기까지 필요한 XP를 몰아줘서 카드를 연달아 고르게 된다.")]
        [Min(1)] public int StartLevel = 30;

        [Tooltip("버틴 시간을 몇 초부터 시작할지. 처음부터 왔다면 그쯤 걸렸을 시간을 넣는다. " +
                 "340이면 5분 40초부터 센다.")]
        [Min(0f)] public float StartElapsedSeconds = 340f;

        [Tooltip("눌렀을 때 이 시작을 실행할 버튼. 조건을 못 채웠으면 통째로 숨긴다.")]
        public Button Button;
    }

    [Header("중간부터 시작 (보스를 잡으면 열린다)")]
    [Tooltip("판을 넘어 남는 해금이다. 조건을 못 채운 버튼은 메뉴에서 숨겨진다.")]
    [SerializeField] private SkipStart[] skipStarts;

    [Tooltip("메인 메뉴에서 F6으로 위 버튼들의 해금을 강제로 켜고 끈다. 배포 전엔 끄는 게 좋다.")]
    [SerializeField] private bool enableUnlockDebugKey = true;

    [Header("역대 최고 기록 (비워두면 표시만 생략된다)")]
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI bestWaveText;
    [SerializeField] private TextMeshProUGUI bestKillsText;
    [SerializeField] private TextMeshProUGUI bestDamageText;

    [Tooltip("기록이 하나도 없을 때 값 대신 보여줄 문구.")]
    [SerializeField] private string emptyRecordLabel = "-";

    [Header("연출")]
    [SerializeField] private float popDuration = 0.35f;
    [SerializeField] private float popFromScale = 0.85f;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// 지금 해금된 것 중 가장 앞선 시작 웨이브. 하나도 없으면 0.
    /// 결과창이 "이제 N스테이지부터 시작할 수 있습니다" 를 띄울 때 이 값을 쓴다.
    /// 어떤 보스가 어느 웨이브를 여는지는 아래 skipStarts 하나에만 적혀 있으므로 여기서 답한다.
    /// </summary>
    public int HighestUnlockedStartWave
    {
        get
        {
            if (skipStarts == null) return 0;

            int best = 0;
            for (int i = 0; i < skipStarts.Length; i++)
            {
                SkipStart entry = skipStarts[i];
                if (entry == null) continue;
                if (!WaveUnlocks.IsBossCleared(entry.RequiresBossWave)) continue;

                if (entry.StartWave > best) best = entry.StartWave;
            }

            return best;
        }
    }

    private Tween popTween;

    private void Awake()
    {
        Instance = this;

        if (panel == null)
        {
            Debug.LogError("[MainMenuUI] panel 이 비어 있습니다. 씬의 메뉴 패널을 연결하세요.", this);
            return;
        }

        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.AddListener(Quit);

        BindSkipStarts();

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // 다른 UI들의 Awake가 끝난 뒤에 연다.
        Show();

        // 일시정지 메뉴의 '다시하기'로 들어온 경우엔 메뉴를 보여주지 않고 곧바로 시작한다.
        // Show() 뒤에 불러야 StartGame의 IsOpen 검사를 통과한다.
        if (GameManager.ConsumeAutoStart()) StartGame();
    }

    private void OpenSettings()
    {
        if (SettingsUI.Instance != null) SettingsUI.Instance.Open();
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 설정창이 위에 떠 있는 동안에는 메뉴 단축키를 받지 않는다.
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        // F6: 중간부터 시작 버튼을 강제로 열고 닫는다. 매번 보스를 잡아보지 않아도 확인할 수 있다.
        if (enableUnlockDebugKey && keyboard.f6Key.wasPressedThisFrame)
        {
            ToggleSkipStartUnlocks();
            return;
        }

        // Space는 전술 일시정지 전용이라 여기서 쓰지 않는다.
        if (keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            StartGame();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame)
        {
            Quit();
        }
    }

    public void Show()
    {
        if (panel == null) return;

        IsOpen = true;
        panel.SetActive(true);

        // 판이 끝나고 메뉴로 돌아오면 갱신된 기록이 보여야 하므로 열 때마다 다시 읽는다.
        RefreshRecords();
        RefreshSkipStarts();

        if (box == null) return;

        // Time.timeScale이 0이므로 반드시 unscaled로 돌려야 애니메이션이 재생된다.
        popTween?.Kill();
        box.localScale = Vector3.one * popFromScale;
        popTween = box.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    /// <summary>PlayerPrefs에 저장된 역대 최고 기록을 네 칸에 채운다. 설정창의 초기화 버튼도 부른다.</summary>
    public void RefreshRecords()
    {
        // 한 판도 안 끝냈으면 0이 아니라 빈 표시를 보여준다.
        bool hasAny = BestRecords.BestSeconds > 0f
                      || BestRecords.BestWave > 0
                      || BestRecords.BestKills > 0
                      || BestRecords.BestDamage > 0f;

        if (bestTimeText != null)
            bestTimeText.text = hasAny ? BestRecords.FormatTime(BestRecords.BestSeconds) : emptyRecordLabel;

        if (bestWaveText != null)
            bestWaveText.text = hasAny ? BestRecords.BestWave.ToString() : emptyRecordLabel;

        if (bestKillsText != null)
            bestKillsText.text = hasAny ? BestRecords.FormatNumber(BestRecords.BestKills) : emptyRecordLabel;

        if (bestDamageText != null)
            bestDamageText.text = hasAny ? BestRecords.FormatNumber(BestRecords.BestDamage) : emptyRecordLabel;
    }

    private void Close()
    {
        IsOpen = false;
        popTween?.Kill();

        if (panel != null) panel.SetActive(false);
    }

    private void StartGame()
    {
        if (!IsOpen) return;

        SfxManager.Play(SfxManager.Common?.ButtonClick);

        Close();
        if (GameManager.Instance != null) GameManager.Instance.StartGame();
    }

    // ---------------------------------------------------------------- 중간부터 시작

    /// <summary>
    /// 버튼마다 클릭을 연결한다. 람다가 반복 변수를 붙잡지 않도록 항목을 지역 변수로 받아 쓴다.
    /// </summary>
    private void BindSkipStarts()
    {
        if (skipStarts == null) return;

        for (int i = 0; i < skipStarts.Length; i++)
        {
            SkipStart entry = skipStarts[i];
            if (entry == null || entry.Button == null) continue;

            entry.Button.onClick.AddListener(() => StartSkipped(entry));
        }
    }

    /// <summary>조건을 채운 버튼만 보여준다. 메뉴를 열 때마다 다시 판단한다.</summary>
    private void RefreshSkipStarts()
    {
        if (skipStarts == null) return;

        for (int i = 0; i < skipStarts.Length; i++)
        {
            SkipStart entry = skipStarts[i];
            if (entry == null || entry.Button == null) continue;

            bool unlocked = WaveUnlocks.IsBossCleared(entry.RequiresBossWave);

            GameObject go = entry.Button.gameObject;
            if (go.activeSelf != unlocked) go.SetActive(unlocked);
        }
    }

    /// <summary>
    /// 디버그용. 중간부터 시작 버튼들의 해금을 한 번에 뒤집는다.
    /// 하나라도 잠겨 있으면 전부 열고, 전부 열려 있으면 전부 닫는다.
    /// </summary>
    private void ToggleSkipStartUnlocks()
    {
        if (skipStarts == null || skipStarts.Length == 0) return;

        bool anyLocked = false;
        for (int i = 0; i < skipStarts.Length; i++)
        {
            if (skipStarts[i] == null) continue;
            if (!WaveUnlocks.IsBossCleared(skipStarts[i].RequiresBossWave)) { anyLocked = true; break; }
        }

        for (int i = 0; i < skipStarts.Length; i++)
        {
            SkipStart entry = skipStarts[i];
            if (entry == null) continue;

            WaveUnlocks.SetBossCleared(entry.RequiresBossWave, anyLocked);
        }

        RefreshSkipStarts();
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        Debug.Log("[디버그] 중간부터 시작 버튼 " + (anyLocked ? "전부 열림" : "전부 닫힘"));
    }

    private void StartSkipped(SkipStart entry)
    {
        if (!IsOpen || entry == null) return;

        // 버튼이 보이더라도 조건을 다시 확인한다. 숨기는 것만으로 막았다고 믿지 않는다.
        if (!WaveUnlocks.IsBossCleared(entry.RequiresBossWave)) return;

        SfxManager.Play(SfxManager.Common?.ButtonClick);

        Close();
        if (GameManager.Instance != null)
            GameManager.Instance.StartGameAt(entry.StartWave, entry.StartLevel, entry.StartElapsedSeconds);
    }

    private void Quit()
    {
        if (!IsOpen) return;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
