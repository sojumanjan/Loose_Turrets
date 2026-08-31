// 화면이 커질 때 그리는 픽셀 수를 제한한다. 두 가지를 같이 시도한다.
//
// 1) Screen.SetResolution 으로 캔버스(백버퍼) 자체를 줄인다. 먹히면 블릿/UI/브라우저 합성까지 같이 줄어 제일 좋다.
//    다만 WebGL 기본 템플릿은 전체화면에 들어갈 때 캔버스를 모니터 크기로 되돌려버려서 무시될 수 있다.
// 2) 그게 무시되면 URP Render Scale 로 3D 렌더 타겟만이라도 줄인다.
//
// 2번은 캔버스가 안 줄어드는 만큼 효과가 제한적이지만(실측 전체화면 29 FPS), 아무것도 안 하는 것(19 FPS)보다 낫다.
// Render Scale 은 언제나 "지금 실제 캔버스 크기" 기준으로 다시 계산하므로,
// 1번이 성공해서 캔버스가 작아지면 2번은 저절로 1.0으로 돌아온다. 두 번 줄어드는 일은 없다.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RenderScaleLimiter : MonoBehaviour
{
    [Tooltip("그리는 픽셀 수 상한. 640000이면 대략 1067x600. 창모드(960x600 = 57.6만)와 비슷하게 둔다.")]
    [Min(100000)] [SerializeField] private int maxPixels = 640000;

    [Tooltip("Render Scale 하한. 너무 낮추면 눈에 띄게 뿌옇다.")]
    [Range(0.3f, 1f)] [SerializeField] private float minScale = 0.5f;

    [Tooltip("캔버스 축소도 시도할지. 무시되면 아무 일도 안 일어나므로 켜둬도 손해는 없다.")]
    [SerializeField] private bool tryResizeCanvas = true;

    [Tooltip("캔버스를 너무 작게 만들면 글자를 못 읽는다. 가로세로 각각 이 아래로는 안 내려간다.")]
    [Min(160)] [SerializeField] private int minSide = 480;

    private Vector2Int seen = new Vector2Int(-1, -1);
    private Vector2Int requested = new Vector2Int(-1, -1);

    // Render Scale 은 씬이 아니라 URP 에셋 값이라, 에디터에서 돌리면 에셋이 실제로 바뀐다. 끝날 때 되돌린다.
    private float originalScale = -1f;

    private static UniversalRenderPipelineAsset Urp =>
        GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

    private void OnEnable()
    {
        Evaluate();
    }

    private void OnDisable()
    {
        if (originalScale < 0f) return;

        UniversalRenderPipelineAsset urp = Urp;
        if (urp != null) urp.renderScale = originalScale;
    }

    private void Update()
    {
        Evaluate();
    }

    private void Evaluate()
    {
        Vector2Int now = new Vector2Int(Screen.width, Screen.height);

        // 크기가 그대로면 할 일이 없다. 매 프레임 도는 자리라 대부분 여기서 빠진다.
        if (now == seen) return;
        seen = now;

        ApplyRenderScale(now);

        if (tryResizeCanvas) TryShrinkCanvas(now);
    }

    /// <summary>지금 캔버스 크기를 기준으로 렌더 타겟 배율을 다시 잡는다.</summary>
    private void ApplyRenderScale(Vector2Int size)
    {
        UniversalRenderPipelineAsset urp = Urp;
        if (urp == null) return;

        if (originalScale < 0f) originalScale = urp.renderScale;

        long pixels = (long)size.x * size.y;
        if (pixels <= 0) return;

        // 픽셀 수를 상한에 맞추려면 가로세로를 각각 제곱근만큼 줄여야 한다.
        float scale = pixels <= maxPixels ? 1f : Mathf.Sqrt(maxPixels / (float)pixels);

        urp.renderScale = Mathf.Clamp(scale, minScale, 1f);
    }

    private void TryShrinkCanvas(Vector2Int size)
    {
        // 방금 우리가 요청해서 생긴 크기라면 또 건드리지 않는다.
        if (size == requested) return;

        long pixels = (long)size.x * size.y;
        if (pixels <= maxPixels) return;

        float scale = Mathf.Sqrt(maxPixels / (float)pixels);

        Vector2Int target = new Vector2Int(
            Mathf.Max(minSide, Mathf.RoundToInt(size.x * scale)),
            Mathf.Max(minSide, Mathf.RoundToInt(size.y * scale)));

        if (target == size) return;

        requested = target;
        Screen.SetResolution(target.x, target.y, Screen.fullScreen);
    }
}
