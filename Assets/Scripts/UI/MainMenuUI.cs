// 시작 화면. UI는 씬의 Canvas에 직접 배치하고, 이 스크립트는 참조만 받아 동작시킨다.
// 문구와 배치는 인스펙터/씬에서 자유롭게 고치면 된다.

using DG.Tweening;
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
    [SerializeField] private Button exitButton;

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
    }

    private void Update()
    {
        if (!IsOpen) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame)
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

        if (box == null) return;

        // Time.timeScale이 0이므로 반드시 unscaled로 돌려야 애니메이션이 재생된다.
        popTween?.Kill();
        box.localScale = Vector3.one * popFromScale;
        popTween = box.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
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
