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
    [Tooltip("체력 칸 이미지들. 왼쪽부터 순서대로 넣는다. 남은 칸 수만큼 켜지고 나머지는 빈 칸 색이 된다.")]
    [SerializeField] private Image[] hpCells;

    [Tooltip("잃은 칸의 색.")]
    [SerializeField] private Color hpEmptyColor = new Color(0.22f, 0.24f, 0.3f, 1f);

    [SerializeField] private Color hpColor = new Color(0.35f, 0.85f, 0.45f);
    [SerializeField] private Color hpLowColor = new Color(0.9f, 0.3f, 0.3f);
    [Tooltip("이 비율 아래로 내려가면 체력 바가 빨개진다.")]
    [SerializeField, Range(0f, 1f)] private float hpLowRatio = 0.3f;

    [Header("진행 표시")]
    [SerializeField] private TextMeshProUGUI waveText;
    [Tooltip("경과 시간. 처치 수와 한 덩어리로 두면 자릿수가 바뀔 때마다 시간이 밀린다.")]
    [SerializeField] private TextMeshProUGUI timeText;

    [SerializeField] private TextMeshProUGUI killsText;

    [Header("보스 경고 (화면 중앙)")]
    [Tooltip("경고 전체를 켜고 끄는 오브젝트. 스프라이트와 글씨를 자식으로 둔다.")]
    [SerializeField] private GameObject bossWarningRoot;

    [SerializeField] private TextMeshProUGUI bossWarningText;

    [Tooltip("경고가 한 번 커졌다 작아지는 데 걸리는 시간.")]
    [Min(0.1f)] [SerializeField] private float bossWarningPulse = 0.6f;

    [SerializeField] private float bossWarningPulseScale = 1.12f;

    [Header("보스 체력바 (화면 상단)")]
    [Tooltip("체력바 전체를 켜고 끄는 오브젝트. 보스전이 아닐 때는 꺼진다.")]
    [SerializeField] private GameObject bossHpRoot;

    [Tooltip("채워지는 부분. anchorMax.x 로 채운다.")]
    [SerializeField] private RectTransform bossHpFill;

    [SerializeField] private Image bossHpFillImage;
    [SerializeField] private Color bossHpColor = new Color(0.9f, 0.3f, 0.3f);

    [Tooltip("2페이즈에 들어가면 이 색으로 바뀐다.")]
    [SerializeField] private Color bossHpPhase2Color = new Color(1f, 0.6f, 0.2f);

    [Header("배너")]
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private float bannerHoldDuration = 1.1f;
    [SerializeField] private float bannerFadeDuration = 0.45f;
    [SerializeField] private float bannerPopFromScale = 0.7f;


    private Sequence bannerSequence;

    // 직전에 화면에 쓴 값. TextMeshPro는 같은 문자열을 다시 넣어도 메시를 새로 만들고 Canvas를 더티로 표시한다.
    // Screen Space Overlay 캔버스가 매 프레임 재빌드되면 적 수와 무관한 고정비용이 그대로 프레임에 얹힌다.
    // 그래서 값이 실제로 바뀐 프레임에만 쓴다.
    private int shownLevel = int.MinValue;
    private int shownKills = int.MinValue;
    private int shownSeconds = int.MinValue;
    private float shownXpRatio = -1f;
    private string shownWaveLabel;
    private int shownHearts = int.MinValue;
    private int shownMaxHearts = int.MinValue;
    private bool shownHpLow;
    private float shownBossRatio = -1f;
    private bool shownBossPhase2;
    private Sequence bossWarningSequence;

    private void Awake()
    {
        Instance = this;

        // 예전에는 이 값을 매 프레임 다시 넣었다. 한 번만 넣으면 결과가 같다.
        if (waveText != null) waveText.fontSize = 50;

        if (bannerText != null) bannerText.alpha = 0f;
        if (bossWarningRoot != null) bossWarningRoot.SetActive(false);
        if (bossHpRoot != null) bossHpRoot.SetActive(false);
    }

    // ---------------- 보스 ----------------

    /// <summary>보스가 오기 전 쉬는 시간 동안 화면 중앙에 크게 알린다. 구역 경고 대신 뜬다.
    /// 문구는 보스 프리팹이 들고 있으므로 호출하는 쪽이 넘겨준다.</summary>
    public void ShowBossWarning(string message)
    {
        if (bossWarningRoot == null) return;

        bossWarningRoot.SetActive(true);
        if (bossWarningText != null) bossWarningText.text = message;

        bossWarningSequence?.Kill();

        // 쉬는 시간은 실제 시간으로 흐르므로 여기도 스케일 시간을 그대로 쓴다.
        // unscaled 로 돌리면 레벨업으로 멈춘 동안에도 혼자 뛰어서 어긋난다.
        Transform t = bossWarningRoot.transform;
        t.localScale = Vector3.one;

        bossWarningSequence = DOTween.Sequence()
            .Append(t.DOScale(bossWarningPulseScale, bossWarningPulse * 0.5f).SetEase(Ease.OutQuad))
            .Append(t.DOScale(1f, bossWarningPulse * 0.5f).SetEase(Ease.InQuad))
            .SetLoops(-1);
    }

    public void HideBossWarning()
    {
        bossWarningSequence?.Kill();
        bossWarningSequence = null;

        if (bossWarningRoot == null) return;

        bossWarningRoot.transform.localScale = Vector3.one;
        bossWarningRoot.SetActive(false);
    }

    /// <summary>보스 체력바. 보스가 없으면 통째로 숨긴다.</summary>
    private void UpdateBossHp()
    {
        BossEnemy boss = BossEnemy.Current;
        bool show = boss != null && boss.IsAlive;

        if (bossHpRoot != null && bossHpRoot.activeSelf != show) bossHpRoot.SetActive(show);
        if (!show)
        {
            shownBossRatio = -1f;
            return;
        }

        // 값이 바뀐 프레임에만 손댄다. 매 프레임 쓰면 그것만으로 캔버스가 다시 그려진다.
        if (boss.HpRatio != shownBossRatio)
        {
            shownBossRatio = boss.HpRatio;
            SetFill(bossHpFill, shownBossRatio);
        }

        if (bossHpFillImage != null && boss.InPhase2 != shownBossPhase2)
        {
            shownBossPhase2 = boss.InPhase2;
            bossHpFillImage.color = shownBossPhase2 ? bossHpPhase2Color : bossHpColor;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>웨이브 시작 등 큼직한 알림을 화면 중앙에 잠깐 띄운다.</summary>
    /// <summary>holdDuration에 0보다 큰 값을 주면 그 시간만큼 띄워둔다. 0이면 기본 유지 시간을 쓴다.</summary>
    public void ShowBanner(string message, Color color, float holdDuration = 0f)
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
        bannerSequence.AppendInterval(holdDuration > 0f ? holdDuration : bannerHoldDuration);
        bannerSequence.Append(bannerText.DOFade(0f, bannerFadeDuration));
    }

    private void LateUpdate()
    {
        GameManager game = GameManager.Instance;
        PlayerController player = PlayerController.Instance;

        UpdateBossHp();

        if (game != null)
        {
            if (game.XpRatio != shownXpRatio)
            {
                shownXpRatio = game.XpRatio;
                SetFill(xpFill, shownXpRatio);
            }

            if (levelText != null && game.Level != shownLevel)
            {
                shownLevel = game.Level;
                levelText.text = "레벨 " + shownLevel;
            }

            if (waveText != null)
            {
                // 라벨은 초 단위로만 바뀌므로 만들어 보고 같으면 버린다.
                // 문자열 하나를 만드는 비용이 캔버스를 재빌드하는 비용보다 훨씬 싸다.
                string label = BuildWaveLabel(game);
                if (label != shownWaveLabel)
                {
                    shownWaveLabel = label;
                    waveText.text = label;
                }
            }

            int totalSeconds = Mathf.FloorToInt(game.Elapsed);
            if (timeText != null && totalSeconds != shownSeconds)
            {
                shownSeconds = totalSeconds;
                timeText.text = string.Format("{0:00}:{1:00}", totalSeconds / 60, totalSeconds % 60);
            }

            if (killsText != null && game.Kills != shownKills)
            {
                shownKills = game.Kills;
                killsText.text = "처치 " + shownKills;
            }
        }

        if (player == null) return;

        float ratio = player.HpRatio;
        int hearts = Mathf.RoundToInt(player.Hp);

        // 칸 단위 체력이라 게이지를 늘였다 줄이지 않고 칸을 하나씩 껐다 켠다.
        if (hpCells != null)
        {
            int max = Mathf.RoundToInt(player.MaxHp);
            bool low = ratio <= hpLowRatio;

            // 칸 색과 활성 상태를 매 프레임 다시 쓰면 그것만으로 캔버스가 더티가 된다.
            if (hearts == shownHearts && max == shownMaxHearts && low == shownHpLow) return;

            shownHearts = hearts;
            shownMaxHearts = max;
            shownHpLow = low;

            for (int i = 0; i < hpCells.Length; i++)
            {
                if (hpCells[i] == null) continue;

                // 최대 칸보다 많이 만들어 뒀으면 남는 칸은 아예 숨긴다.
                bool exists = i < max;
                hpCells[i].gameObject.SetActive(exists);
                if (!exists) continue;

                hpCells[i].color = i < hearts
                    ? (low ? hpLowColor : hpColor)
                    : hpEmptyColor;
            }
        }
    }

    private static string BuildWaveLabel(GameManager game)
    {
        switch (game.State)
        {
            case GameManager.GameState.Playing:
                // 마지막 웨이브가 없으므로 총 개수를 붙이지 않는다. 버틴 웨이브 수가 곧 기록이다.
                return string.Format("웨이브 {0}   {1:0}초", game.Wave, Mathf.Max(0f, game.StateTimeLeft));

            case GameManager.GameState.Break:
                return string.Format("다음 웨이브까지 {0:0}초", Mathf.Max(0f, game.StateTimeLeft));

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
