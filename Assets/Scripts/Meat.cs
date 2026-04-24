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

    [Header("Cooking")]
    [SerializeField] private float maxCookTime = 5f;

    [Header("Grid Rotation")]
    [SerializeField] private bool rotatePreviewVisual = true;
    [SerializeField] private float rotatedPreviewAngleZ = 90f;

    public float sideACookTime = 0f;
    public float sideBCookTime = 0f;
    public bool isSideA = true;
    public MeatStates state = MeatStates.Crudo;

    public float SideAHeatTarget => GetHeatTimeForSide(true);
    public float SideBHeatTarget => GetHeatTimeForSide(false);
    public float CookTimePerSide => isSideA ? SideAHeatTarget : SideBHeatTarget;
    public float SideAProgress01 => Mathf.Clamp01(sideACookTime / Mathf.Max(0.0001f, SideAHeatTarget));
    public float SideBProgress01 => Mathf.Clamp01(sideBCookTime / Mathf.Max(0.0001f, SideBHeatTarget));
    public float CookedPercent01 => Mathf.Clamp01((sideACookTime + sideBCookTime) / Mathf.Max(0.0001f, SideAHeatTarget + SideBHeatTarget));
    public bool IsSideAActive => isSideA;
    public bool IsGridRotated => isGridRotated;

    private readonly List<GridSlot> occupiedSlots = new List<GridSlot>();
    private int lastCookFrame = -1;
    private bool isGridRotated;

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
            transform.position = CalcCenter(slotsEncontrados);
            return;
        }

        transform.position = startPosition;
    }

    private bool TrySendToBuildBuffer(Vector3 dropWorldPoint)
    {
        MeatTransferBuffer transferBuffer = FindFirstObjectByType<MeatTransferBuffer>();
        if (transferBuffer == null)
            return false;

        return transferBuffer.TryQueueFromGrillToBuild(this, dropWorldPoint);
    }

    protected override void HandleHeldInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleGridRotation();
        }
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

    public void Cook(float heat)
    {
        if (lastCookFrame == Time.frameCount)
            return;

        lastCookFrame = Time.frameCount;

        float delta = Time.deltaTime * Mathf.Max(0f, heat);
        if (delta <= 0f)
            return;

        if (isSideA)
        {
            sideACookTime += delta;
        }
        else
        {
            sideBCookTime += delta;
        }

        UpdateState();
    }

    public void Flip()
    {
        isSideA = !isSideA;
        ApplyCutVisual();

        string cutName = cut != null ? cut.cutName : "Sin corte";
        Debug.Log("Flip carne: " + cutName);
    }

    public void FlipSide()
    {
        Flip();
    }

    public void ToggleGridRotation()
    {
        SetGridRotation(!isGridRotated);

        if (isHeldByMouse)
            UpdateHoverPreview();

        if (cut != null)
        {
            Vector2Int size = GetRequiredGridSize();
            Debug.Log("Rotacion de grilla: " + cut.cutName + " -> " + size.x + "x" + size.y);
        }
    }

    public void SetGridRotation(bool rotated)
    {
        isGridRotated = rotated;
        ApplyGridRotationPreview();
    }

    public void RegisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null)
            return;

        if (!occupiedSlots.Contains(slot))
            occupiedSlots.Add(slot);

        if (currentSlot == null)
            currentSlot = slot;
    }

    public void UnregisterOccupiedSlot(GridSlot slot)
    {
        if (slot == null)
            return;

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
        float sideAHeatTarget = SideAHeatTarget;
        float sideBHeatTarget = SideBHeatTarget;

        bool sideAReady = sideACookTime >= sideAHeatTarget;
        bool sideBReady = sideBCookTime >= sideBHeatTarget;

        bool sideABurnt = sideACookTime >= sideAHeatTarget * 1.5f;
        bool sideBBurnt = sideBCookTime >= sideBHeatTarget * 1.5f;
        bool sideAOvercooked = sideACookTime >= sideAHeatTarget * 1.2f;
        bool sideBOvercooked = sideBCookTime >= sideBHeatTarget * 1.2f;

        if (sideABurnt || sideBBurnt)
        {
            state = MeatStates.Pasado;
            return;
        }

        if (sideAReady && sideBReady)
        {
            state = sideAOvercooked || sideBOvercooked ? MeatStates.Muy_Hecho : MeatStates.Hecho;
            return;
        }

        state = sideACookTime > 0f || sideBCookTime > 0f ? MeatStates.Jugoso : MeatStates.Crudo;
    }

    private int GetRequiredCellCount()
    {
        Vector2Int size = GetRequiredGridSize();
        return Mathf.Max(1, size.x * size.y);
    }

    private Vector2Int GetRequiredGridSize()
    {
        if (cut == null)
            return Vector2Int.one;

        Vector2Int space = cut.GrillSpace;
        if (isGridRotated)
            space = new Vector2Int(space.y, space.x);

        return new Vector2Int(Mathf.Max(1, space.x), Mathf.Max(1, space.y));
    }

    private void ApplyGridRotationPreview()
    {
        if (!rotatePreviewVisual)
            return;

        Vector3 euler = transform.eulerAngles;
        euler.z = isGridRotated ? rotatedPreviewAngleZ : 0f;
        transform.eulerAngles = euler;
    }

    private float GetHeatTimeForSide(bool sideA)
    {
        if (cut != null)
            return Mathf.Max(0.0001f, cut.GetHeatTimeForSide(sideA));

        return Mathf.Max(0.0001f, maxCookTime);
    }

    private void ApplyCutVisual()
    {
        if (spriteRenderer == null || cut == null)
            return;

        Sprite targetSprite = cut.GetSpriteForSide(isSideA);
        if (targetSprite == null)
            targetSprite = cut.GetDefaultSprite();

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }

    public float GetCookedPercent()
    {
        return CookedPercent01 * 100f;
    }

    void OnDestroy()
    {
        ReleaseOccupiedSlots();
    }

    void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        itemType = ItemType.Meat;
        ApplyCutVisual();
        ApplyGridRotationPreview();
    }
}
