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
