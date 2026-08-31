// 마우스로 포탑을 집어 옮긴다. 콜라이더 없이 마우스 월드 좌표와 포탑 사이 거리로 집을 대상을 고른다.
// 빈 땅을 끌면 스타크래프트처럼 영역 사각형이 그려지고, 그 안의 포탑이 한 묶음으로 선택된다.
// 선택된 묶음은 사각형 안을 잡아 통째로 옮길 수 있다.

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class TurretDragHandler : MonoBehaviour
{
    private enum Mode
    {
        Idle,           // 아무것도 안 하는 중 (선택만 남아 있을 수 있다)
        BoxSelecting,   // 영역 사각형을 그리는 중
        DraggingOne,    // 포탑 하나를 들고 있는 중
        DraggingGroup   // 선택된 묶음을 통째로 옮기는 중
    }

    [Header("집기")]
    [SerializeField] private float pickRadius = 1.3f;
    [SerializeField] private float pickupScaleUp = 1.175f;
    [SerializeField] private float pickupDuration = 0.12f;

    [Header("들고 있는 동안")]
    [SerializeField] private float followSpeed = 22f;
    [SerializeField] private float liftHeight = 0.63f;

    [Header("내려놓기")]
    [SerializeField] private float dropDuration = 0.18f;

    [Header("영역 선택")]
    [Tooltip("사각형을 그릴 재질. 비우면 Sprites/Default 로 대신한다.")]
    [SerializeField] private Material boxMaterial;
    [SerializeField] private Color boxColor = new Color(0.45f, 1f, 0.6f, 0.9f);
    [SerializeField] private float boxLineWidth = 0.09f;

    [Tooltip("바닥보다 살짝 위여야 파묻히지 않는다.")]
    [SerializeField] private float boxHeight = 0.03f;

    [Tooltip("선택이 끝난 뒤 포탑들을 감싸는 사각형의 여유. 이 안을 누르면 묶음을 잡는다.")]
    [SerializeField] private float selectionPadding = 0.9f;

    [Tooltip("이보다 작게 끌면 그냥 클릭으로 보고 선택을 해제한다.")]
    [SerializeField] private float minBoxSize = 0.5f;

    private Mode mode = Mode.Idle;

    private TurretBase held;

    // 선택된 묶음. 영역을 새로 그리거나 포탑 하나를 집으면 비워진다.
    private readonly List<TurretBase> selected = new List<TurretBase>(16);

    // 그룹을 잡은 순간의 마우스 기준 상대 위치. 잡은 모양을 유지하며 따라오게 한다.
    private readonly List<Vector3> grabOffsets = new List<Vector3>(16);

    // 영역을 그리는 동안 하이라이트가 켜진 포탑들. 들어오고 나갈 때만 갱신한다.
    private readonly List<TurretBase> boxHighlighted = new List<TurretBase>(16);

    private Vector3 boxStart;
    private Vector3 boxEnd;

    private LineRenderer boxLine;

    /// <summary>지금 포탑을 들고 있는가. 레벨업 UI가 열릴 때 참고한다.</summary>
    public bool IsDragging => mode == Mode.DraggingOne || mode == Mode.DraggingGroup;

    /// <summary>지금 포탑을 집어 옮겨도 되는가. 화면을 덮는 UI가 떠 있으면 막는다.</summary>
    private static bool DragAllowed
    {
        get
        {
            if (LevelUpUI.Instance != null && LevelUpUI.Instance.IsOpen) return false;
            if (ResultUI.Instance != null && ResultUI.Instance.IsOpen) return false;
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsOpen) return false;

            return PauseController.Instance == null || PauseController.Instance.AllowsTurretDrag;
        }
    }

    private void Awake()
    {
        BuildBoxLine();
    }

    private void Update()
    {
        // ESC 메뉴처럼 화면을 덮는 UI가 떠 있으면 조작을 막는다.
        // Space 전술 일시정지는 포탑을 옮기려고 멈추는 것이므로 여기서 막지 않는다.
        if (!DragAllowed)
        {
            Abort();
            return;
        }

        PruneSelection();

        switch (mode)
        {
            case Mode.Idle:
                if (MouseWorld.LeftPressedThisFrame) OnPress();
                break;

            case Mode.BoxSelecting:
                TickBoxSelect();
                break;

            case Mode.DraggingOne:
                // 창 밖에서 버튼을 떼는 경우까지 잡으려면 Released가 아니라 Held를 봐야 안전하다.
                if (!MouseWorld.LeftHeld) DropOne();
                else DragOneToMouse();
                break;

            case Mode.DraggingGroup:
                if (!MouseWorld.LeftHeld) DropGroup();
                else DragGroupToMouse();
                break;
        }

        RefreshBoxVisual();
    }

    // ---------------------------------------------------------------- 누른 순간

    private void OnPress()
    {
        Vector3 mouse = MouseWorld.Position;

        // 1) 선택된 묶음의 사각형 안을 눌렀으면 묶음을 통째로 잡는다.
        if (selected.Count > 0 && SelectionBounds().Contains(new Vector2(mouse.x, mouse.z)))
        {
            BeginGroupDrag(mouse);
            return;
        }

        // 2) 마우스 근처 포탑 하나.
        TurretBase nearest = FindNearest(mouse);
        if (nearest != null)
        {
            ClearSelection();
            PickOne(nearest);
            return;
        }

        ClearSelection();

        if (PauseController.Instance != null && !PauseController.Instance.IsPaused) return;
        // 3) 빈 땅 — 새 영역을 그린다. 기존 선택은 여기서 풀린다.

        mode = Mode.BoxSelecting;
        boxStart = mouse;
        boxEnd = mouse;
    }

    private TurretBase FindNearest(Vector3 mouse)
    {
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

        return nearest;
    }

    // ---------------------------------------------------------------- 영역 선택

    private void TickBoxSelect()
    {
        boxEnd = MouseWorld.Position;

        // 끌고 있는 중에도 사각형에 들어온 포탑은 평소 집었을 때와 같은 피드백을 받는다.
        RefreshBoxHighlight(CurrentBoxRect());

        if (!MouseWorld.LeftHeld) CommitBoxSelect();
    }

    private Rect CurrentBoxRect()
    {
        return Rect.MinMaxRect(
            Mathf.Min(boxStart.x, boxEnd.x),
            Mathf.Min(boxStart.z, boxEnd.z),
            Mathf.Max(boxStart.x, boxEnd.x),
            Mathf.Max(boxStart.z, boxEnd.z));
    }

    /// <summary>사각형에 새로 들어온 포탑만 켜고, 빠져나간 포탑만 끈다. 매 프레임 다시 칠하지 않는다.</summary>
    private void RefreshBoxHighlight(Rect rect)
    {
        var turrets = TurretBase.All;

        for (int i = 0; i < turrets.Count; i++)
        {
            TurretBase turret = turrets[i];
            if (turret == null) continue;

            Vector3 p = turret.transform.position;
            bool inside = rect.Contains(new Vector2(p.x, p.z));
            bool lit = boxHighlighted.Contains(turret);

            if (inside && !lit)
            {
                boxHighlighted.Add(turret);
                ApplyPickVisual(turret);
            }
            else if (!inside && lit)
            {
                boxHighlighted.Remove(turret);
                ReleaseVisual(turret);
            }
        }

        // 하이라이트 도중에 사라진 포탑 정리.
        for (int i = boxHighlighted.Count - 1; i >= 0; i--)
        {
            if (boxHighlighted[i] == null) boxHighlighted.RemoveAt(i);
        }
    }

    private void CommitBoxSelect()
    {
        Rect rect = CurrentBoxRect();
        bool tooSmall = rect.width < minBoxSize && rect.height < minBoxSize;

        mode = Mode.Idle;

        // 그냥 클릭한 것에 가까우면 선택으로 보지 않는다.
        if (tooSmall || boxHighlighted.Count == 0)
        {
            ClearSelection();
            return;
        }

        selected.Clear();
        selected.AddRange(boxHighlighted);
        boxHighlighted.Clear();

        // 선택된 포탑은 계속 밝은 상태로 둔다. 이게 '선택됨' 표시가 된다.
        SfxManager.Play(SfxManager.Common?.TurretPickUp, SelectionCenter());
    }

    private void ClearSelection()
    {
        for (int i = 0; i < boxHighlighted.Count; i++) ReleaseVisual(boxHighlighted[i]);
        boxHighlighted.Clear();

        for (int i = 0; i < selected.Count; i++) ReleaseVisual(selected[i]);
        selected.Clear();
    }

    /// <summary>선택된 포탑들을 감싸는 사각형. 여유를 둬서 안쪽을 누르기 쉽게 만든다.</summary>
    private Rect SelectionBounds()
    {
        if (selected.Count == 0) return Rect.zero;

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;

        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i] == null) continue;

            Vector3 p = selected[i].transform.position;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        if (minX > maxX) return Rect.zero;

        return Rect.MinMaxRect(minX - selectionPadding, minZ - selectionPadding,
                               maxX + selectionPadding, maxZ + selectionPadding);
    }

    private Vector3 SelectionCenter()
    {
        Rect rect = SelectionBounds();
        return new Vector3(rect.center.x, 0f, rect.center.y);
    }

    /// <summary>사라진 포탑을 목록에서 걷어낸다. 레벨업이나 파괴로 없어질 수 있다.</summary>
    private void PruneSelection()
    {
        for (int i = selected.Count - 1; i >= 0; i--)
        {
            if (selected[i] == null) selected.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- 하나 옮기기

    private void PickOne(TurretBase turret)
    {
        held = turret;
        mode = Mode.DraggingOne;

        ApplyPickVisual(held);
        SfxManager.Play(SfxManager.Common?.TurretPickUp, held.transform.position);
    }

    private void DragOneToMouse()
    {
        if (held == null)
        {
            mode = Mode.Idle;
            return;
        }

        Vector3 mouse = MouseWorld.Position;
        Vector3 target = new Vector3(mouse.x, liftHeight, mouse.z);

        held.transform.position = Vector3.Lerp(held.transform.position, target, FollowT());
    }

    private void DropOne()
    {
        TurretBase dropped = held;
        held = null;
        mode = Mode.Idle;

        if (dropped == null) return;

        SfxManager.Play(SfxManager.Common?.TurretDrop, dropped.transform.position);
        LandTurret(dropped);
    }

    // ---------------------------------------------------------------- 묶음 옮기기

    private void BeginGroupDrag(Vector3 mouse)
    {
        mode = Mode.DraggingGroup;

        grabOffsets.Clear();

        for (int i = 0; i < selected.Count; i++)
        {
            TurretBase turret = selected[i];
            if (turret == null)
            {
                grabOffsets.Add(Vector3.zero);
                continue;
            }

            Vector3 offset = turret.transform.position - mouse;
            offset.y = 0f;
            grabOffsets.Add(offset);

            // 이미 밝아져 있지만, 들어올리는 동안 반동이 스케일을 건드리지 않게 막아둔다.
            turret.IsDragScaling = true;
        }

        SfxManager.Play(SfxManager.Common?.TurretPickUp, mouse);
    }

    private void DragGroupToMouse()
    {
        Vector3 mouse = MouseWorld.Position;
        float t = FollowT();

        for (int i = 0; i < selected.Count && i < grabOffsets.Count; i++)
        {
            TurretBase turret = selected[i];
            if (turret == null) continue;

            Vector3 target = mouse + grabOffsets[i];
            target.y = liftHeight;

            turret.transform.position = Vector3.Lerp(turret.transform.position, target, t);
        }
    }

    private void DropGroup()
    {
        mode = Mode.Idle;

        SfxManager.Play(SfxManager.Common?.TurretDrop, SelectionCenter());

        for (int i = 0; i < selected.Count; i++)
        {
            TurretBase turret = selected[i];
            if (turret == null) continue;

            // 선택은 유지한다. 놓고 나서 바로 다시 잡아 옮길 수 있어야 한다.
            LandTurret(turret, keepHeld: true);
        }
    }

    // ---------------------------------------------------------------- 공통 연출

    private float FollowT()
    {
        // 프레임레이트에 영향받지 않는 지수 감쇠 추종. 살짝 끌려오는 손맛을 준다.
        // 일시정지(timeScale 0) 중에도 끌려와야 하므로 unscaled를 쓴다.
        return 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>집었을 때의 피드백. 고유색이 밝아지고 사거리 원이 켜지고 살짝 커진다.</summary>
    private void ApplyPickVisual(TurretBase turret)
    {
        if (turret == null) return;

        turret.SetHeld(true);
        turret.transform.DOKill();

        // 커지는 동안에는 반동이 스케일에 끼어들지 못하게 막고, 다 커지면 곧바로 풀어준다.
        // 여기서 풀어줘야 들고 있는 동안에도 발사 펀치가 나온다.
        turret.IsDragScaling = true;

        turret.transform.DOScale(turret.BaseScale * pickupScaleUp, pickupDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .OnComplete(() => turret.IsDragScaling = false);
    }

    /// <summary>하이라이트만 끄고 크기를 되돌린다. 위치는 건드리지 않는다.</summary>
    private void ReleaseVisual(TurretBase turret)
    {
        if (turret == null) return;

        turret.SetHeld(false);
        turret.transform.DOKill();
        turret.IsDragScaling = true;

        turret.transform.DOScale(turret.BaseScale, dropDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                turret.transform.localScale = turret.BaseScale;
                turret.IsDragScaling = false;
            });
    }

    /// <summary>아레나 안으로 끌어들여 바닥에 내려놓는다.</summary>
    private void LandTurret(TurretBase turret, bool keepHeld = false)
    {
        // 하이라이트는 손을 떼는 즉시 풀어준다. 착지 연출이 끝날 때까지 기다리면 반응이 늦어 보인다.
        if (!keepHeld) turret.SetHeld(false);

        Vector3 p = turret.transform.position;
        Vector2 bounds = ArenaBounds.HalfSize;
        Vector3 landing = new Vector3(
            Mathf.Clamp(p.x, -bounds.x, bounds.x),
            0f,
            Mathf.Clamp(p.z, -bounds.y, bounds.y));

        turret.transform.DOKill();

        // 착지가 끝날 때까지 발사 반동이 스케일에 끼어들지 못하게 막는다.
        turret.IsDragScaling = true;

        // 선택을 유지하는 경우에는 밝은 상태 그대로이므로 크기도 집었을 때 크기를 유지한다.
        Vector3 endScale = keepHeld ? turret.BaseScale * pickupScaleUp : turret.BaseScale;

        // 일시정지 중에 놓아도 착지 연출이 끝까지 돌아야 한다.
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Join(turret.transform.DOMove(landing, dropDuration).SetEase(Ease.OutBack));
        sequence.Join(turret.transform.DOScale(endScale, dropDuration).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            // 트윈 오차가 남지 않도록 최종값을 정확히 박아둔다.
            turret.transform.position = landing;
            turret.transform.localScale = endScale;
            turret.IsDragScaling = false;
        });
    }

    /// <summary>UI가 덮이거나 조작이 막힐 때 진행 중인 모든 동작을 정리한다.</summary>
    private void Abort()
    {
        // 진행 중에는 매 프레임 여기로 들어온다. 정리할 것이 없으면 곧바로 빠져나간다.
        if (mode == Mode.Idle && selected.Count == 0 && boxHighlighted.Count == 0) return;

        if (mode == Mode.DraggingOne) DropOne();
        else if (mode == Mode.DraggingGroup) DropGroup();

        mode = Mode.Idle;

        ClearSelection();
        RefreshBoxVisual();
    }

    // ---------------------------------------------------------------- 사각형 그리기

    private void BuildBoxLine()
    {
        Material material = boxMaterial;

        if (material == null)
        {
            Shader fallback = Shader.Find("Sprites/Default");
            if (fallback == null) return;
            material = new Material(fallback);
        }

        GameObject go = new GameObject("SelectionBox");
        go.transform.SetParent(transform, false);

        boxLine = go.AddComponent<LineRenderer>();
        boxLine.sharedMaterial = material;
        boxLine.useWorldSpace = true;
        boxLine.loop = true;
        boxLine.positionCount = 4;
        boxLine.startWidth = boxLineWidth;
        boxLine.endWidth = boxLineWidth;
        boxLine.numCornerVertices = 2;
        // 사거리 원과 같은 방식. 카메라를 향해 눕혀야 탑뷰에서 바닥에 붙어 보인다.
        boxLine.alignment = LineAlignment.View;
        boxLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        boxLine.receiveShadows = false;
        boxLine.startColor = boxColor;
        boxLine.endColor = boxColor;
        boxLine.enabled = false;
    }

    private void RefreshBoxVisual()
    {
        if (boxLine == null) return;

        // 그리는 중에는 마우스가 만든 사각형, 선택이 남아 있으면 포탑들을 감싸는 사각형.
        bool drawing = mode == Mode.BoxSelecting;
        bool hasSelection = selected.Count > 0;

        if (!drawing && !hasSelection)
        {
            boxLine.enabled = false;
            return;
        }

        Rect rect = drawing ? CurrentBoxRect() : SelectionBounds();

        boxLine.enabled = true;
        boxLine.startColor = boxColor;
        boxLine.endColor = boxColor;

        float y = boxHeight;
        boxLine.SetPosition(0, new Vector3(rect.xMin, y, rect.yMin));
        boxLine.SetPosition(1, new Vector3(rect.xMax, y, rect.yMin));
        boxLine.SetPosition(2, new Vector3(rect.xMax, y, rect.yMax));
        boxLine.SetPosition(3, new Vector3(rect.xMin, y, rect.yMax));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
        Vector2 bounds = ArenaBounds.HalfSize;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.x * 2f, 0.1f, bounds.y * 2f));
    }
}
