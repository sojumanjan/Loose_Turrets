// 메인 메뉴와 ESC 메뉴가 함께 여는 설정창. 패널은 하나만 두고 양쪽에서 Open()을 부른다.
// UI는 씬의 SettingsCanvas에 배치하고 이 스크립트는 참조만 받는다. 배치는 씬에서 자유롭게 고치면 된다.

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    [Header("씬 참조")]
    [SerializeField] private GameObject panel;

    [Tooltip("열릴 때 튀어나오는 애니메이션이 걸릴 대상. 비워두면 애니메이션만 생략된다.")]
    [SerializeField] private RectTransform box;

    [SerializeField] private Button closeButton;

    [Header("볼륨")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Tooltip("슬라이더 옆에 퍼센트를 띄운다. 비워도 된다.")]
    [SerializeField] private TextMeshProUGUI masterValueText;
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Header("화면")]
    [Tooltip("켜면 창모드, 끄면 전체화면.")]
    [SerializeField] private Toggle windowedToggle;

    [Header("연출")]
    [SerializeField] private float popDuration = 0.24f;
    [SerializeField] private float popFromScale = 0.88f;

    public bool IsOpen { get; private set; }

    // 슬라이더 값을 코드로 되돌릴 때 onValueChanged 가 다시 불려 무한히 되도는 것을 막는다.
    private bool applying;

    private SliderReleaseNotifier sfxRelease;

    private void Awake()
    {
        Instance = this;

        if (panel == null)
        {
            Debug.LogError("[SettingsUI] panel 이 비어 있습니다. 씬의 설정 패널을 연결하세요.", this);
            return;
        }

        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        if (windowedToggle != null) windowedToggle.onValueChanged.AddListener(OnWindowedChanged);

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);

            // 미리듣기는 드래그 도중이 아니라 손을 뗀 순간 한 번만 낸다.
            sfxRelease = sfxSlider.GetComponent<SliderReleaseNotifier>();
            if (sfxRelease == null) sfxRelease = sfxSlider.gameObject.AddComponent<SliderReleaseNotifier>();

            sfxRelease.Released += PlaySfxPreview;
        }

        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (sfxRelease != null) sfxRelease.Released -= PlaySfxPreview;
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- 열고 닫기

    public void Open()
    {
        if (panel == null || IsOpen) return;

        IsOpen = true;
        panel.SetActive(true);

        Refresh();
        SfxManager.Play(SfxManager.Common?.ButtonClick);

        if (box == null) return;

        // 메뉴에서 열면 timeScale이 0이므로 반드시 unscaled로 돌려야 재생된다.
        box.DOKill();
        box.localScale = Vector3.one * popFromScale;
        box.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;

        if (box != null) box.DOKill();
        if (panel != null) panel.SetActive(false);

        SfxManager.Play(SfxManager.Common?.ButtonClick);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // ESC로도 닫는다. 이 창이 열려 있는 동안에는 일시정지 메뉴가 ESC를 받지 않는다.
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    /// <summary>지금 저장된 값을 위젯에 되비춘다. 열 때마다 부른다.</summary>
    private void Refresh()
    {
        applying = true;

        if (masterSlider != null) masterSlider.value = SoundSettings.Master;
        if (bgmSlider != null) bgmSlider.value = SoundSettings.Bgm;
        if (sfxSlider != null) sfxSlider.value = SoundSettings.Sfx;
        if (windowedToggle != null) windowedToggle.isOn = !Screen.fullScreen;

        applying = false;

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (masterValueText != null) masterValueText.text = Percent(SoundSettings.Master);
        if (bgmValueText != null) bgmValueText.text = Percent(SoundSettings.Bgm);
        if (sfxValueText != null) sfxValueText.text = Percent(SoundSettings.Sfx);
    }

    private static string Percent(float ratio) => Mathf.RoundToInt(ratio * 100f) + "%";

    // ---------------------------------------------------------------- 위젯 콜백

    private void OnMasterChanged(float value)
    {
        if (applying) return;

        SoundSettings.SetMaster(value);
        UpdateLabels();
    }

    private void OnBgmChanged(float value)
    {
        if (applying) return;

        SoundSettings.SetBgm(value);
        UpdateLabels();
    }

    private void OnSfxChanged(float value)
    {
        if (applying) return;

        SoundSettings.SetSfx(value);
        UpdateLabels();
    }

    /// <summary>슬라이더에서 손을 뗄 때 한 번. 방금 정한 크기를 귀로 확인시켜 준다.</summary>
    private void PlaySfxPreview()
    {
        if (!IsOpen) return;

        SfxManager.Play(SfxManager.Common?.ButtonClick);
    }

    private void OnWindowedChanged(bool windowed)
    {
        if (applying) return;

        Screen.fullScreen = !windowed;
    }
}
