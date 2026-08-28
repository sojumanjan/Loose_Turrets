// 이번 웨이브에 적이 들어올 구역을 미리 알려주는 경고 표시.
// 스폰 링은 카메라 밖이라 거기 그리면 안 보인다. 같은 8분할을 아레나 안쪽에 투영해 화면 안에 아이콘을 띄운다.
// 아이콘은 커졌다 작아지고, 화면 아래 경고 문구도 같은 박자로 함께 뛴다.

using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class SpawnZoneWarning : MonoBehaviour
{
    public static SpawnZoneWarning Instance { get; private set; }

    [Header("경고 아이콘")]
    [Tooltip("각 구역에 띄울 스프라이트. 비우면 아이콘이 안 나온다.")]
    [SerializeField] private Sprite warningSprite;
    [SerializeField] private Color warningColor = Color.white;

    [Tooltip("아이콘 배율. 스프라이트 원본 크기에 곱해진다. 너무 작으면 여기를 키운다.")]
    [SerializeField] private float iconScale = 1f;

    [Tooltip("아레나 경계보다 이만큼 안쪽에 놓는다. 경계선과 겹치면 잘 안 보인다.")]
    [SerializeField] private float inset = 1.4f;

    [Tooltip("바닥보다 살짝 위여야 파묻히지 않는다.")]
    [SerializeField] private float height = 0.05f;

    [Header("경고 문구")]
    [Tooltip("화면 아래에 띄울 텍스트. 비워두면 문구 없이 아이콘만 나온다.")]
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private string warningMessage = "경고 방향에서 적들이 몰려옵니다!";
    [SerializeField] private Color warningTextColor = new Color(1f, 0.35f, 0.25f);

    [Header("스폰 구역 경계선 물들이기")]
    [Tooltip("아이콘이 사라진 뒤에도 적이 들어올 변을 붉게 남겨 어디서 오는지 계속 알려준다.")]
    [SerializeField] private bool tintBoundary = true;

    [Tooltip("적이 들어오는 변의 색.")]
    [SerializeField] private Color boundaryColor = new Color(1f, 0.25f, 0.2f, 0.85f);

    [Tooltip("비우면 Sprites/Default로 대체한다. 사거리 원과 같은 재질을 써도 된다.")]
    [SerializeField] private Material boundaryMaterial;

    [Tooltip("띠의 두께. 회색 경계선보다 살짝 굵어야 덮인다.")]
    [SerializeField] private float boundaryThickness = 0.34f;

    [Tooltip("회색 경계선(기본 0.01)보다 높아야 위에 덮인다.")]
    [SerializeField] private float boundaryY = 0.03f;

    [Header("연출")]
    [Tooltip("한 번 커졌다 작아지는 데 걸리는 시간.")]
    [SerializeField] private float pulseDuration = 0.45f;

    [Tooltip("몇 번 뛰고 사라질지.")]
    [SerializeField] private int pulseCount = 4;

    [Tooltip("가장 작을 때의 배율.")]
    [SerializeField, Range(0.1f, 1f)] private float pulseMinScale = 0.75f;

    [Tooltip("가장 클 때의 배율.")]
    [SerializeField, Range(1f, 2f)] private float pulseMaxScale = 1.15f;

    [Tooltip("문구가 뛰는 폭. 아이콘 대비 비율이다. 1이면 아이콘과 똑같이, 0.3이면 그 30%만 움직인다. " +
             "문구는 아이콘보다 크게 보여서 같은 배율로 뛰면 과해진다.")]
    [SerializeField, Range(0f, 1f)] private float textPulseStrength = 0.3f;

    [Tooltip("등장할 때 튀어나오는 시간.")]
    [SerializeField] private float popInDuration = 0.22f;

    [Tooltip("사라질 때 스르륵 없어지는 시간.")]
    [SerializeField] private float fadeOutDuration = 0.3f;

    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>(SpawnZones.Count);
    private readonly List<Sequence> tweens = new List<Sequence>(SpawnZones.Count);

    // 구역마다 하나씩. 아이콘과 달리 다음 Warn 이 올 때까지 계속 켜져 있는다.
    private readonly List<MeshRenderer> boundaryStrips = new List<MeshRenderer>(SpawnZones.Count);
    private MaterialPropertyBlock boundaryBlock;
    private static readonly int SpriteColorId = Shader.PropertyToID("_Color");

    private Sequence textTween;
    private Vector3 textBaseScale = Vector3.one;

    private void Awake()
    {
        Instance = this;

        Build();

        if (warningText == null) return;

        textBaseScale = warningText.rectTransform.localScale;
        warningText.text = warningMessage;
        warningText.color = warningTextColor;
        warningText.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>지정한 구역들을 알린다. zones가 비어 있으면 8구역 전부를 표시한다.</summary>
    public void Warn(IList<int> zones)
    {
        HideAll();

        // 구역이 여덟 개여도 경고음은 한 번만 울린다.
        SfxManager.Play(SfxManager.Common?.ZoneWarning);

        if (zones == null || zones.Count == 0)
        {
            for (int zone = 1; zone <= SpawnZones.Count; zone++) Pulse(zone);
        }
        else
        {
            for (int i = 0; i < zones.Count; i++) Pulse(zones[i]);
        }

        // 아이콘은 몇 번 뛰고 사라지지만 경계선 색은 다음 Warn 까지 남는다.
        ApplyBoundary(zones);

        PulseText();
    }

    /// <summary>구역 하나만 알린다. 무한 모드에서 새 구역이 열릴 때 쓴다.</summary>
    public void WarnSingle(int zone)
    {
        SfxManager.Play(SfxManager.Common?.ZoneWarning);

        Pulse(zone);
        PulseText();
    }

    // ---------------------------------------------------------------- 아이콘

    private void Pulse(int zone)
    {
        int index = Mathf.Clamp(zone, 1, SpawnZones.Count) - 1;
        if (index < 0 || index >= markers.Count) return;

        SpriteRenderer marker = markers[index];
        if (marker == null) return;

        PlaceMarker(marker, index + 1);

        tweens[index]?.Kill();

        marker.enabled = true;
        SetMarkerAlpha(marker, 1f);

        Transform icon = marker.transform;
        icon.localScale = Vector3.one * (iconScale * pulseMinScale);

        // timeScale이 0인 순간(레벨업 등)에도 움직이도록 unscaled로 돌린다.
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        // 톡 튀어나온 뒤 커졌다 작아지기를 반복하고, 마지막에 스르륵 사라진다.
        sequence.Append(icon.DOScale(iconScale * pulseMaxScale, popInDuration).SetEase(Ease.OutBack));
        sequence.Append(icon.DOScale(iconScale * pulseMinScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(Mathf.Max(1, pulseCount) * 2, LoopType.Yoyo));

        // SpriteRenderer.DOFade는 DOTween의 Sprite 모듈이 켜져 있어야 해서, 값만 굴려 직접 칠한다.
        sequence.Append(DOVirtual.Float(1f, 0f, fadeOutDuration, value => SetMarkerAlpha(marker, value)));
        sequence.OnComplete(() => marker.enabled = false);

        tweens[index] = sequence;
    }

    // ---------------------------------------------------------------- 경계선

    /// <summary>넘어온 구역의 변만 붉게 켜고 나머지는 끈다. 비어 있으면 전 구역이라는 뜻이다.</summary>
    private void ApplyBoundary(IList<int> zones)
    {
        if (boundaryStrips.Count == 0) return;

        bool all = zones == null || zones.Count == 0;

        for (int zone = 1; zone <= SpawnZones.Count; zone++)
        {
            MeshRenderer strip = boundaryStrips[zone - 1];
            if (strip == null) continue;

            bool on = all || Contains(zones, zone);
            strip.enabled = on;

            // 아레나 크기가 바뀌어도 따라가도록 켤 때마다 다시 배치한다.
            if (on) PlaceStrip(strip.transform, zone);
        }
    }

    private static bool Contains(IList<int> zones, int zone)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] == zone) return true;
        }

        return false;
    }

    /// <summary>구역 선분 위에 띠를 얹는다. 선분 규칙은 SpawnZones 한 곳에서만 온다.</summary>
    private void PlaceStrip(Transform strip, int zone)
    {
        Vector3 a, b;
        SpawnZones.GetSegment(zone, ArenaBounds.HalfSize, out a, out b);

        Vector3 center = (a + b) * 0.5f;
        center.y = boundaryY;
        strip.position = center;

        // 축에 나란한 선분이라 어느 쪽이 긴지만 보면 된다.
        Vector3 d = b - a;
        float length = d.magnitude;
        float t = Mathf.Max(0.02f, boundaryThickness);

        strip.localScale = Mathf.Abs(d.x) > Mathf.Abs(d.z)
            ? new Vector3(length, 0.02f, t)
            : new Vector3(t, 0.02f, length);
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
    private void PlaceMarker(SpriteRenderer marker, int zone)
    {
        Vector2 half = ArenaBounds.HalfSize;
        half = new Vector2(Mathf.Max(1f, half.x - inset), Mathf.Max(1f, half.y - inset));

        Vector3 position = SpawnZones.GetCenter(zone, half);
        position.y = height;

        marker.transform.position = position;

        // 탑뷰 카메라와 같은 각도로 눕혀야 바닥에 붙어 보인다. 카메라 각도를 바꿔도 알아서 따라간다.
        Camera cam = Camera.main;
        if (cam != null) marker.transform.rotation = cam.transform.rotation;
    }

    private void SetMarkerAlpha(SpriteRenderer marker, float alpha)
    {
        Color color = warningColor;
        color.a = alpha;
        marker.color = color;
    }

    // ---------------------------------------------------------------- 문구

    private void PulseText()
    {
        if (warningText == null) return;

        textTween?.Kill();

        warningText.text = warningMessage;
        warningText.alpha = 1f;

        // 1을 기준으로 아이콘의 진폭을 textPulseStrength 만큼만 가져다 쓴다.
        float textMin = Mathf.Lerp(1f, pulseMinScale, textPulseStrength);
        float textMax = Mathf.Lerp(1f, pulseMaxScale, textPulseStrength);

        RectTransform rect = warningText.rectTransform;
        rect.localScale = textBaseScale * textMin;

        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        sequence.Append(rect.DOScale(textBaseScale * textMax, popInDuration).SetEase(Ease.OutBack));
        sequence.Append(rect.DOScale(textBaseScale * textMin, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(Mathf.Max(1, pulseCount) * 2, LoopType.Yoyo));

        sequence.Append(warningText.DOFade(0f, fadeOutDuration));
        sequence.OnComplete(() => rect.localScale = textBaseScale);

        textTween = sequence;
    }

    // ---------------------------------------------------------------- 준비

    private void Build()
    {
        for (int zone = 1; zone <= SpawnZones.Count; zone++)
        {
            GameObject go = new GameObject("ZoneWarning" + zone);
            go.transform.SetParent(transform, false);

            SpriteRenderer marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = warningSprite;
            marker.color = warningColor;
            marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker.receiveShadows = false;

            // 바닥이나 적에게 가리지 않도록 맨 앞에 그린다.
            marker.sortingOrder = 100;
            marker.enabled = false;

            markers.Add(marker);
            tweens.Add(null);
        }

        BuildBoundary();
    }

    private void BuildBoundary()
    {
        if (!tintBoundary) return;

        Material mat = boundaryMaterial;
        if (mat == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return;
            mat = new Material(fallback);
        }

        // 회색 경계선과 같은 모양이라 기본 큐브를 그대로 쓴다.
        GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cube = sample.GetComponent<MeshFilter>().sharedMesh;
        Destroy(sample);

        boundaryBlock = new MaterialPropertyBlock();

        for (int zone = 1; zone <= SpawnZones.Count; zone++)
        {
            GameObject go = new GameObject("ZoneEdge" + zone);
            go.transform.SetParent(transform, false);

            go.AddComponent<MeshFilter>().sharedMesh = cube;

            MeshRenderer strip = go.AddComponent<MeshRenderer>();
            strip.sharedMaterial = mat;
            strip.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            strip.receiveShadows = false;
            strip.enabled = false;

            strip.GetPropertyBlock(boundaryBlock);
            boundaryBlock.SetColor(SpriteColorId, boundaryColor);
            strip.SetPropertyBlock(boundaryBlock);

            boundaryStrips.Add(strip);
        }
    }
}
