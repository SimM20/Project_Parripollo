using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Meat : Item
{
    [Header("Data")]
    [FormerlySerializedAs("meatData")]
    [SerializeField] public MeatCutSO cut;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Grid Rotation")]
    [SerializeField] private bool rotatePreviewVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    [Header("Cooking State (Heat Units)")]
    public float sideACookTime = 0f;
    public float sideBCookTime = 0f;
    public bool isSideA = true;
    public MeatStates state = MeatStates.Crudo;

    private readonly List<GridSlot> occupiedSlots = new List<GridSlot>();
    private int lastCookFrame = -1;
    private bool isGridRotated;

    public float SideAProgress01 => Mathf.Clamp01(sideACookTime / Mathf.Max(0.0001f, SideAHeatTarget));
    public float SideBProgress01 => Mathf.Clamp01(sideBCookTime / Mathf.Max(0.0001f, SideBHeatTarget));
    public float CookedPercent01 => Mathf.Clamp01((sideACookTime + sideBCookTime) / Mathf.Max(0.0001f, SideAHeatTarget + SideBHeatTarget));
    public float SideAHeatTarget => cut != null ? cut.GetHeatTimeForSide(true) : 10000f;
    public float SideBHeatTarget => cut != null ? cut.GetHeatTimeForSide(false) : 10000f;
    public bool IsSideAActive => isSideA;
    public bool IsGridRotated => isGridRotated;

    void Awake()
    {
        itemType = ItemType.Meat;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyCutVisual();
    }

    public void SetCut(MeatCutSO newCut)
    {
        cut = newCut;
        ApplyCutVisual();
    }

    public void Cook(float heatFromSlot)
    {
        if (lastCookFrame == Time.frameCount)
            return;

        lastCookFrame = Time.frameCount;

        float totalHeatInThisFrame = 0f;

        foreach (var slot in occupiedSlots)
        {
            if (slot != null)
            {
                totalHeatInThisFrame += slot.totalHeatReceived;
            }
        }

        float deltaHeat = totalHeatInThisFrame * Time.deltaTime;

        if (isSideA)
            sideACookTime += deltaHeat;
        else
            sideBCookTime += deltaHeat;

        UpdateState();
    }

    public override void OnMouseUp()
    {
        isHeldByMouse = false;
        ClearHoverPreview();

        if (TrashZone.TryConsumeAtWorldPoint(transform.position, this))
            return;

        if (TrySendToBuildBuffer(transform.position))
            return;

        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            ReleaseOccupiedSlots();

            foreach (var s in slotsEncontrados)
            {
                s.PlaceMeat(this);
            }

            currentSlot = slotsEncontrados[0];

            Vector3 rawOffset = (cut != null) ? cut.visualOffset : Vector3.zero;
            Vector3 rotatedOffset = transform.rotation * rawOffset;
            transform.position = CalcCenter(slotsEncontrados) + rotatedOffset;

            return;
        }

        transform.position = startPosition;
    }

    private bool TrySendToBuildBuffer(Vector3 dropWorldPoint)
    {
        MeatTransferBuffer transferBuffer = FindFirstObjectByType<MeatTransferBuffer>();
        if (transferBuffer == null) return false;
        return transferBuffer.TryQueueFromGrillToBuild(this, dropWorldPoint);
    }

    protected override void HandleHeldInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
            ToggleGridRotation();
    }

    protected override void OnPickedUp()
    {
        ApplyGridRotationPreview();
        UpdateHoverPreview();
    }

    protected override void UpdateHoverPreview()
    {
        Vector2Int requiredSize = GetRequiredGridSize();
        GridSlot[] allSlots = FindObjectsOfType<GridSlot>();

        if (GridSlot.TryFindContiguousPlacement(allSlots, requiredSize, transform.position, itemType, gameObject, out List<GridSlot> slotsEncontrados))
        {
            SetHoverPreview(slotsEncontrados, true);
            return;
        }

        Collider2D hit = Physics2D.OverlapPoint(transform.position);
        if (hit != null && hit.TryGetComponent<GridSlot>(out GridSlot hoveredSlot))
        {
            SetHoverPreview(hoveredSlot, false);
            return;
        }

        ClearHoverPreview();
    }

    private Vector3 CalcCenter(List<GridSlot> slots)
    {
        Vector3 centro = Vector3.zero;
        foreach (var s in slots) centro += s.transform.position;
        return centro / slots.Count;
    }

    public void Flip()
    {
        isSideA = !isSideA;
        ApplyCutVisual();
    }

    public void ToggleGridRotation()
    {
        SetGridRotation(!isGridRotated);
        if (isHeldByMouse) UpdateHoverPreview();
    }

    public void SetGridRotation(bool rotated)
    {
        isGridRotated = rotated;
        ApplyGridRotationPreview();
    }

    public void RegisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null) return;
        if (!occupiedSlots.Contains(slot)) occupiedSlots.Add(slot);
        if (currentSlot == null) currentSlot = slot;
    }

    public void UnregisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null) return;
        occupiedSlots.Remove(slot);
        if (currentSlot == slot)
            currentSlot = occupiedSlots.Count > 0 ? occupiedSlots[0] : null;
    }

    public void ReleaseOccupiedSlots()
    {
        for (int i = occupiedSlots.Count - 1; i >= 0; i--)
        {
            GridSlot slot = occupiedSlots[i];
            if (slot != null && slot.currentItem == gameObject)
                slot.ClearSlot();
        }
        occupiedSlots.Clear();
        currentSlot = null;
    }

    private void UpdateState()
    {
        float target = isSideA ? SideAHeatTarget : SideBHeatTarget;

        if (sideACookTime >= target && sideBCookTime >= target)
        {
            state = (sideACookTime > target * 1.3f) ? MeatStates.Muy_Hecho : MeatStates.Hecho;
        }
        else if (sideACookTime > 0 || sideBCookTime > 0)
        {
            state = MeatStates.Jugoso;
        }
    }

    private Vector2Int GetRequiredGridSize()
    {
        if (cut == null) return Vector2Int.one;
        Vector2Int space = cut.GrillSpace;
        if (isGridRotated) space = new Vector2Int(space.y, space.x);
        return new Vector2Int(Mathf.Max(1, space.x), Mathf.Max(1, space.y));
    }

    private void ApplyGridRotationPreview()
    {
        if (!rotatePreviewVisual) return;
        transform.eulerAngles = new Vector3(0, 0, isGridRotated ? rotatedPreviewAngleZ : 0f);
    }

    private void ApplyCutVisual()
    {
        if (spriteRenderer == null || cut == null) return;
        Sprite targetSprite = cut.GetSpriteForSide(isSideA) ?? cut.GetDefaultSprite();
        if (targetSprite != null) spriteRenderer.sprite = targetSprite;
    }

    void OnDestroy() => ReleaseOccupiedSlots();

    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        itemType = ItemType.Meat;
        ApplyCutVisual();
        ApplyGridRotationPreview();
    }
}