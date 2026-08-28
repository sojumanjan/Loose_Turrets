// 맵을 둘러싼 사각형 둘레를 8구역으로 나누는 규칙 한 곳.
// 스포너는 스폰 링 크기로, 경고 표시는 아레나 크기로 같은 규칙을 쓴다.
//
//         1     2
//      ┌─────┬─────┐
//    8 │           │ 3
//      ├           ┤
//    7 │           │ 4
//      └─────┴─────┘
//         6     5

using UnityEngine;

public static class SpawnZones
{
    public const int Count = 8;

    /// <summary>구역 번호(1~8)에 해당하는 선분의 양 끝점. 시계방향, 왼쪽 위가 1.</summary>
    public static void GetSegment(int zone, Vector2 halfSize, out Vector3 a, out Vector3 b)
    {
        float hx = halfSize.x;
        float hz = halfSize.y;

        switch (Mathf.Clamp(zone, 1, Count))
        {
            case 1: a = new Vector3(-hx, 0f, hz); b = new Vector3(0f, 0f, hz); break;     // 위 왼쪽
            case 2: a = new Vector3(0f, 0f, hz); b = new Vector3(hx, 0f, hz); break;      // 위 오른쪽
            case 3: a = new Vector3(hx, 0f, hz); b = new Vector3(hx, 0f, 0f); break;      // 오른쪽 위
            case 4: a = new Vector3(hx, 0f, 0f); b = new Vector3(hx, 0f, -hz); break;     // 오른쪽 아래
            case 5: a = new Vector3(hx, 0f, -hz); b = new Vector3(0f, 0f, -hz); break;    // 아래 오른쪽
            case 6: a = new Vector3(0f, 0f, -hz); b = new Vector3(-hx, 0f, -hz); break;   // 아래 왼쪽
            case 7: a = new Vector3(-hx, 0f, -hz); b = new Vector3(-hx, 0f, 0f); break;   // 왼쪽 아래
            default: a = new Vector3(-hx, 0f, 0f); b = new Vector3(-hx, 0f, hz); break;   // 8: 왼쪽 위
        }
    }

    /// <summary>구역 선분의 가운데 점. 경고 아이콘을 놓을 자리.</summary>
    public static Vector3 GetCenter(int zone, Vector2 halfSize)
    {
        Vector3 a, b;
        GetSegment(zone, halfSize, out a, out b);
        return (a + b) * 0.5f;
    }
}
