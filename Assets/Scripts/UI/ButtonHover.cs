// 버튼에 마우스를 올리면 살짝 커진다. 색만 밝아지는 기본 하이라이트보다 눌러도 되는 곳이 확실히 드러난다.
// 메뉴는 timeScale이 0인 상태에서 뜨므로 트윈을 반드시 unscaled로 돌린다.

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("올렸을 때 커지는 배율.")]
    [SerializeField] private float hoverScale = 1.06f;

    [Tooltip("커지고 돌아오는 데 걸리는 시간.")]
    [SerializeField] private float duration = 0.12f;

    private RectTransform rect;
    private Selectable target;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        target = GetComponent<Selectable>();
        baseScale = rect.localScale;
    }

    private void OnDisable()
    {
        // 꺼질 때 커진 채로 굳으면 다시 켰을 때 어긋난 크기로 나타난다.
        rect.DOKill();
        rect.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (target != null && !target.interactable) return;

        Scale(baseScale * hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Scale(baseScale);
    }

    private void Scale(Vector3 to)
    {
        rect.DOKill();
        rect.DOScale(to, duration).SetEase(Ease.OutQuad).SetUpdate(true);
    }
}
