// 성능 병목이 CPU인지 GPU인지 화면에서 바로 읽으려고 띄우는 진단 오버레이. F5로 켜고 끈다.
//
// FPS만으로는 원인을 못 가린다. FrameTimingManager가 주는 CPU/GPU 프레임 시간을 나란히 보여준다.
//   CPU ~= 전체     -> 로직/드로우콜 문제. 해상도를 낮춰도 안 나아진다.
//   GPU ~= 전체     -> 픽셀/오버드로우 문제. 해상도를 낮추면 나아진다.
// Player Settings의 Frame Timing Stats가 켜져 있어야 값이 들어온다. WebGL에서 GPU 값은 안 나올 수 있다.
//
// 배포할 때는 이 컴포넌트를 꺼두면 된다.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerfHud : MonoBehaviour
{
    [Tooltip("시작할 때부터 켜둘지. 웹빌드에서 F5를 누르기 어렵다면 켜두고 빌드한다.")]
    [SerializeField] private bool startVisible = true;

    [Tooltip("FPS를 평균 내는 시간(초). 짧으면 숫자가 튀고 길면 반응이 느리다.")]
    [Min(0.1f)] [SerializeField] private float sampleWindow = 0.5f;

    [Tooltip("FPS를 브라우저 콘솔(F12)에도 찍는다. 이 오버레이 자체가 OnGUI라 공짜가 아니므로, "
             + "F5로 오버레이를 끈 상태에서 콘솔로 재는 게 가장 정확한 숫자다.")]
    [SerializeField] private bool logToConsole = true;

    private bool visible;
    private int frames;
    private float elapsed;
    private float fps;

    private double cpuMs;
    private double gpuMs;
    private bool timingsAvailable;

    private GUIStyle style;
    private readonly FrameTiming[] timings = new FrameTiming[1];

    private void Awake()
    {
        visible = startVisible;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f5Key.wasPressedThisFrame) visible = !visible;

        // 일시정지 중에도 프레임은 계속 도므로 unscaled로 잰다.
        frames++;
        elapsed += Time.unscaledDeltaTime;

        if (elapsed < sampleWindow) return;

        fps = frames / elapsed;
        frames = 0;
        elapsed = 0f;

        SampleTimings();

        // 오버레이를 끈 상태에서도 숫자를 볼 수 있어야 오버레이 자체의 비용을 잴 수 있다.
        if (logToConsole)
            Debug.Log($"FPS {fps:0}  ({1000f / Mathf.Max(0.01f, fps):0.0}ms)   "
                      + $"캔버스 {Screen.width}x{Screen.height}   적 {EnemyRegistry.Count}");
    }

    private void SampleTimings()
    {
        FrameTimingManager.CaptureFrameTimings();

        uint count = FrameTimingManager.GetLatestTimings(1, timings);
        timingsAvailable = count > 0;

        if (!timingsAvailable) return;

        cpuMs = timings[0].cpuFrameTime;
        gpuMs = timings[0].gpuFrameTime;
    }

    private void OnGUI()
    {
        if (!visible) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
        }

        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        int cw = Screen.width;
        int ch = Screen.height;
        float scale = urp != null ? urp.renderScale : 1f;

        int rw = Mathf.Max(1, Mathf.RoundToInt(cw * scale));
        int rh = Mathf.Max(1, Mathf.RoundToInt(ch * scale));

        float frameMs = fps > 0.01f ? 1000f / fps : 0f;

        // 보간 문자열 안에서 따옴표를 또 쓰면 파서가 헷갈리므로 미리 만들어 둔다.
        string gpuText = gpuMs > 0.01 ? gpuMs.ToString("0.0") + "ms" : "측정불가";
        string split = timingsAvailable
            ? "CPU " + cpuMs.ToString("0.0") + "ms   GPU " + gpuText
            : "CPU/GPU 측정불가 (Frame Timing Stats 꺼짐)";

        string text =
            $"FPS      {fps:0}      프레임 {frameMs:0.0}ms\n" +
            $"{split}\n" +
            $"캔버스   {cw} x {ch}   ({cw * (long)ch / 1000000f:0.00}M px)\n" +
            $"렌더     {rw} x {rh}   (scale {scale:0.00})\n" +
            $"적 {EnemyRegistry.Count}       포탑 {TurretBase.All.Count}\n" +
            $"[F5] 끄기";

        Vector2 size = style.CalcSize(new GUIContent(text));
        var box = new Rect(8f, 8f, size.x + 16f, size.y + 12f);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(new Rect(box.x + 8f, box.y + 6f, size.x, size.y), text, style);
    }
}
