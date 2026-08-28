// 마우스로 포탑을 집어 옮긴다. 콜라이더 없이 마우스 월드 좌표와 포탑 사이 거리로 집을 대상을 고른다.

using DG.Tweening;
using UnityEngine;

public class TurretDragHandler : MonoBehaviour
{
    [Header("집기")]
    [SerializeField] private float pickRadius = 1.3f;
    [SerializeField] private float pickupScaleUp = 1.175f;
    [SerializeField] private float pickupDuration = 0.12f;

    [Header("들고 있는 동안")]
    [SerializeField] private float followSpeed = 22f;
    [SerializeField] private float liftHeight = 0.9f;

    [Header("내려놓기")]
    [SerializeField] private float dropDuration = 0.18f;

    private TurretBase held;

    /// <summary>지금 포탑을 들고 있는가. 5단계 레벨업 UI가 열릴 때 참고한다.</summary>
    public bool IsDragging => held != null;

    private void Update()
    {
        if (held == null)
        {
            if (MouseWorld.LeftPressedThisFrame) TryPick();
            return;
        }

        // 창 밖에서 버튼을 떼는 경우까지 잡으려면 Released가 아니라 Held를 봐야 안전하다.
        if (!MouseWorld.LeftHeld)
        {
            Drop();
            return;
        }

        DragToMouse();
    }

    private void TryPick()
    {
        Vector3 mouse = MouseWorld.Position;

        TurretBase nearest = null;
        float bestSqrDistance = pickRadius * pickRadius;

        var turrets = TurretBase.All;
        for (int i = 0; i < turrets.Count; i++)
        {
            TurretBase turret = turrets[i];
            if (turret == null) continue;

            Vector3 delta = turret.transform.position - mouse;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;

            if (sqrDistance <= bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = turret;
            }
        }

        if (nearest == null) return;

        held = nearest;
        held.SetHeld(true);

        SfxManager.Play(SfxManager.Common?.TurretPickUp, held.transform.position);

        held.transform.DOKill();

        // OnComplete에서 쓸 대상을 지역 변수로 붙잡는다. held는 그 사이 다른 포탑으로 바뀔 수 있다.
        TurretBase picked = held;

        // 커지는 동안에는 반동이 스케일에 끼어들지 못하게 막고, 다 커지면 곧바로 풀어준다.
        // 여기서 풀어줘야 들고 있는 동안에도 발사 펀치가 나온다.
        picked.IsDragScaling = true;

        picked.transform.DOScale(picked.BaseScale * pickupScaleUp, pickupDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => picked.IsDragScaling = false);
    }

    private void DragToMouse()
    {
        Vector3 mouse = MouseWorld.Position;
        Vector3 target = new Vector3(mouse.x, liftHeight, mouse.z);

        // 프레임레이트에 영향받지 않는 지수 감쇠 추종. 살짝 끌려오는 손맛을 준다.
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        held.transform.position = Vector3.Lerp(held.transform.position, target, t);
    }

    private void Drop()
    {
        TurretBase dropped = held;
        held = null;

        // 하이라이트는 손을 떼는 즉시 풀어준다. 착지 연출이 끝날 때까지 기다리면 반응이 늦어 보인다.
        dropped.SetHeld(false);

        SfxManager.Play(SfxManager.Common?.TurretDrop, dropped.transform.position);

        Vector3 p = dropped.transform.position;
        Vector2 bounds = ArenaBounds.HalfSize;
        Vector3 landing = new Vector3(
            Mathf.Clamp(p.x, -bounds.x, bounds.x),
            0f,
            Mathf.Clamp(p.z, -bounds.y, bounds.y));

        dropped.transform.DOKill();

        // 착지가 끝날 때까지 발사 반동이 스케일에 끼어들지 못하게 막는다.
        dropped.IsDragScaling = true;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(dropped.transform.DOMove(landing, dropDuration).SetEase(Ease.OutBack));
        sequence.Join(dropped.transform.DOScale(dropped.BaseScale, dropDuration).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            // 트윈 오차가 남지 않도록 최종값을 정확히 박아둔다.
            dropped.transform.position = landing;
            dropped.transform.localScale = dropped.BaseScale;
            dropped.IsDragScaling = false;
        });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Vector2 bounds = ArenaBounds.HalfSize;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.x * 2f, 0.1f, bounds.y * 2f));
    }
}
