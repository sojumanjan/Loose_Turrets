// 화면 비율이 어떻든 아레나 좌우 벽이 잘리지 않게 직교 카메라 크기를 맞춘다.
// 직교 카메라는 orthographicSize가 세로 절반만 정하고 가로는 화면 비율이 정하므로,
// 화면이 좁아지면 세로를 키워서 필요한 가로 폭을 되찾는다.

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [Tooltip("항상 화면에 들어와야 하는 가로 절반 폭(월드 단위). 아레나 벽 바깥면이 ±13.125라 여유를 조금 준 값이다.")]
    [Min(0.1f)] [SerializeField] private float requiredHalfWidth = 13.5f;

    [Tooltip("기본 세로 절반 높이. 화면이 충분히 넓으면 이 값을 그대로 쓴다.")]
    [Min(0.1f)] [SerializeField] private float baseSize = 8f;

    [Tooltip("세로를 여기까지만 키운다. 더 키우면 적이 스폰되는 자리(z = ±11)가 화면에 들어온다.")]
    [Min(0.1f)] [SerializeField] private float maxSize = 9.2f;

    private Camera cam;
    private int lastWidth;
    private int lastHeight;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    // 전체화면 토글이나 브라우저 창 크기 변경은 아무 때나 일어난다. 크기가 바뀐 프레임에만 다시 계산한다.
    private void Update()
    {
        if (Screen.width == lastWidth && Screen.height == lastHeight) return;

        Apply();
    }

    private void Apply()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return;

        lastWidth = Screen.width;
        lastHeight = Screen.height;

        float aspect = cam.aspect;
        if (aspect < 0.01f) return;

        // 이 가로 폭을 담으려면 세로가 얼마여야 하는가.
        float needed = requiredHalfWidth / aspect;

        cam.orthographicSize = Mathf.Clamp(Mathf.Max(baseSize, needed), baseSize, maxSize);
    }
}
