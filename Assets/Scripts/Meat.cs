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

    [Header("Visual Effects Prefabs")]
    [SerializeField] private GameObject heatPrefab;
    [SerializeField] private GameObject smokePrefab;
    [SerializeField] private Vector3 heatOffset = Vector3.zero;
    [SerializeField] private Vector3 smokeOffset = new Vector3(0f, 0.8f, -0.1f);

    private GameObject heatInstance;
    private GameObject smokeInstance;

    [Header("Sound")]
    [SerializeField] protected AudioClip hardSound;
    [SerializeField] protected AudioClip softSound;

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

    public float SideAProgress01 => HeatScale > 0f ? Mathf.Clamp(sideACookTime, 0f, BurnThreshold) / HeatScale : 0f;
    public float SideBProgress01 => HeatScale > 0f ? Mathf.Clamp(sideBCookTime, 0f, BurnThreshold) / HeatScale : 0f;
    public float CookedPercent01 => Mathf.Clamp01((sideACookTime + sideBCookTime) / Mathf.Max(0.0001f, HeatScale * 2f));
    public float HeatScale => cut != null ? cut.GetHeatScale() : 10000f;
    public float BurnThreshold => cut != null ? cut.GetBurnThreshold() : 10000f;
    public MeatStates SideAState => cut != null ? cut.GetStateForHeat(sideACookTime) : MeatStates.Crudo;
    public MeatStates SideBState => cut != null ? cut.GetStateForHeat(sideBCookTime) : MeatStates.Crudo;
    public MeatStates ActiveSideState => isSideA ? SideAState : SideBState;
    public float ActiveSideProgress01 => isSideA ? SideAProgress01 : SideBProgress01;
    public bool IsAnySideBurned => SideAState == MeatStates.Quemado || SideBState == MeatStates.Quemado;
    public bool IsOnGrill => occupiedSlots.Count > 0 && !isHeldByMouse;
    public bool IsSideAActive => isSideA;
    public bool IsGridRotated => isGridRotated;

    protected virtual void Awake()
    {
        itemType = ItemType.Meat;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        ApplyCutVisual();
    }

    protected override void Update()
    {
        base.Update();
        UpdateEffects();
    }

    private void UpdateEffects()
    {
        bool showHeat = IsCurrentlyCooking();
        bool isBurned = IsAnySideBurned;

        if (heatInstance != null)
        {
            if (heatInstance.activeSelf != showHeat)
                heatInstance.SetActive(showHeat);
        }
        else if (heatPrefab != null && showHeat)
        {
            heatInstance = Instantiate(heatPrefab, transform);
            heatInstance.transform.localPosition = heatOffset;
            heatInstance.transform.localRotation = Quaternion.identity;
            heatInstance.SetActive(true);
        }

        if (smokeInstance != null)
        {
            if (smokeInstance.activeSelf != isBurned)
                smokeInstance.SetActive(isBurned);
        }
        else if (smokePrefab != null && isBurned)
        {
            smokeInstance = Instantiate(smokePrefab, transform);
            smokeInstance.transform.localPosition = smokeOffset;
            smokeInstance.transform.localRotation = Quaternion.identity;
            smokeInstance.SetActive(true);
        }
    }

    private bool IsCurrentlyCooking()
    {
        if (occupiedSlots.Count == 0 || isHeldByMouse)
            return false;

        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            GridSlot slot = occupiedSlots[i];
            if (slot != null && slot.totalHeatReceived > 0.01f)
            {
                return true;
            }
        }
        return false;
    }

    public void SetCut(MeatCutSO newCut)
    {
        cut = newCut;
        ApplyCutVisual();
    }

    public virtual void Cook(float heatFromSlot)
    {
        if (lastCookFrame == Time.frameCount)
            return;

        lastCookFrame = Time.frameCount;

        float totalHeatInThisFrame = 0f;

        foreach (var slot in occupiedSlots)
        {
            if (slot != null)
                totalHeatInThisFrame += slot.totalHeatReceived;
        }

        float deltaHeat = totalHeatInThisFrame * Time.deltaTime;

        // Solo acumula la cara apoyada; al alcanzar el umbral de Quemado deja de acumular (clamp).
        float burnThreshold = BurnThreshold;

        if (isSideA)
            sideACookTime = Mathf.Min(sideACookTime + deltaHeat, burnThreshold);
        else
            sideBCookTime = Mathf.Min(sideBCookTime + deltaHeat, burnThreshold);

        RefreshState();
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
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Hide();

        if (MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.HideIfTarget(this);

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
        state = ActiveSideState;
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

    /// <summary>
    /// Deriva el estado visible desde el calor acumulado de la cara activa.
    /// Los estados por cara siempre se derivan de los floats (SideAState/SideBState).
    /// </summary>
    public void RefreshState()
    {
        MeatStates newState = ActiveSideState;

        if (newState != state)
        {
            state = newState;
            ApplyCutVisual();
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

        Sprite targetSprite = cut.GetSpriteForState(state, isSideA);

        if (targetSprite == null)
            targetSprite = cut.GetSpriteForSide(isSideA) ?? cut.GetDefaultSprite();

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }

    void OnMouseEnter()
    {
        if (isHeldByMouse) return;
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Show(this.ToHoverString(), transform);

        if (IsOnGrill && MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.Show(this);
    }

    void OnMouseExit()
    {
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Hide();

        if (MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.HideIfTarget(this);
    }

    void OnDestroy()
    {
        ReleaseOccupiedSlots();

        if (MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.HideIfTarget(this);
    }

    void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        itemType = ItemType.Meat;
        ApplyCutVisual();
        ApplyGridRotationPreview();
    }
}