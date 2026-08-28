// 이번 웨이브에 적이 들어올 구역을 미리 알려주는 경고 표시.
// 스폰 링은 카메라 밖이라 거기 그리면 안 보인다. 같은 8분할을 아레나 경계에 투영해 화면 안에 그린다.

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class SpawnZoneWarning : MonoBehaviour
{
    public static SpawnZoneWarning Instance { get; private set; }

    [Header("표시")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.25f);
    [SerializeField] private float lineWidth = 0.35f;

    [Tooltip("아레나 경계보다 살짝 안쪽에 그린다. 경계선과 겹치면 잘 안 보인다.")]
    [SerializeField] private float inset = 0.5f;

    [Tooltip("바닥보다 살짝 위여야 파묻히지 않는다.")]
    [SerializeField] private float height = 0.05f;

    [Header("연출")]
    [SerializeField] private float blinkDuration = 0.45f;
    [SerializeField] private int blinkCount = 4;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.95f;

    private readonly List<LineRenderer> markers = new List<LineRenderer>(SpawnZones.Count);
    private readonly List<Tween> tweens = new List<Tween>(SpawnZones.Count);

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>지정한 구역들을 깜빡인다. zones가 비어 있으면 8구역 전부를 표시한다.</summary>
    public void Warn(IList<int> zones)
    {
        HideAll();

        if (zones == null || zones.Count == 0)
        {
            for (int zone = 1; zone <= SpawnZones.Count; zone++) Blink(zone);
            return;
        }

        for (int i = 0; i < zones.Count; i++) Blink(zones[i]);
    }

    /// <summary>구역 하나만 알린다. 무한 모드에서 새 구역이 열릴 때 쓴다.</summary>
    public void WarnSingle(int zone)
    {
        Blink(zone);
    }

    private void Blink(int zone)
    {
        int index = Mathf.Clamp(zone, 1, SpawnZones.Count) - 1;
        if (index < 0 || index >= markers.Count) return;

        LineRenderer line = markers[index];
        RefreshSegment(line, index + 1);

        tweens[index]?.Kill();

        line.enabled = true;
        SetAlpha(line, maxAlpha);

        // timeScale이 0인 순간(레벨업 등)에도 보이도록 unscaled로 돌린다.
        Tween tween = DOVirtual.Float(maxAlpha, minAlpha, blinkDuration, value => SetAlpha(line, value))
            .SetLoops(blinkCount * 2, LoopType.Yoyo)
            .SetUpdate(true)
            .OnComplete(() => line.enabled = false);

        tweens[index] = tween;
    }

    private void HideAll()
    {
        for (int i = 0; i < markers.Count; i++)
        {
            tweens[i]?.Kill();
            tweens[i] = null;

            if (markers[i] != null) markers[i].enabled = false;
        }
    }

    /// <summary>아레나 크기가 바뀌어도 따라가도록 표시 직전에 위치를 다시 잡는다.</summary>
    private void RefreshSegment(LineRenderer line, int zone)
    {
        Vector2 half = ArenaBounds.HalfSize;
        half = new Vector2(Mathf.Max(1f, half.x - inset), Mathf.Max(1f, half.y - inset));

        Vector3 a, b;
        SpawnZones.GetSegment(zone, half, out a, out b);

        a.y = height;
        b.y = height;

        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    private void SetAlpha(LineRenderer line, float alpha)
    {
        Color color = warningColor;
        color.a = alpha;

        line.startColor = color;
        line.endColor = color;
    }

    private void Build()
    {
        Material material = lineMaterial;

        if (material == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return;
            material = new Material(fallback);
        }

        for (int zone = 1; zone <= SpawnZones.Count; zone++)
        {
            GameObject go = new GameObject("ZoneWarning" + zone);
            go.transform.SetParent(transform, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = 2;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            markers.Add(line);
            tweens.Add(null);
        }
    }
}
