// 마우스 스크린 좌표를 XZ 평면(y=0)의 월드 좌표로 바꿔주는 static 유틸. 포탑 드래그와 조준이 전부 이걸 쓴다.

using UnityEngine;
using UnityEngine.InputSystem;

public static class MouseWorld
{
    private static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);
    private static Camera cachedCamera;

    private static Camera Cam
    {
        get
        {
            if (cachedCamera == null) cachedCamera = Camera.main;
            return cachedCamera;
        }
    }

    /// <summary>마우스가 가리키는 바닥(y=0) 위의 월드 좌표. 카메라나 마우스가 없으면 Vector3.zero.</summary>
    public static Vector3 Position
    {
        get
        {
            Camera cam = Cam;
            if (cam == null || Mouse.current == null) return Vector3.zero;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            return GroundPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }
    }

    public static bool LeftPressedThisFrame => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool LeftHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
    public static bool LeftReleasedThisFrame => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;

    /// <summary>씬을 다시 로드했을 때 이전 씬의 파괴된 카메라를 계속 붙들지 않도록 초기화한다.</summary>
    public static void ResetCache()
    {
        cachedCamera = null;
    }
}
