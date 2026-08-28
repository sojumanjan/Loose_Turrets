// 아레나 경계를 정하는 단 하나의 출처. 플레이어 이동 제한, 포탑 배치 제한, 배회 적 반사, 바닥 경계선이 전부 이 값을 쓴다.
// 자식으로 붙어있는 경계선 오브젝트 4개(위/아래/왼/오른)를 값에 맞춰 자동으로 배치한다.

using UnityEngine;

[ExecuteAlways]
public class ArenaBounds : MonoBehaviour
{
    // 씬에 이 컴포넌트가 없어도 게임이 돌아가도록 기본값을 들고 있는다.
    private static Vector2 halfSize = new Vector2(13f, 6.2f);

    /// <summary>아레나 절반 크기. x는 좌우, y는 앞뒤(z축)를 뜻한다.</summary>
    public static Vector2 HalfSize => halfSize;

    // 여기를 벗어난 적은 즉시 정리한다. 스폰 링(기본 17 x 11)보다 반드시 커야 한다.
    private static Vector2 killHalfSize = new Vector2(26f, 20f);

    public static Vector2 KillHalfSize => killHalfSize;

    /// <summary>맵 밖으로 완전히 새어나갔는가. 이 적을 그대로 두면 마지막 웨이브가 끝나지 않는다.</summary>
    public static bool IsOutsideKillBounds(Vector3 position)
    {
        return Mathf.Abs(position.x) > killHalfSize.x || Mathf.Abs(position.z) > killHalfSize.y;
    }

    [Header("아레나 크기 (여기 하나만 고치면 전부 따라온다)")]
    [SerializeField] private Vector2 halfExtents = new Vector2(13f, 6.2f);

    [Header("맵 이탈 처리")]
    [Tooltip("이 사각형을 벗어난 적은 즉시 사라진다. 적이 스폰되는 링보다 반드시 커야 한다. (스포너 기본값 17 x 11)")]
    [SerializeField] private Vector2 killHalfExtents = new Vector2(26f, 20f);

    [Header("바닥 경계선")]
    [SerializeField] private float lineThickness = 0.25f;
    [Tooltip("바닥(y=0)보다 살짝 위여야 파묻히지 않는다.")]
    [SerializeField] private float lineY = 0.01f;

    private void Awake()
    {
        Apply();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void Apply()
    {
        halfSize = new Vector2(Mathf.Max(1f, halfExtents.x), Mathf.Max(1f, halfExtents.y));

        // 킬 경계는 아레나보다 작을 수 없다.
        killHalfSize = new Vector2(Mathf.Max(halfSize.x + 1f, killHalfExtents.x),
                                   Mathf.Max(halfSize.y + 1f, killHalfExtents.y));

        LayoutEdges();
    }

    /// <summary>자식 경계선 4개를 현재 크기에 맞춰 옮긴다. 자식이 모자라면 아무것도 하지 않는다.</summary>
    private void LayoutEdges()
    {
        if (transform.childCount < 4) return;

        float hx = halfSize.x;
        float hz = halfSize.y;
        float t = Mathf.Max(0.02f, lineThickness);

        //          위            아래           왼            오른
        Vector3[] positions =
        {
            new Vector3(0f, lineY, hz),
            new Vector3(0f, lineY, -hz),
            new Vector3(-hx, lineY, 0f),
            new Vector3(hx, lineY, 0f)
        };

        Vector3[] scales =
        {
            new Vector3(hx * 2f + t, 0.02f, t),
            new Vector3(hx * 2f + t, 0.02f, t),
            new Vector3(t, 0.02f, hz * 2f),
            new Vector3(t, 0.02f, hz * 2f)
        };

        for (int i = 0; i < 4; i++)
        {
            Transform edge = transform.GetChild(i);
            edge.localPosition = positions[i];
            edge.localScale = scales[i];
            edge.localRotation = Quaternion.identity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.6f);
        Gizmos.DrawWireCube(transform.position, new Vector3(halfExtents.x * 2f, 0.1f, halfExtents.y * 2f));

        // 킬 경계. 스폰 링이 이 안에 들어와야 한다.
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.55f);
        Gizmos.DrawWireCube(transform.position, new Vector3(killHalfExtents.x * 2f, 0.1f, killHalfExtents.y * 2f));
    }
}
