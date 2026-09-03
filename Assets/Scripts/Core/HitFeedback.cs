// 스폰/피격/사망 연출 담당. 색 깜빡임과 스케일 펀치를 DOTween으로 처리한다. 적과 플레이어가 공유한다.

using System;
using DG.Tweening;
using UnityEngine;

public class HitFeedback : MonoBehaviour
{
    [Header("피격 색 깜빡임")]
    [Tooltip("원래 색을 이 색 쪽으로 섞는다. 완전히 갈아끼우지 않으므로 눈이 덜 아프다.")]
    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("0이면 변화 없음, 1이면 flashColor로 완전히 덮음. 0.6쯤이 적당하다.")]
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.6f;

    [SerializeField] private float flashDuration = 0.08f;

    [Header("피격 스케일 펀치")]
    [SerializeField] private float punchStrength = 0.25f;
    [SerializeField] private float punchDuration = 0.18f;

    [Header("스폰 / 사망")]
    [SerializeField] private float spawnPopDuration = 0.3f;
    [SerializeField] private float deathHitDuration = 0.14f;
    [SerializeField] private float deathShrinkDuration = 0.2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] renderers;

    // MaterialPropertyBlock 이 아니라 렌더러마다 자기 머티리얼 사본을 쓴다.
    // MPB 는 SRP Batcher 를 통째로 무력화해서, 적 170마리가 배칭 없이 드로우콜 170개로 나갔다.
    // 머티리얼 사본은 서로 달라도 셰이더가 같으면 SRP Batcher 가 한 배치로 묶는다.
    // (URP/Lit 의 _BaseColor 는 머티리얼 단위 CBUFFER 라 GPU 인스턴싱으로도 인스턴스별로 못 바꾼다.)
    private Material[] instances;

    private Color[] originalColors;
    private Vector3 baseScale;

    // 프리팹에 찍혀 있던 진짜 원래 크기. baseScale은 보스가 부푸는 동안 바뀌므로,
    // 풀로 돌아갈 때 이 값으로 되돌려야 다음에 나온 놈이 부푼 크기를 물려받지 않는다.
    private Vector3 originalBaseScale;

    /// <summary>지금의 기준 크기. 피격 펀치와 사망 축소가 여기서 출발한다.</summary>
    public Vector3 BaseScale => baseScale;

    // 상태 이상(감속 등)으로 물든 색. 피격 깜빡임이 끝나면 원래색이 아니라 이 색으로 돌아온다.
    private Color tintColor = Color.white;
    private float tintStrength;

    private Tween flashTween;
    private Tween scaleTween;

    private void Awake()
    {
        baseScale = transform.localScale;
        originalBaseScale = baseScale;
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        instances = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Material shared = renderers[i].sharedMaterial;
            originalColors[i] = shared != null && shared.HasProperty(BaseColorId)
                ? shared.GetColor(BaseColorId)
                : Color.white;

            // .material 은 첫 접근 때 사본을 만든다. 풀링 덕분에 오브젝트 하나당 딱 한 번만 일어난다.
            instances[i] = renderers[i].material;
        }
    }

    private void OnDestroy()
    {
        if (instances == null) return;

        // .material 이 만든 사본은 우리가 치워야 한다.
        for (int i = 0; i < instances.Length; i++)
            if (instances[i] != null) Destroy(instances[i]);
    }

    private void OnDisable()
    {
        // 비활성 상태로 생성되면 Awake 없이 OnDisable이 먼저 올 수 있다.
        if (renderers == null) return;

        // 풀로 돌아갈 때 트윈이 살아있으면 다음 사용 때 스케일이 깨진다.
        flashTween?.Kill();
        scaleTween?.Kill();

        // 부풀려 놓은 기준 크기도 여기서 원래대로 돌린다.
        baseScale = originalBaseScale;
        transform.localScale = baseScale;

        tintStrength = 0f;
        ApplyBaseColors();
    }

    /// <summary>
    /// 상태 이상 색을 건다. strength 0이면 원래색으로 돌아간다.
    /// 피격 깜빡임은 이 색을 기준으로 계산되므로, 맞아도 물든 색이 지워지지 않는다.
    /// </summary>
    public void SetTint(Color color, float strength)
    {
        if (renderers == null) return;

        tintColor = color;
        tintStrength = Mathf.Clamp01(strength);

        // 깜빡이는 중이 아니면 바로 반영한다.
        if (flashTween == null || !flashTween.IsActive()) ApplyBaseColors();
    }

    /// <summary>피격/복구의 기준이 되는 색. 원래색에 상태 이상 색을 섞은 것.</summary>
    private Color BaseColor(int index)
    {
        return tintStrength <= 0f ? originalColors[index] : Color.Lerp(originalColors[index], tintColor, tintStrength);
    }

    /// <summary>
    /// 기준 크기를 바꾼다. 보스가 맞을수록 부푸는 것처럼, 연출 도중에 몸집 자체가 커져야 할 때 쓴다.
    /// 피격 펀치가 매번 baseScale로 되돌리기 때문에, transform을 직접 키워봐야 다음 피격에 지워진다.
    /// </summary>
    public void SetBaseScale(Vector3 scale, float duration = 0f)
    {
        baseScale = scale;

        scaleTween?.Kill();

        if (duration <= 0f)
        {
            transform.localScale = baseScale;
            return;
        }

        scaleTween = transform.DOScale(baseScale, duration).SetEase(Ease.OutBack);
    }

    /// <summary>스폰 시 0에서 원래 크기로 튀어나온다.</summary>
    public void PlaySpawn()
    {
        scaleTween?.Kill();
        transform.localScale = Vector3.zero;
        scaleTween = transform.DOScale(baseScale, spawnPopDuration).SetEase(Ease.OutBack);
    }

    /// <summary>피격 시 색 깜빡 + 스케일 펀치.</summary>
    public void PlayHit()
    {
        Flash();
        Punch();
    }

    /// <summary>
    /// 사망 연출. 마지막 일격도 피격으로 보이도록 짧게 반짝인 뒤 축소한다.
    /// 치명타일 때는 PlayHit을 따로 부르지 말고 이것만 부른다.
    /// </summary>
    public void PlayDeath(Action onComplete)
    {
        scaleTween?.Kill();

        // 중간 피격과 완전히 동일한 짧은 반짝임. flashDuration 뒤에 원래 색으로 돌아온다.
        Flash();
        transform.localScale = baseScale;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOPunchScale(Vector3.one * punchStrength, deathHitDuration, 8, 0.6f));
        sequence.Append(transform.DOScale(Vector3.zero, deathShrinkDuration).SetEase(Ease.InBack));
        sequence.OnComplete(() => onComplete?.Invoke());

        scaleTween = sequence;
    }

    private void Flash()
    {
        flashTween?.Kill();
        ApplyFlash(flashStrength);
        flashTween = DOVirtual.DelayedCall(flashDuration, ApplyBaseColors);
    }

    private void Punch()
    {
        scaleTween?.Kill();
        transform.localScale = baseScale;
        scaleTween = transform.DOPunchScale(Vector3.one * punchStrength, punchDuration, 8, 0.6f);
    }

    /// <summary>원래 색을 flashColor 쪽으로 strength 만큼만 섞는다. 각 렌더러의 고유색이 남는다.</summary>
    private void ApplyFlash(float strength)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            if (instances[i] == null) continue;

            instances[i].SetColor(BaseColorId, Color.Lerp(BaseColor(i), flashColor, strength));
        }
    }

    private void ApplyBaseColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            if (instances[i] == null) continue;

            instances[i].SetColor(BaseColorId, BaseColor(i));
        }
    }
}
