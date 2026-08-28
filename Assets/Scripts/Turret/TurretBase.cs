// 모든 포탑의 공통 베이스. 사거리 안 최근접 적 탐색과 발사 쿨다운을 처리하고, 실제 발사만 자식이 Fire()로 구현한다.
// 들었을 때 고유색을 밝히고 사거리 원을 띄우는 것도 여기서 처리한다.

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public abstract class TurretBase : MonoBehaviour
{
    /// <summary>데미지 / 연사 / 사거리 배율 묶음.</summary>
    public struct Mods
    {
        public float Damage;
        public float FireRate;
        public float Range;

        public static Mods One => new Mods { Damage = 1f, FireRate = 1f, Range = 1f };
    }

    // 드래그 핸들러가 마우스 근처 포탑을 찾을 때 쓴다. 콜라이더를 쓰지 않으므로 직접 들고 있는다.
    private static readonly List<TurretBase> all = new List<TurretBase>(16);
    public static IReadOnlyList<TurretBase> All => all;

    // 업그레이드는 배율로 관리한다. 나중에 생성되는 포탑도 자동으로 그동안의 강화를 물려받는다.
    private static Mods globalMods = Mods.One;
    private static readonly Dictionary<Type, Mods> typeMods = new Dictionary<Type, Mods>();

    // 포탑 종류별 특수 강화 레벨. 0이면 기본, 1이면 특수 강화 1회 적용.
    private static readonly Dictionary<Type, int> specialLevels = new Dictionary<Type, int>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // glb(glTFast)로 들어온 모델은 URP Lit이 아니라 자체 셰이더 그래프를 쓴다. 색 프로퍼티 이름이 다르다.
    private static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");

    // 사거리 원 채움이 쓰는 Sprites/Default의 색 프로퍼티.
    private static readonly int SpriteColorId = Shader.PropertyToID("_Color");

    [Header("데이터")]
    [Tooltip("연결하면 사거리/공속/데미지를 Awake에서 이 에셋 값으로 덮어쓴다. 비우면 아래 인스펙터 값을 그대로 쓴다.")]
    [SerializeField] private TurretDef def;

    [Header("포탑 공통 (TurretDef가 연결되면 덮어써짐)")]
    [SerializeField] protected float range = 6f;
    [SerializeField] protected float fireInterval = 0.5f;
    [SerializeField] protected float damage = 5f;
    [SerializeField] protected Transform muzzle;

    [Header("발사 반동")]
    [Tooltip("발사 순간 앞뒤로 움츠러드는 정도. 클수록 세게 눌린다. 포탑마다 손맛이 달라 프리팹에서 조절한다.")]
    [SerializeField] protected float recoilStrength = 0.18f;
    [SerializeField] protected float recoilDuration = 0.12f;

    [Header("동작")]
    [SerializeField] private float targetRefreshInterval = 0.15f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private bool drawRangeGizmo = true;

    [Header("들었을 때 하이라이트")]
    [Tooltip("고유색에 곱하는 밝기 증가량. 0이면 변화 없음, 1이면 두 배로 밝아진다. " +
             "흰색을 섞지 않고 곱하므로 텍스처로 색을 내는 모델도 색조를 잃지 않고 밝아진다.")]
    [SerializeField, Range(0f, 1f)] private float heldBrightness = 0.45f;

    [Header("들었을 때 사거리 원")]
    [Tooltip("사거리 원과 채움의 색. 예전에는 몸체 머터리얼에서 유추했지만, " +
             "모델을 glb로 바꾸면 몸체 색이 흰 텍스처라 링까지 하얘진다. 그래서 포탑마다 직접 지정한다.")]
    [SerializeField] private Color rangeColor = new Color(0.35f, 0.6f, 0.95f);
    [SerializeField] private Material rangeRingMaterial;
    [SerializeField] private float ringWidth = 0.09f;
    [SerializeField, Range(0f, 1f)] private float ringAlpha = 0.8f;

    [Tooltip("들지 않았는데도 원을 보여주는 포탑(오라형)이 쓸 흐린 투명도.")]
    [SerializeField, Range(0f, 1f)] private float ringIdleAlpha = 0.3f;
    [SerializeField] private int ringSegments = 64;
    [Tooltip("바닥(y=0)보다 살짝 위에 그려야 파묻히지 않는다.")]
    [SerializeField] private float ringHeight = 0.04f;

    [Tooltip("원 내부를 채우는 투명도. 0이면 테두리만 그린다. 항상 원을 보여주는 오라형 포탑만 채운다.")]
    [SerializeField, Range(0f, 1f)] private float ringFillAlpha = 0.14f;

    [Header("반복 효과음 (TurretDef의 LoopSfx를 채운 포탑만)")]
    [Tooltip("사거리 안에 적이 들어오고 나갈 때 소리가 붙었다 사라지는 데 걸리는 시간.")]
    [SerializeField] private float loopFadeDuration = 0.25f;

    /// <summary>현재 조준 중인 적. 없으면 null.</summary>
    protected EnemyBase CurrentTarget { get; private set; }

    /// <summary>마우스로 들려 있는가. 들려 있어도 조준과 발사는 그대로 이어간다.</summary>
    public bool IsHeld { get; private set; }

    /// <summary>프리팹이 가진 원래 스케일. 모델마다 다르므로 1이라고 가정하면 안 된다.</summary>
    public Vector3 BaseScale { get; private set; } = Vector3.one;

    /// <summary>내려놓기 연출이 도는 동안 true. 드래그 핸들러가 켜고 끈다.</summary>
    public bool IsSettling { get; set; }

    /// <summary>전체 강화와 이 포탑 종류 전용 강화를 곱한 최종 배율.</summary>
    public Mods TotalMods
    {
        get
        {
            Mods own;
            if (!typeMods.TryGetValue(GetType(), out own)) own = Mods.One;

            return new Mods
            {
                Damage = globalMods.Damage * own.Damage,
                FireRate = globalMods.FireRate * own.FireRate,
                Range = globalMods.Range * own.Range
            };
        }
    }

    /// <summary>이 포탑 종류의 특수 강화 레벨. 0이면 아직 없음.</summary>
    protected int SpecialLevel => GetSpecialLevel(GetType());

    /// <summary>true면 들지 않아도 사거리 원을 계속 보여준다. 오라형 포탑이 켠다.</summary>
    protected virtual bool AlwaysShowRange => false;

    protected float EffectiveDamage => damage * TotalMods.Damage;
    public float EffectiveRange => range * TotalMods.Range;
    private float EffectiveFireInterval => fireInterval / Mathf.Max(0.01f, TotalMods.FireRate);

    private Renderer[] tintedRenderers;
    private int[] tintedColorIds;
    private Color[] originalColors;
    private MaterialPropertyBlock propertyBlock;
    private LineRenderer rangeRing;
    private MeshRenderer rangeFill;
    private Mesh rangeFillMesh;
    private MaterialPropertyBlock fillPropertyBlock;
    private AudioSource loopSource;
    private float nextFireTime;
    private float nextTargetRefreshTime;
    private Tween recoilTween;

    // ---------------- 업그레이드 배율 ----------------

    // static은 씬을 다시 로드해도 남으므로 새 판을 시작할 때 반드시 되돌린다.
    public static void ResetMultipliers()
    {
        globalMods = Mods.One;
        typeMods.Clear();
        specialLevels.Clear();
    }

    /// <summary>해당 포탑 종류의 특수 강화 레벨. 자식이 SpecialLevel로 읽는다.</summary>
    public static int GetSpecialLevel(Type kind)
    {
        int level;
        return kind != null && specialLevels.TryGetValue(kind, out level) ? level : 0;
    }

    public static void AddSpecialLevel(Type kind)
    {
        if (kind == null) return;
        specialLevels[kind] = GetSpecialLevel(kind) + 1;
    }

    /// <summary>모든 포탑에 적용되는 강화.</summary>
    public static void AddGlobalMods(float damageAdd, float fireRateAdd, float rangeAdd)
    {
        globalMods.Damage += damageAdd;
        globalMods.FireRate += fireRateAdd;
        globalMods.Range += rangeAdd;
    }

    /// <summary>특정 포탑 종류에만 적용되는 강화.</summary>
    public static void AddTypeMods(Type kind, float damageAdd, float fireRateAdd, float rangeAdd)
    {
        if (kind == null) return;

        Mods mods;
        if (!typeMods.TryGetValue(kind, out mods)) mods = Mods.One;

        mods.Damage += damageAdd;
        mods.FireRate += fireRateAdd;
        mods.Range += rangeAdd;

        typeMods[kind] = mods;
    }

    // ---------------- 라이프사이클 ----------------

    /// <summary>이 포탑이 어떤 종류인지. GameManager가 강화 수치를 찾을 때 쓴다.</summary>
    public TurretDef Def => def;

    protected virtual void Awake()
    {
        BaseScale = transform.localScale;

        ApplyDef();

        if (muzzle == null) muzzle = transform;

        CacheTintableRenderers();
        BuildRangeRing();
        BuildLoopSource();
    }

    // 반복음은 포탑과 수명이 같아야 하고 드래그하면 따라와야 해서, 풀이 아니라 본인이 들고 있는다.
    private void BuildLoopSource()
    {
        if (def == null || def.LoopSfx == null || !def.LoopSfx.HasClip) return;

        GameObject go = new GameObject("LoopSfx");
        go.transform.SetParent(transform, false);

        loopSource = go.AddComponent<AudioSource>();
        def.LoopSfx.ApplyToLoopSource(loopSource);

        // 사거리에 적이 들어오면 그때 붙는다. 처음엔 무음.
        loopSource.volume = 0f;
        loopSource.Play();
    }

    private void ApplyDef()
    {
        if (def == null) return;

        range = def.Range;
        fireInterval = def.FireInterval;
        damage = def.Damage;
    }

    private void CacheTintableRenderers()
    {
        List<Renderer> targets = new List<Renderer>();
        List<int> colorIds = new List<int>();
        Renderer[] found = GetComponentsInChildren<Renderer>();

        for (int i = 0; i < found.Length; i++)
        {
            // LineRenderer는 레이저/사거리원 연출용이라 색을 건드리면 안 된다.
            if (found[i] is LineRenderer) continue;

            Material mat = found[i].sharedMaterial;
            if (mat == null) continue;

            // 머터리얼마다 색 프로퍼티 이름이 다르므로 렌더러별로 어느 쪽인지 기억해둔다.
            int colorId;
            if (mat.HasProperty(BaseColorId)) colorId = BaseColorId;
            else if (mat.HasProperty(GltfBaseColorId)) colorId = GltfBaseColorId;
            else continue;

            targets.Add(found[i]);
            colorIds.Add(colorId);
        }

        tintedRenderers = targets.ToArray();
        tintedColorIds = colorIds.ToArray();
        originalColors = new Color[tintedRenderers.Length];
        propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < tintedRenderers.Length; i++)
            originalColors[i] = tintedRenderers[i].sharedMaterial.GetColor(tintedColorIds[i]);
    }

    private void BuildRangeRing()
    {
        Material mat = rangeRingMaterial;
        if (mat == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return;
            mat = new Material(fallback);
        }

        GameObject go = new GameObject("RangeRing");
        go.transform.SetParent(transform, false);

        rangeRing = go.AddComponent<LineRenderer>();
        rangeRing.sharedMaterial = mat;

        // 부모가 들릴 때 1.25배로 커지므로 로컬 좌표로 그리면 사거리가 과장된다. 반드시 월드 좌표로 그린다.
        rangeRing.useWorldSpace = true;
        rangeRing.loop = true;
        rangeRing.positionCount = Mathf.Max(8, ringSegments);
        rangeRing.startWidth = ringWidth;
        rangeRing.endWidth = ringWidth;
        rangeRing.alignment = LineAlignment.View;
        rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeRing.receiveShadows = false;
        rangeRing.enabled = AlwaysShowRange;

        BuildRangeFill(mat);

        ApplyRingColor(false);
        if (rangeRing.enabled) UpdateRangeRing();
    }

    // 테두리만으로는 영향 범위가 잘 안 읽히는 오라형 포탑을 위해 원 안쪽을 옅게 채운다.
    private void BuildRangeFill(Material mat)
    {
        if (!AlwaysShowRange || ringFillAlpha <= 0f) return;

        rangeFillMesh = BuildDiscMesh(Mathf.Max(8, ringSegments));

        GameObject go = new GameObject("RangeFill");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = rangeFillMesh;

        rangeFill = go.AddComponent<MeshRenderer>();
        rangeFill.sharedMaterial = mat;
        rangeFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rangeFill.receiveShadows = false;

        fillPropertyBlock = new MaterialPropertyBlock();
        UpdateRangeFill();
    }

    // 반지름 1인 XZ 평면 원판. 실제 사거리는 매 프레임 스케일로 맞춘다.
    // Sprites/Default는 Cull Off라 앞뒷면 감김 방향을 신경 쓰지 않아도 된다.
    private static Mesh BuildDiscMesh(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        colors[0] = Color.white;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            colors[i + 1] = Color.white;
        }

        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }

        Mesh mesh = new Mesh { name = "RangeFillDisc" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void UpdateRangeFill()
    {
        if (rangeFill == null) return;

        // 포탑을 들면 부모가 1.25배로 커지므로 그만큼 나눠서 사거리를 정확히 유지한다.
        float parentScale = transform.lossyScale.x;
        float radius = parentScale > 0.0001f ? EffectiveRange / parentScale : EffectiveRange;

        Transform t = rangeFill.transform;
        t.localScale = new Vector3(radius, 1f, radius);

        // 테두리보다 살짝 아래에 깔아야 링이 채움 위로 또렷하게 보인다.
        Vector3 center = transform.position;
        center.y = ringHeight * 0.5f;
        t.position = center;
    }

    /// <summary>사거리 원과 채움이 함께 쓰는 바탕색. 지정한 색을 그대로 쓴다.</summary>
    private Color RingBaseColor()
    {
        // 예전에는 몸체 머터리얼에서 색을 유추해 흰색을 30% 섞어 밝혔지만,
        // 지금은 rangeColor를 직접 고르므로 손대면 인스펙터에서 고른 색과 화면 색이 달라진다.
        return rangeColor;
    }

    private void ApplyRingColor(bool held)
    {
        if (rangeFill != null)
        {
            Color fill = RingBaseColor();
            fill.a = ringFillAlpha;

            rangeFill.GetPropertyBlock(fillPropertyBlock);
            fillPropertyBlock.SetColor(SpriteColorId, fill);
            rangeFill.SetPropertyBlock(fillPropertyBlock);
        }

        if (rangeRing == null) return;

        Color color = RingBaseColor();
        color.a = held ? ringAlpha : ringIdleAlpha;

        rangeRing.startColor = color;
        rangeRing.endColor = color;
    }

    protected virtual void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this);
    }

    protected virtual void OnDestroy()
    {
        // 런타임에 만든 메시는 자동으로 회수되지 않는다.
        if (rangeFillMesh != null) Destroy(rangeFillMesh);
    }

    /// <summary>드래그 핸들러가 호출한다. 발사는 막지 않고 고유색을 밝히고 사거리 원을 띄운다.</summary>
    public void SetHeld(bool held)
    {
        IsHeld = held;
        ApplyHeldVisual(held);

        if (rangeRing == null) return;

        rangeRing.enabled = held || AlwaysShowRange;
        ApplyRingColor(held);

        if (rangeRing.enabled) UpdateRangeRing();
    }

    private void ApplyHeldVisual(bool held)
    {
        if (tintedRenderers == null) return;

        // 흰색을 섞으면 텍스처로 색을 내는 모델(_BaseColor가 순백)은 아무 변화가 없다.
        // 대신 곱하면 URP Lit이 _BaseMap에 그대로 실어주므로 어떤 머터리얼이든 밝아진다.
        float gain = 1f + heldBrightness;

        for (int i = 0; i < tintedRenderers.Length; i++)
        {
            if (tintedRenderers[i] == null) continue;

            Color origin = originalColors[i];
            Color color = held
                ? new Color(origin.r * gain, origin.g * gain, origin.b * gain, origin.a)
                : origin;

            tintedRenderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(tintedColorIds[i], color);
            tintedRenderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    // 드래그 핸들러가 Update에서 위치를 옮기므로, 링 갱신은 LateUpdate에서 해야 한 프레임 밀리지 않는다.
    private void LateUpdate()
    {
        if (rangeRing != null && rangeRing.enabled) UpdateRangeRing();
        UpdateRangeFill();
        UpdateLoopVolume();
    }

    /// <summary>사거리 안에 적이 있을 때만 반복음이 들리도록 볼륨을 밀고 당긴다.</summary>
    private void UpdateLoopVolume()
    {
        if (loopSource == null) return;

        float full = def.LoopSfx.Volume * SfxManager.MasterVolume;
        float target = CurrentTarget != null ? full : 0f;

        // 레벨업으로 timeScale이 0이어도 페이드는 돌아야 하므로 unscaled를 쓴다.
        float maxDelta = loopFadeDuration <= 0f
            ? full
            : full * Time.unscaledDeltaTime / loopFadeDuration;

        loopSource.volume = Mathf.MoveTowards(loopSource.volume, target, maxDelta);
    }

    private void UpdateRangeRing()
    {
        int segments = rangeRing.positionCount;
        float radius = EffectiveRange;

        Vector3 center = transform.position;
        center.y = ringHeight;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            rangeRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    protected virtual void Update()
    {
        RefreshTargetIfDue();

        if (CurrentTarget == null) return;

        AimAt(CurrentTarget.transform.position);

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + EffectiveFireInterval;
            Fire(CurrentTarget);
            PlayFireSfx();
            PlayRecoil();
        }
    }

    private void RefreshTargetIfDue()
    {
        // 죽은 타깃은 즉시 버린다.
        if (CurrentTarget != null && !CurrentTarget.IsAlive) CurrentTarget = null;

        if (Time.time < nextTargetRefreshTime && CurrentTarget != null) return;

        nextTargetRefreshTime = Time.time + targetRefreshInterval;
        CurrentTarget = EnemyRegistry.FindNearest(transform.position, EffectiveRange);
    }

    /// <summary>자식이 구현하는 발사.</summary>
    protected abstract void Fire(EnemyBase target);

    protected virtual void AimAt(Vector3 worldPosition)
    {
        Vector3 direction = worldPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    /// <summary>발사음. 대포처럼 한 번의 Fire에서 여러 발이 나가면 자식이 발마다 다시 부른다.</summary>
    protected virtual void PlayFireSfx()
    {
        if (def == null) return;

        SfxManager.Play(def.FireSfx, muzzle != null ? muzzle.position : transform.position);
    }

    protected virtual void PlayRecoil()
    {
        // 들려 있거나 착지 중일 때는 드래그 연출이 스케일을 쥐고 있으므로 반동을 생략한다.
        // 착지 중에 펀치가 끼어들면 펀치가 기억한 중간 스케일로 되돌려놓아 크기가 어긋난다.
        if (IsHeld || IsSettling) return;

        recoilTween?.Kill(true);
        recoilTween = transform.DOPunchScale(new Vector3(0f, 0f, -recoilStrength), recoilDuration, 6, 0.8f);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!drawRangeGizmo) return;

        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? EffectiveRange : range);
    }
}
