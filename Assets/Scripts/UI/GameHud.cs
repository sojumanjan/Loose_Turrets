// 화면 HUD. UI는 씬의 Canvas에 직접 배치하고, 이 스크립트는 참조만 받아 값을 갱신한다.
// 위치 / 크기 / 글꼴 / 색은 전부 씬에서 고치면 된다.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameHud : MonoBehaviour
{
    public static GameHud Instance { get; private set; }

    [Header("경험치")]
    [Tooltip("XP 바의 채워지는 부분. anchorMax.x 를 조절해 채운다.")]
    [SerializeField] private RectTransform xpFill;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("체력")]
    [SerializeField] private RectTransform hpFill;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Color hpColor = new Color(0.35f, 0.85f, 0.45f);
    [SerializeField] private Color hpLowColor = new Color(0.9f, 0.3f, 0.3f);
    [Tooltip("이 비율 아래로 내려가면 체력 바가 빨개진다.")]
    [SerializeField, Range(0f, 1f)] private float hpLowRatio = 0.3f;

    [Header("진행 표시")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("배너")]
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private float bannerHoldDuration = 1.1f;
    [SerializeField] private float bannerFadeDuration = 0.45f;
    [SerializeField] private float bannerPopFromScale = 0.7f;

    private Image hpFillImage;
    private Sequence bannerSequence;

    private void Awake()
    {
        Instance = this;

        if (hpFill != null) hpFillImage = hpFill.GetComponent<Image>();
        if (bannerText != null) bannerText.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>웨이브 시작 등 큼직한 알림을 화면 중앙에 잠깐 띄운다.</summary>
    public void ShowBanner(string message, Color color)
    {
        if (bannerText == null) return;

        bannerSequence?.Kill();

        bannerText.text = message;
        bannerText.color = color;
        bannerText.alpha = 1f;

        RectTransform rect = bannerText.rectTransform;
        rect.localScale = Vector3.one * bannerPopFromScale;

        // 레벨업 등으로 timeScale이 0일 수 있으므로 unscaled로 돌린다.
        bannerSequence = DOTween.Sequence().SetUpdate(true);
        bannerSequence.Append(rect.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
        bannerSequence.AppendInterval(bannerHoldDuration);
        bannerSequence.Append(bannerText.DOFade(0f, bannerFadeDuration));
    }

    private void LateUpdate()
    {
        GameManager game = GameManager.Instance;
        PlayerController player = PlayerController.Instance;

        if (game != null)
        {
            SetFill(xpFill, game.XpRatio);

            if (levelText != null) levelText.text = "레벨 " + game.Level;
            if (waveText != null) waveText.text = BuildWaveLabel(game);

            if (statsText != null)
            {
                int minutes = Mathf.FloorToInt(game.Elapsed / 60f);
                int seconds = Mathf.FloorToInt(game.Elapsed % 60f);
                statsText.text = string.Format("{0:00}:{1:00}   처치 {2}", minutes, seconds, game.Kills);
            }
        }

        if (player == null) return;

        float ratio = player.HpRatio;
        SetFill(hpFill, ratio);

        if (hpFillImage != null) hpFillImage.color = ratio <= hpLowRatio ? hpLowColor : hpColor;
        if (hpText != null) hpText.text = Mathf.CeilToInt(player.Hp) + " / " + Mathf.RoundToInt(player.MaxHp);
    }

    private static string BuildWaveLabel(GameManager game)
    {
        switch (game.State)
        {
            case GameManager.GameState.Playing:
                return string.Format("웨이브 {0}/{1}   {2:0}초", game.Wave, game.TotalWaves, Mathf.Max(0f, game.StateTimeLeft));

            case GameManager.GameState.Break:
                return string.Format("다음 웨이브까지 {0:0}초", Mathf.Max(0f, game.StateTimeLeft));

            case GameManager.GameState.FinalSweep:
                return "남은 적 " + EnemyRegistry.Count;

            case GameManager.GameState.Endless:
                // 무한 모드에는 웨이브 개념이 없다. 단계만 보여준다.
                return string.Format("무한 모드   {0}단계", game.EndlessStep + 1);

            default:
                return "";
        }
    }

    // 스프라이트 없이 채움을 표현하려고 anchorMax.x를 조절한다. Image.fillAmount는 스프라이트를 요구한다.
    private static void SetFill(RectTransform fill, float ratio)
    {
        if (fill == null) return;

        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }
}
