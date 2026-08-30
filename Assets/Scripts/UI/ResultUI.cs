// 게임 오버 결과창. 최종 성적을 보여주고 재시작 버튼을 준다.
// UI는 씬의 ResultCanvas에 직접 배치하고, 이 스크립트는 참조만 받아 채운다. 배치는 씬에서 자유롭게 고치면 된다.

using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultUI : MonoBehaviour
{
    /// <summary>피해량 표의 한 줄. 이름과 수치를 따로 둬야 좌우 정렬이 맞는다.</summary>
    [System.Serializable]
    public class DamageSlot
    {
        [Tooltip("이 줄 전체. 안 쓰는 포탑 줄은 이걸 꺼서 숨긴다.")]
        public GameObject Root;

        [Tooltip("칸 배경. 해당 포탑의 카드 색으로 물든다. 비워도 된다.")]
        public Image Background;

        [Tooltip("이름 왼쪽에 붙는 포탑 아이콘. TurretDef의 Card Icon을 그린다. 비워도 된다.")]
        public Image Icon;

        public TextMeshProUGUI NameText;
        public TextMeshProUGUI ValueText;
    }

    public static ResultUI Instance { get; private set; }

    [Header("씬 참조")]
    [Tooltip("결과창 전체를 켜고 끄는 오브젝트. 보통 반투명 배경 패널.")]
    [SerializeField] private GameObject panel;

    [Tooltip("등장할 때 튀어나오는 애니메이션이 걸릴 대상. 비워두면 애니메이션만 생략된다.")]
    [SerializeField] private RectTransform box;

    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("제목 아래 색 띠. 클리어/오버에 따라 색이 바뀐다. 비워도 된다.")]
    [SerializeField] private Image accentStrip;

    [Header("왼쪽 - 포탑별 피해량")]
    [Tooltip("포탑 종류 수만큼 미리 만들어 둔 칸. 피해를 넣은 포탑만 많은 순으로 앞칸부터 채우고 나머지는 숨긴다.")]
    [SerializeField] private DamageSlot[] damageSlots;

    [Tooltip("칸 배경을 포탑 카드 색으로 물들이는 진하기. 0이면 색을 칠하지 않는다.")]
    [SerializeField, Range(0f, 1f)] private float slotBackgroundAlpha = 0.3f;

    [Tooltip("한 판 내내 한 발도 안 쏜 경우 왼쪽 칸을 통째로 숨긴다. 비워도 된다.")]
    [SerializeField] private GameObject damagePanel;

    [Header("오른쪽 - 성적")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("버튼")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [SerializeField] private TextMeshProUGUI hintText;

    [Header("색")]
    [SerializeField] private Color gameOverColor = new Color(0.95f, 0.35f, 0.35f);

    [Header("연출")]
    [SerializeField] private float popDuration = 0.32f;
    [SerializeField] private float popFromScale = 0.8f;

    public bool IsOpen { get; private set; }

    // 매번 리스트를 새로 만들 이유는 없다.
    private static readonly List<TurretDef> damageRanking = new List<TurretDef>(8);

    private void Awake()
    {
        Instance = this;

        if (panel == null)
        {
            Debug.LogError("[ResultUI] panel 이 비어 있습니다. 씬의 결과창 패널을 연결하세요.", this);
            return;
        }

        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (menuButton != null) menuButton.onClick.AddListener(BackToMainMenu);

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>클리어가 없는 게임이라 결과창은 사망으로만 뜬다.</summary>
    public void Show()
    {
        if (panel == null) return;

        IsOpen = true;
        panel.SetActive(true);

        if (titleText != null)
        {
            titleText.text = "게임 오버";
            titleText.color = gameOverColor;
        }

        if (accentStrip != null) accentStrip.color = gameOverColor;

        if (statsText != null) statsText.text = BuildStats();
        FillDamage();

        if (hintText != null) hintText.text = "R 키로 다시하기";

        if (box == null) return;

        // Time.timeScale이 0이므로 반드시 unscaled로 돌려야 애니메이션이 재생된다.
        box.DOKill();
        box.localScale = Vector3.one * popFromScale;
        box.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard();
    }

    private void Keyboard()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.rKey.wasPressedThisFrame) Restart();
    }

    // ---------------------------------------------------------------- 내용 채우기

    private static string BuildStats()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return "";

        int minutes = Mathf.FloorToInt(game.Elapsed / 60f);
        int seconds = Mathf.FloorToInt(game.Elapsed % 60f);

        // 끝나는 웨이브가 없으므로 총 개수를 붙이지 않는다. 어디까지 버텼는지가 곧 기록이다.
        string first = "최고기록  " + game.Wave + " 웨이브";

        return string.Format(
            "{0}\n시간      {1:00}:{2:00}\n처치      {3}\n레벨      {4}\n포탑      {5}",
            first, minutes, seconds, game.Kills, game.Level, TurretBase.All.Count);
    }

    /// <summary>피해를 넣은 포탑만 많은 순으로 앞칸부터 채운다. 0인 포탑은 건너뛰고 남는 칸은 숨긴다.</summary>
    private void FillDamage()
    {
        if (damageSlots == null || damageSlots.Length == 0) return;

        DamageStats.FillRanking(damageRanking);
        float total = DamageStats.Total;

        bool any = damageRanking.Count > 0 && total > 0f;
        if (damagePanel != null) damagePanel.SetActive(any);

        int used = 0;

        if (any)
        {
            for (int i = 0; i < damageRanking.Count && used < damageSlots.Length; i++)
            {
                TurretDef def = damageRanking[i];
                float amount = DamageStats.Get(def);

                // 한 번도 안 쓴 포탑은 칸을 차지하지 않는다.
                if (def == null || amount <= 0f) continue;

                DamageSlot slot = damageSlots[used];
                used++;

                if (slot == null) continue;
                if (slot.Root != null) slot.Root.SetActive(true);

                // 칸 배경을 그 포탑의 카드 색으로 물들인다. 레벨업 카드와 같은 색이라 한눈에 이어진다.
                if (slot.Background != null)
                {
                    Color tint = def.CardColor;
                    tint.a = slotBackgroundAlpha;
                    slot.Background.color = tint;
                }

                // 아이콘이 없는 포탑은 그 칸만 아이콘을 숨긴다. 글자는 그대로 나온다.
                if (slot.Icon != null)
                {
                    slot.Icon.sprite = def.CardIcon;
                    slot.Icon.enabled = def.CardIcon != null;
                }

                if (slot.NameText != null) slot.NameText.text = def.DisplayName;
                if (slot.ValueText != null)
                    slot.ValueText.text = Mathf.RoundToInt(amount).ToString("N0") + "대미지 ("
                        + Mathf.RoundToInt(amount / total * 100f) + "%)";
            }
        }

        // 남는 칸은 비워둔다.
        for (int i = used; i < damageSlots.Length; i++)
        {
            if (damageSlots[i] != null && damageSlots[i].Root != null) damageSlots[i].Root.SetActive(false);
        }
    }

    // ---------------------------------------------------------------- 버튼

    private void Close()
    {
        IsOpen = false;

        if (box != null) box.DOKill();
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>메뉴를 건너뛰고 곧바로 새 판을 시작한다.</summary>
    private void Restart()
    {
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        Close();

        if (GameManager.Instance != null) GameManager.Instance.RestartAndPlay();
        else Time.timeScale = 1f;
    }

    /// <summary>메인 메뉴로 돌아간다.</summary>
    private void BackToMainMenu()
    {
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        Close();

        if (GameManager.Instance != null) GameManager.Instance.Restart();
        else Time.timeScale = 1f;
    }
}
