// 게임 중 일시정지 두 가지를 담당한다. 씬 배선 없이 UI를 코드로 만든다.
//  - Space : 전술 일시정지. 화면을 가리지 않고 포탑만 옮길 수 있다.
//  - ESC   : 일시정지 메뉴. 화면을 덮고 계속하기 / 다시하기를 고른다.
// 레벨업·결과·메인메뉴가 이미 timeScale을 쥐고 있을 때는 끼어들지 않는다. 서로 덮어쓰면 게임이 영영 멈춘다.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [Header("폰트")]
    [Tooltip("코드로 만드는 글자에 쓸 폰트. 비우면 TMP 기본 폰트를 쓰는데, " +
             "에셋 임포트로 기본 폰트가 라틴 전용으로 바뀌면 한글이 통째로 깨진다. 반드시 연결한다.")]
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("전술 일시정지 안내")]
    [SerializeField] private string tacticalMessage = "일시정지  ·  포탑을 옮길 수 있습니다  ·  Space 계속";
    [SerializeField] private Color tacticalColor = new Color(0.55f, 0.85f, 1f);

    [Tooltip("화면 위쪽에서 얼마나 내려서 띄울지. 음수가 아래쪽이라 값을 올릴수록 문구가 위로 간다.")]
    [SerializeField] private float tacticalBannerY = -80f;

    /// <summary>Space로 멈춘 상태. 이때는 포탑을 집어 옮길 수 있다.</summary>
    public bool TacticalPaused { get; private set; }

    /// <summary>ESC 메뉴가 열려 있다. 화면을 덮으므로 포탑 조작도 막는다.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>어떤 이유로든 이 스크립트가 게임을 멈춰둔 상태.</summary>
    public bool IsPaused => TacticalPaused || MenuOpen;

    /// <summary>포탑을 집어 옮겨도 되는가. 드래그 핸들러가 물어본다.</summary>
    public bool AllowsTurretDrag => !MenuOpen;

    private GameObject menuPanel;
    private RectTransform menuBox;
    private TextMeshProUGUI tacticalBanner;

    private void Awake()
    {
        Instance = this;

        Build();

        menuPanel.SetActive(false);
        tacticalBanner.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 설정창이 열려 있으면 ESC는 그쪽이 먼저 받는다. 여기서 또 받으면 두 창이 같이 닫힌다.
        if (SettingsUI.Instance != null && SettingsUI.Instance.IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame) ToggleMenu();
        else if (keyboard.spaceKey.wasPressedThisFrame) ToggleTactical();
    }

    private void OpenSettings()
    {
        if (SettingsUI.Instance != null) SettingsUI.Instance.Open();
    }

    /// <summary>다른 UI가 이미 게임을 멈춰 쥐고 있으면 손대지 않는다.</summary>
    private bool BlockedByOtherUI()
    {
        if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return true;
        if (LevelUpUI.Instance != null && LevelUpUI.Instance.IsOpen) return true;
        if (ResultUI.Instance != null && ResultUI.Instance.IsOpen) return true;

        GameManager game = GameManager.Instance;
        return game == null || game.State == GameManager.GameState.Menu || game.IsOver;
    }

    // ---------------------------------------------------------------- Space

    private void ToggleTactical()
    {
        // 메뉴가 떠 있는 동안에는 Space를 무시한다. 메뉴를 닫는 길은 메뉴 안에만 둔다.
        if (MenuOpen) return;
        if (!TacticalPaused && BlockedByOtherUI()) return;

        TacticalPaused = !TacticalPaused;

        tacticalBanner.gameObject.SetActive(TacticalPaused);

        if (TacticalPaused) PulseBanner();
        else tacticalBanner.rectTransform.DOKill();

        SfxManager.Play(SfxManager.Common?.ButtonClick);
        ApplyTimeScale();
    }

    private void PulseBanner()
    {
        RectTransform rect = tacticalBanner.rectTransform;

        rect.DOKill();
        rect.localScale = Vector3.one;

        // 멈춰 있다는 걸 계속 알려야 하므로 무한 반복. timeScale이 0이라 반드시 unscaled로 돌린다.
        rect.DOScale(1.04f, 0.7f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    // ---------------------------------------------------------------- ESC

    private void ToggleMenu()
    {
        if (MenuOpen)
        {
            CloseMenu();
            return;
        }

        if (BlockedByOtherUI()) return;

        MenuOpen = true;
        menuPanel.SetActive(true);

        SfxManager.Play(SfxManager.Common?.ButtonClick);

        // timeScale이 0이므로 unscaled로 돌려야 재생된다.
        menuBox.DOKill();
        menuBox.localScale = Vector3.one * 0.85f;
        menuBox.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);

        ApplyTimeScale();
    }

    private void CloseMenu()
    {
        MenuOpen = false;

        menuBox.DOKill();
        menuPanel.SetActive(false);

        SfxManager.Play(SfxManager.Common?.ButtonClick);
        ApplyTimeScale();
    }

    /// <summary>메뉴를 건너뛰고 곧바로 새 판을 시작한다.</summary>
    private void RestartAndPlay()
    {
        if (!LeaveForSceneReload()) return;

        GameManager.Instance.RestartAndPlay();
    }

    /// <summary>메인 메뉴로 돌아간다. 씬을 다시 불러오면 메뉴 상태로 시작한다.</summary>
    private void BackToMainMenu()
    {
        if (!LeaveForSceneReload()) return;

        GameManager.Instance.Restart();
    }

    /// <summary>씬을 다시 불러오기 전 정리. GameManager가 없으면 시간만 풀고 false를 준다.</summary>
    private bool LeaveForSceneReload()
    {
        MenuOpen = false;
        TacticalPaused = false;

        menuBox.DOKill();
        tacticalBanner.rectTransform.DOKill();
        menuPanel.SetActive(false);
        tacticalBanner.gameObject.SetActive(false);

        SfxManager.Play(SfxManager.Common?.ButtonClick);

        if (GameManager.Instance != null) return true;

        // 씬을 못 부르면 최소한 게임이 멈춘 채로 굳지는 않게 한다.
        Time.timeScale = 1f;
        return false;
    }

    /// <summary>이 스크립트가 멈춰둔 것만 되돌린다.</summary>
    private void ApplyTimeScale()
    {
        Time.timeScale = IsPaused ? 0f : 1f;
    }

    // ---------------------------------------------------------------- 생성

    private void Build()
    {
        GameObject canvasGo = new GameObject("PauseCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // 레벨업(200)보다 위, 결과(300)보다 아래.
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        BuildTacticalBanner(canvasGo.transform);
        BuildMenu(canvasGo.transform);
    }

    /// <summary>전술 일시정지 안내. 화면을 가리면 포탑을 못 보므로 위쪽에 한 줄만 띄운다.</summary>
    private void BuildTacticalBanner(Transform parent)
    {
        tacticalBanner = CreateText("TacticalBanner", parent, tacticalMessage, 40f);
        tacticalBanner.color = tacticalColor;

        RectTransform rect = tacticalBanner.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(1200f, 60f);
        rect.anchoredPosition = new Vector2(0f, tacticalBannerY);
    }

    private void BuildMenu(Transform parent)
    {
        menuPanel = new GameObject("Dim", typeof(RectTransform));
        menuPanel.transform.SetParent(parent, false);

        Image dim = menuPanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform dimRect = menuPanel.GetComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;

        GameObject boxGo = new GameObject("Box", typeof(RectTransform));
        boxGo.transform.SetParent(menuPanel.transform, false);

        Image boxImage = boxGo.AddComponent<Image>();
        boxImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

        menuBox = boxGo.GetComponent<RectTransform>();
        SetRect(menuBox, new Vector2(620f, 590f), Vector2.zero);

        TextMeshProUGUI title = CreateText("Title", menuBox, "일시정지", 68f);
        SetRect(title.rectTransform, new Vector2(560f, 100f), new Vector2(0f, 210f));
        title.color = new Color(0.85f, 0.88f, 0.95f);

        BuildButton("ResumeButton", "계속하기", new Vector2(0f, 95f), CloseMenu);
        BuildButton("SettingsButton", "설정", new Vector2(0f, 10f), OpenSettings);
        BuildButton("RestartButton", "다시하기", new Vector2(0f, -75f), RestartAndPlay);
        BuildButton("MenuButton", "메인 메뉴로", new Vector2(0f, -160f), BackToMainMenu);

        TextMeshProUGUI hint = CreateText("Hint", menuBox, "ESC 닫기   ·   Space 전술 일시정지", 26f);
        SetRect(hint.rectTransform, new Vector2(560f, 44f), new Vector2(0f, -240f));
        hint.color = new Color(0.6f, 0.63f, 0.7f);
    }

    private void BuildButton(string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(menuBox, false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.22f, 0.24f, 0.3f, 1f);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(360f, 74f), position);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        // 런타임에 만드는 버튼이라 호버 연출도 여기서 같이 붙인다.
        go.AddComponent<ButtonHover>();

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText("Label", go.transform, label, 34f);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
