// 슬라이더에서 손을 뗀 순간을 한 번만 알린다.
// onValueChanged 에 소리를 붙이면 드래그하는 내내 매 프레임 울리므로, 미리듣기 같은 건 여기에 붙인다.

using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class SliderReleaseNotifier : MonoBehaviour, IPointerUpHandler
{
    public event Action Released;

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Released != null) Released();
    }
}
