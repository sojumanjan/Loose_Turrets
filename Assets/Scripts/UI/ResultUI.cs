// 스테이지 클리어 / 게임 오버 결과창. 최종 성적을 보여주고 재시작 버튼을 준다. 씬 배선 없이 코드로 만든다.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultUI : MonoBehaviour
{
    public static ResultUI Instance { get; private set; }

    [Header("색")]
    [SerializeField] private Color clearColor = new Color(0.45f, 0.95f, 0.55f);
    [SerializeField] private Color gameOverColor = new Color(0.95f, 0.35f, 0.35f);
    [SerializeField] private Color endlessColor = new Color(1f, 0.55f, 0.85f);

    public bool IsOpen { get; private set; }

    private GameObject panel;
    private RectTransform box;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI hintText;
    private Image accentStrip;
    private GameObject continueButton;

    private void Awake()
    {
        Instance = this;
        Build();
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(bool cleared)
    {
        IsOpen = true;
        panel.SetActive(true);

        Color accent = cleared ? clearColor : gameOverColor;

        titleText.text = cleared ? "STAGE CLEAR!" : "GAME OVER";
        titleText.color = accent;
        accentStrip.color = accent;
        statsText.text = BuildStats();

        // 클리어했을 때만 무한 모드로 이어갈 수 있다.
        continueButton.SetActive(cleared);
        hintText.text = cleared ? "E to continue   ·   R to restart" : "or press  R";

        // Time.timeScale이 0이므로 반드시 unscaled로 돌려야 애니메이션이 재생된다.
        box.DOKill();
        box.localScale = Vector3.one * 0.8f;
        box.DOScale(1f, 0.32f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private static string BuildStats()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return "";

        int minutes = Mathf.FloorToInt(game.Elapsed / 60f);
        int seconds = Mathf.FloorToInt(game.Elapsed % 60f);

        return string.Format(
            "WAVE      {0} / {1}\nTIME      {2:00}:{3:00}\nKILLS     {4}\nLEVEL     {5}\nTURRETS   {6}",
            game.Wave, game.TotalWaves, minutes, seconds, game.Kills, game.Level, TurretBase.All.Count);
    }

    private void Update()
    {
        if (!IsOpen) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (continueButton.activeSelf && keyboard.eKey.wasPressedThisFrame) ContinueEndless();
    }

    /// <summary>클리어 화면에서 무한 모드로 이어간다.</summary>
    private void ContinueEndless()
    {
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        IsOpen = false;
        box.DOKill();
        panel.SetActive(false);

        if (GameManager.Instance != null) GameManager.Instance.StartEndless();
    }

    private void Restart()
    {
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        IsOpen = false;
        box.DOKill();
        panel.SetActive(false);

        if (GameManager.Instance != null) GameManager.Instance.Restart();
    }

    // ---------------- UI 생성 ----------------

    private void Build()
    {
        GameObject canvasGo = new GameObject("ResultCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 레벨업 UI(200)보다 위에 떠야 한다.
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        panel = CreateFullScreenImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.85f));

        // ---- 가운데 박스 ----
        GameObject boxGo = new GameObject("Box", typeof(RectTransform));
        boxGo.transform.SetParent(panel.transform, false);

        Image boxImage = boxGo.AddComponent<Image>();
        boxImage.color = new Color(0.12f, 0.13f, 0.17f, 1f);

        box = boxGo.GetComponent<RectTransform>();
        SetRect(box, new Vector2(760f, 620f), Vector2.zero);

        GameObject stripGo = new GameObject("Strip", typeof(RectTransform));
        stripGo.transform.SetParent(box, false);
        accentStrip = stripGo.AddComponent<Image>();
        accentStrip.raycastTarget = false;
        RectTransform stripRect = stripGo.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.offsetMin = new Vector2(0f, -16f);
        stripRect.offsetMax = Vector2.zero;

        titleText = CreateText("Title", box, "", 78f, TextAlignmentOptions.Center);
        SetRect(titleText.rectTransform, new Vector2(700f, 120f), new Vector2(0f, 200f));

        statsText = CreateText("Stats", box, "", 36f, TextAlignmentOptions.Center);
        SetRect(statsText.rectTransform, new Vector2(660f, 280f), new Vector2(0f, 10f));
        statsText.color = new Color(0.82f, 0.85f, 0.9f);

        // ---- 버튼 ----
        BuildButton("RestartButton", "RESTART", new Vector2(-170f, -200f), Restart);
        continueButton = BuildButton("ContinueButton", "ENDLESS", new Vector2(170f, -200f), ContinueEndless);
        continueButton.GetComponent<Image>().color = new Color(0.28f, 0.2f, 0.28f, 1f);
        continueButton.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = endlessColor;
        continueButton.SetActive(false);

        hintText = CreateText("Hint", box, "or press  R", 26f, TextAlignmentOptions.Center);
        SetRect(hintText.rectTransform, new Vector2(700f, 44f), new Vector2(0f, -262f));
        hintText.color = new Color(0.6f, 0.63f, 0.7f);
    }

    private GameObject BuildButton(string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(box, false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.22f, 0.24f, 0.3f, 1f);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(300f, 78f), position);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText("Label", go.transform, label, 34f, TextAlignmentOptions.Center);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return go;
    }

    private static GameObject CreateFullScreenImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
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
