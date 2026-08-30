// 레벨업 3택 오버레이. 씬 배선이 필요 없도록 UI를 코드로 만든다. 마우스 클릭과 1/2/3 키를 모두 받는다.
// 포탑 관련 카드에는 특수 강화까지 남은 진행도를 별로 표시한다.

using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    // 카드 하나에 그려둘 다이아몬드 슬롯의 최대 개수. SpecialThreshold나 MaxUpgrades가 이보다 크면 잘린다.
    private const int MaxStarSlots = 8;

    // 강화 횟수 게이지 칸의 최대 개수. MaxUpgrades가 이보다 크면 잘린다.
    private const int MaxPipSlots = 8;

    [Header("폰트")]
    [Tooltip("코드로 만드는 글자에 쓸 폰트. 비우면 TMP 기본 폰트를 쓰는데, " +
             "에셋 임포트로 기본 폰트가 라틴 전용으로 바뀌면 한글이 통째로 깨진다. 반드시 연결한다.")]
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("카드")]
    [SerializeField] private int cardCount = 3;
    [SerializeField] private Vector2 cardSize = new Vector2(320f, 430f);
    [SerializeField] private float cardSpacing = 36f;

    [Header("카드 아이콘 (위쪽 중앙)")]
    [Tooltip("어느 포탑 카드인지 구분하는 아이콘. TurretDef의 Card Icon을 그린다.")]
    [SerializeField] private float iconSize = 56f;
    [Tooltip("카드 위쪽 모서리에서 띄워 올리는 높이. 카드 안 상단 중앙은 번호가 쓰므로 바깥으로 올린다. " +
             "너무 키우면 가운데 카드가 '레벨 업!' 제목과 부딪힌다.")]
    [SerializeField] private float iconGap = 6f;

    [Header("카드 색 배합")]
    [SerializeField] private Color cardBaseColor = new Color(0.13f, 0.14f, 0.18f);
    [Tooltip("카드 배경을 포탑 색으로 물들이는 정도.")]
    [SerializeField, Range(0f, 1f)] private float backgroundTint = 0.22f;
    [Tooltip("특수 강화 카드는 더 진하게 물들여 눈에 띄게 한다.")]
    [SerializeField, Range(0f, 1f)] private float specialBackgroundTint = 0.45f;
    [Tooltip("제목 글씨를 흰색 쪽으로 밝히는 정도.")]
    [SerializeField, Range(0f, 1f)] private float titleBrightness = 0.35f;

    [Header("강화 횟수 게이지 (별과 별개)")]
    [SerializeField] private Vector2 pipSize = new Vector2(34f, 9f);
    [SerializeField] private float pipSpacing = 40f;
    [SerializeField] private float pipRowY = -118f;
    [SerializeField] private float pipLabelY = -146f;
    [SerializeField] private Color pipEmptyColor = new Color(0.3f, 0.32f, 0.38f);
    [Tooltip("상한에 도달했을 때 게이지와 글씨에 쓸 색.")]
    [SerializeField] private Color pipFullColor = new Color(0.95f, 0.45f, 0.4f);

    [Header("별")]
    [SerializeField] private float starSize = 22f;
    [SerializeField] private float starSpacing = 34f;
    [SerializeField] private float starRowY = -186f;
    [Tooltip("채워진 별. 포탑 색과 무관하게 노란색으로 통일해 눈에 띄게 한다.")]
    [SerializeField] private Color filledStarColor = new Color(1f, 0.82f, 0.2f);
    [Tooltip("빈 별. 배경에 묻히지 않을 만큼 밝게.")]
    [SerializeField] private Color emptyStarColor = new Color(0.62f, 0.65f, 0.72f);

    [Header("특수 강화 카드 강조")]
    [SerializeField] private float glowPadding = 16f;
    [SerializeField] private float glowPulseScale = 1.05f;
    [SerializeField] private float glowPulseDuration = 0.65f;
    [SerializeField, Range(0f, 1f)] private float glowMinAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float glowMaxAlpha = 0.7f;

    public bool IsOpen { get; private set; }

    private GameObject panel;
    private RectTransform[] cardRects;
    private Image[] cardBackgrounds;
    private Image[] cardStrips;
    private Image[] cardIcons;
    private Image[] cardGlows;
    private Sequence[] glowTweens;
    private Image[][] cardStars;
    private Image[][] cardPips;
    private TextMeshProUGUI[] cardPipLabels;
    private TextMeshProUGUI[] cardNumbers;
    private TextMeshProUGUI[] cardTitles;
    private TextMeshProUGUI[] cardDescriptions;

    private List<UpgradeOption> options;
    private Action<UpgradeOption> onPicked;

    private void Awake()
    {
        Instance = this;
        EnsureEventSystem();
        Build();
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Pick(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Pick(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Pick(2);
    }

    public void Show(List<UpgradeOption> newOptions, Action<UpgradeOption> callback)
    {
        options = newOptions;
        onPicked = callback;
        IsOpen = true;

        panel.SetActive(true);

        for (int i = 0; i < cardRects.Length; i++)
        {
            bool used = options != null && i < options.Count;
            cardRects[i].gameObject.SetActive(used);
            if (!used) continue;

            UpgradeOption option = options[i];

            cardTitles[i].text = option.Title;
            cardDescriptions[i].text = option.Description;

            ApplyAccent(i, option);
            ApplyIcon(i, option);
            ApplyStars(i, option);
            ApplyPips(i, option);
            ApplyGlow(i, option);

            // Time.timeScale이 0이므로 반드시 unscaled로 돌려야 애니메이션이 재생된다.
            cardRects[i].DOKill();
            cardRects[i].localScale = Vector3.one * 0.8f;
            cardRects[i].DOScale(1f, 0.25f)
                .SetEase(Ease.OutBack)
                .SetDelay(i * 0.06f)
                .SetUpdate(true);
        }
    }

    private void ApplyAccent(int index, UpgradeOption option)
    {
        bool special = IsSpecialCard(option);
        float tint = special ? specialBackgroundTint : backgroundTint;

        cardBackgrounds[index].color = Color.Lerp(cardBaseColor, option.Accent, tint);
        cardStrips[index].color = option.Accent;
        cardNumbers[index].color = option.Accent;
        cardTitles[index].color = Color.Lerp(option.Accent, Color.white, titleBrightness);
    }

    /// <summary>포탑 카드에만 아이콘을 띄운다. 전체 강화나 플레이어 강화는 Icon이 비어 있어 숨겨진다.</summary>
    private void ApplyIcon(int index, UpgradeOption option)
    {
        Image icon = cardIcons[index];

        bool show = option.Icon != null;
        icon.gameObject.SetActive(show);
        if (!show) return;

        icon.sprite = option.Icon;
    }

    private void ApplyStars(int index, UpgradeOption option)
    {
        Image[] stars = cardStars[index];

        bool show = option.StarsTotal > 0 && option.StarsFilled >= 0;
        int total = Mathf.Min(option.StarsTotal, MaxStarSlots);

        // 개수가 달라져도 가운데 정렬을 유지하도록 매번 위치를 다시 잡는다.
        float startX = -(total - 1) * starSpacing * 0.5f;

        for (int i = 0; i < stars.Length; i++)
        {
            bool used = show && i < total;
            stars[i].gameObject.SetActive(used);
            if (!used) continue;

            stars[i].rectTransform.anchoredPosition = new Vector2(startX + i * starSpacing, starRowY);
            stars[i].color = i < option.StarsFilled ? filledStarColor : emptyStarColor;
        }
    }

    /// <summary>일반 강화를 몇 번 썼는지 보여주는 게이지. 특수 강화는 여기 포함되지 않는다.</summary>
    private void ApplyPips(int index, UpgradeOption option)
    {
        Image[] pips = cardPips[index];
        TextMeshProUGUI label = cardPipLabels[index];

        bool show = option.UpgradesMax > 0 && option.UpgradesUsed >= 0;
        label.gameObject.SetActive(show);

        if (!show)
        {
            for (int i = 0; i < pips.Length; i++) pips[i].gameObject.SetActive(false);
            return;
        }

        int total = Mathf.Min(option.UpgradesMax, MaxPipSlots);
        int used = Mathf.Clamp(option.UpgradesUsed, 0, total);
        bool full = used >= total;

        float startX = -(total - 1) * pipSpacing * 0.5f;

        for (int i = 0; i < pips.Length; i++)
        {
            bool active = i < total;
            pips[i].gameObject.SetActive(active);
            if (!active) continue;

            pips[i].rectTransform.anchoredPosition = new Vector2(startX + i * pipSpacing, pipRowY);
            pips[i].color = i < used ? (full ? pipFullColor : option.Accent) : pipEmptyColor;
        }

        label.text = "강화  " + option.UpgradesUsed + " / " + option.UpgradesMax;
        label.color = full ? pipFullColor : new Color(0.66f, 0.7f, 0.78f);
    }

    /// <summary>특수 강화 카드 뒤에 맥동하는 테두리 빛을 켠다.</summary>
    private void ApplyGlow(int index, UpgradeOption option)
    {
        Image glow = cardGlows[index];

        glowTweens[index]?.Kill();
        glowTweens[index] = null;

        bool special = IsSpecialCard(option);
        glow.gameObject.SetActive(special);
        if (!special) return;

        Color color = option.Accent;
        color.a = glowMaxAlpha;
        glow.color = color;

        glow.rectTransform.localScale = Vector3.one;

        // timeScale이 0이므로 unscaled로 돌려야 한다.
        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLoops(-1, LoopType.Yoyo);
        sequence.Join(glow.rectTransform.DOScale(glowPulseScale, glowPulseDuration).SetEase(Ease.InOutSine));
        sequence.Join(glow.DOFade(glowMinAlpha, glowPulseDuration).SetEase(Ease.InOutSine));

        glowTweens[index] = sequence;
    }

    /// <summary>특수 강화 카드인가. 첫 번째든 두 번째든 똑같이 강조한다.</summary>
    private static bool IsSpecialCard(UpgradeOption option)
    {
        return option.Type == UpgradeType.TypeSpecial || option.Type == UpgradeType.TypeSpecial2;
    }

    private void KillGlows()
    {
        if (glowTweens == null) return;

        for (int i = 0; i < glowTweens.Length; i++)
        {
            glowTweens[i]?.Kill();
            glowTweens[i] = null;
        }
    }

    private void Pick(int index)
    {
        if (!IsOpen || options == null) return;
        if (index < 0 || index >= options.Count) return;

        UpgradeOption picked = options[index];

        for (int i = 0; i < cardRects.Length; i++) cardRects[i].DOKill();
        KillGlows();

        IsOpen = false;
        panel.SetActive(false);

        Action<UpgradeOption> callback = onPicked;
        onPicked = null;
        options = null;

        // 콜백 안에서 다음 레벨업이 다시 열릴 수 있으므로 상태를 모두 정리한 뒤 부른다.
        if (callback != null) callback(picked);
    }

    // ---------------- UI 생성 ----------------

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        // Active Input Handling이 Input System 전용이라 StandaloneInputModule은 동작하지 않는다.
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject("LevelUpCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        panel = CreateFullScreenImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.78f));

        // 카드 위로 솟은 아이콘과 부딪히지 않도록 제목을 위로 올려둔다. 패널 위쪽에 여유가 남는다.
        TextMeshProUGUI title = CreateText("Title", panel.transform, "레벨 업!", 91f);
        SetRect(title.rectTransform, new Vector2(1000f, 130f), new Vector2(0f, 390f));
        title.color = new Color(1f, 0.86f, 0.36f);

        TextMeshProUGUI hint = CreateText("Hint", panel.transform, "카드를 클릭하거나  1 / 2 / 3 키", 34f);
        SetRect(hint.rectTransform, new Vector2(1000f, 50f), new Vector2(0f, -320f));
        hint.color = new Color(0.7f, 0.72f, 0.78f);

        cardRects = new RectTransform[cardCount];
        cardBackgrounds = new Image[cardCount];
        cardStrips = new Image[cardCount];
        cardIcons = new Image[cardCount];
        cardGlows = new Image[cardCount];
        glowTweens = new Sequence[cardCount];
        cardStars = new Image[cardCount][];
        cardPips = new Image[cardCount][];
        cardPipLabels = new TextMeshProUGUI[cardCount];
        cardNumbers = new TextMeshProUGUI[cardCount];
        cardTitles = new TextMeshProUGUI[cardCount];
        cardDescriptions = new TextMeshProUGUI[cardCount];

        float totalWidth = cardCount * cardSize.x + (cardCount - 1) * cardSpacing;
        float startX = -totalWidth * 0.5f + cardSize.x * 0.5f;

        for (int i = 0; i < cardCount; i++)
        {
            BuildCard(i, startX + i * (cardSize.x + cardSpacing));
        }
    }

    private void BuildCard(int index, float x)
    {
        // 카드보다 먼저 생성해야 UI 순서상 카드 뒤에 그려진다.
        GameObject glowGo = new GameObject("Glow" + index, typeof(RectTransform));
        glowGo.transform.SetParent(panel.transform, false);

        Image glow = glowGo.AddComponent<Image>();
        glow.raycastTarget = false;
        SetRect(glowGo.GetComponent<RectTransform>(),
            cardSize + new Vector2(glowPadding * 2f, glowPadding * 2f), new Vector2(x, -10f));
        glowGo.SetActive(false);

        cardGlows[index] = glow;

        GameObject card = new GameObject("Card" + index, typeof(RectTransform));
        card.transform.SetParent(panel.transform, false);

        Image background = card.AddComponent<Image>();
        background.color = cardBaseColor;

        RectTransform rect = card.GetComponent<RectTransform>();
        SetRect(rect, cardSize, new Vector2(x, -10f));

        Button button = card.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.45f, 1.45f, 1.45f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        int captured = index;
        button.onClick.AddListener(() => Pick(captured));

        // 카드 상단 색 띠 — 포탑 색을 한눈에 보여준다.
        GameObject stripGo = new GameObject("Strip", typeof(RectTransform));
        stripGo.transform.SetParent(card.transform, false);
        Image strip = stripGo.AddComponent<Image>();
        strip.raycastTarget = false;
        RectTransform stripRect = stripGo.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.offsetMin = new Vector2(0f, -14f);
        stripRect.offsetMax = Vector2.zero;

        // 포탑 아이콘 — 카드 위쪽 바깥에 중앙 정렬로 얹는다.
        // 앵커는 카드 상단 중앙, 피벗은 아래쪽. 그래야 iconGap만큼 카드 위로 솟고 카드 크기가 바뀌어도 따라온다.
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(card.transform, false);

        Image icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = new Vector2(0f, iconGap);

        iconGo.SetActive(false);
        cardIcons[index] = icon;

        TextMeshProUGUI number = CreateText("Number", card.transform, (index + 1).ToString(), 53f);
        SetRect(number.rectTransform, new Vector2(cardSize.x, 70f), new Vector2(0f, cardSize.y * 0.5f - 58f));

        TextMeshProUGUI cardTitle = CreateText("CardTitle", card.transform, "", 48f);
        SetRect(cardTitle.rectTransform, new Vector2(cardSize.x - 40f, 150f), new Vector2(0f, 68f));

        TextMeshProUGUI description = CreateText("Description", card.transform, "", 37f);
        SetRect(description.rectTransform, new Vector2(cardSize.x - 40f, 90f), new Vector2(0f, -48f));
        description.color = new Color(0.72f, 0.75f, 0.82f);

        // 강화 횟수 게이지 — 별과 헷갈리지 않도록 납작한 막대로 그린다.
        Image[] pips = new Image[MaxPipSlots];
        for (int i = 0; i < MaxPipSlots; i++)
        {
            GameObject pipGo = new GameObject("Pip" + i, typeof(RectTransform));
            pipGo.transform.SetParent(card.transform, false);

            Image pip = pipGo.AddComponent<Image>();
            pip.raycastTarget = false;

            RectTransform pipRect = pipGo.GetComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(0.5f, 0.5f);
            pipRect.anchorMax = new Vector2(0.5f, 0.5f);
            pipRect.pivot = new Vector2(0.5f, 0.5f);
            pipRect.sizeDelta = pipSize;

            pipGo.SetActive(false);
            pips[i] = pip;
        }

        TextMeshProUGUI pipLabel = CreateText("PipLabel", card.transform, "", 24f);
        SetRect(pipLabel.rectTransform, new Vector2(cardSize.x, 34f), new Vector2(0f, pipLabelY));
        pipLabel.gameObject.SetActive(false);

        // 별 — 폰트 글리프에 의존하지 않도록 45도 돌린 사각형으로 그린다.
        Image[] stars = new Image[MaxStarSlots];
        for (int i = 0; i < MaxStarSlots; i++)
        {
            GameObject starGo = new GameObject("Star" + i, typeof(RectTransform));
            starGo.transform.SetParent(card.transform, false);

            Image star = starGo.AddComponent<Image>();
            star.raycastTarget = false;

            RectTransform starRect = starGo.GetComponent<RectTransform>();
            starRect.anchorMin = new Vector2(0.5f, 0.5f);
            starRect.anchorMax = new Vector2(0.5f, 0.5f);
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.sizeDelta = new Vector2(starSize, starSize);
            starRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            starGo.SetActive(false);
            stars[i] = star;
        }

        cardRects[index] = rect;
        cardBackgrounds[index] = background;
        cardStrips[index] = strip;
        cardStars[index] = stars;
        cardPips[index] = pips;
        cardPipLabels[index] = pipLabel;
        cardNumbers[index] = number;
        cardTitles[index] = cardTitle;
        cardDescriptions[index] = description;
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
