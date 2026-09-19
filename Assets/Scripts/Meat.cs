using System.Collections;
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

    [Header("Flip Animation")]
    [SerializeField] private float flipDuration = 0.45f;
    [SerializeField] private float flipLiftHeight = 0.6f;
    [SerializeField] private int flipSortingOrderBoost = 50;
    [SerializeField] private AudioClip flipSound;

    private bool isFlipping = false;
    private Coroutine flipCoroutine;
    private Vector3 baseLocalScale = Vector3.one;
    private int baseSortingOrder = 0;

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
    private Collider2D ownCollider;

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
    public bool IsFlipping => isFlipping;

    protected virtual void Awake()
    {
        itemType = ItemType.Meat;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            baseSortingOrder = spriteRenderer.sortingOrder;

        baseLocalScale = transform.localScale;
        ownCollider = GetComponent<Collider2D>();

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
        if (TutorialManager.IsCookingPaused)
            return;

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
        if (!EndHold()) return;

        if (TrashZone.TryConsumeAtWorldPoint(transform.position, this))
            return;

        if (TrySendToBuildBuffer(transform.position))
            return;

        // El plato admite una sola carne: si ya tiene una, este corte vuelve al lugar donde
        // estaba en vez de caer en los slots de la grilla que el plato tapa.
        if (BuildFoodDropZone.IsPlateOccupiedAt(transform.position))
        {
            transform.position = startPosition;
            RestoreHoverIfPointerOver();
            return;
        }

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

            RestoreHoverIfPointerOver();
            return;
        }

        transform.position = startPosition;
        RestoreHoverIfPointerOver();
    }

    /// <summary>
    /// Tras soltar la pieza, reactiva burbuja y barra si el puntero nunca salió del collider
    /// (Unity no dispara OnMouseEnter de nuevo en ese caso).
    /// </summary>
    private void RestoreHoverIfPointerOver()
    {
        if (isHeldByMouse || ownCollider == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 pointerWorld = GetMouseWorldPosition();
        if (!ownCollider.OverlapPoint(pointerWorld)) return;

        ShowHover();
    }

    private void ShowHover()
    {
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Show(this);

        if (IsOnGrill && MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.Show(this);
    }

    private bool TrySendToBuildBuffer(Vector3 dropWorldPoint)
    {
        if (!TutorialManager.CheckMeatDragToBuildAllowed())
            return false;

        MeatTransferBuffer transferBuffer = FindFirstObjectByType<MeatTransferBuffer>();
        if (transferBuffer == null) return false;
        return transferBuffer.TryPlateMeatFromGrill(this, dropWorldPoint);
    }

    protected override void HandleHeldInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
            ToggleGridRotation();
    }

    protected override void OnPickedUp()
    {
        if (isFlipping)
            CancelFlipAnimation();

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
        if (isFlipping) return;

        if (gameObject.activeInHierarchy)
        {
            flipCoroutine = StartCoroutine(FlipRoutine());
        }
        else
        {
            isSideA = !isSideA;
            state = ActiveSideState;
            ApplyCutVisual();
            TutorialManager.NotifyMeatFlipped(cut);
        }
    }

    private IEnumerator FlipRoutine()
    {
        isFlipping = true;

        Vector3 startPos = transform.position;
        Vector3 initialLocalScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : transform.localScale;
        int origSortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : baseSortingOrder;

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = origSortingOrder + flipSortingOrderBoost;

        PlayFlipSound();

        float elapsed = 0f;
        bool logicFlipped = false;

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flipDuration);

            // Parabolic lift arc
            float lift = Mathf.Sin(t * Mathf.PI) * flipLiftHeight;
            transform.position = startPos + Vector3.up * lift;

            // Horizontal flip scale (compressing towards 0 and expanding back to full width)
            float cosVal = Mathf.Cos(t * Mathf.PI);
            float scaleX = Mathf.Abs(cosVal) * initialLocalScale.x;

            // Subtle vertical stretch in flight
            float scaleY = initialLocalScale.y * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
            transform.localScale = new Vector3(scaleX, scaleY, initialLocalScale.z);

            // Mid-air flip point (at peak of the jump / when scaleX reaches 0)
            if (!logicFlipped && t >= 0.5f)
            {
                logicFlipped = true;
                isSideA = !isSideA;
                state = ActiveSideState;
                ApplyCutVisual();
                TutorialManager.NotifyMeatFlipped(cut);
            }

            yield return null;
        }

        // Safety guarantee for logical flip
        if (!logicFlipped)
        {
            isSideA = !isSideA;
            state = ActiveSideState;
            ApplyCutVisual();
            TutorialManager.NotifyMeatFlipped(cut);
        }

        transform.position = startPos;
        transform.localScale = initialLocalScale;

        if (spriteRenderer != null)
            spriteRenderer.sortingOrder = origSortingOrder;

        // Landing bounce / impact juice
        yield return StartCoroutine(LandingBounce(initialLocalScale));

        isFlipping = false;
        flipCoroutine = null;
    }

    private IEnumerator LandingBounce(Vector3 targetScale)
    {
        float bounceDuration = 0.08f;
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounceAmount = Mathf.Sin(t * Mathf.PI) * 0.12f;
            transform.localScale = new Vector3(
                targetScale.x * (1f + bounceAmount),
                targetScale.y * (1f - bounceAmount),
                targetScale.z
            );
            yield return null;
        }
        transform.localScale = targetScale;
    }

    public void CancelFlipAnimation()
    {
        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }

        if (isFlipping)
        {
            isFlipping = false;
            transform.localScale = (baseLocalScale.sqrMagnitude > 0.0001f) ? baseLocalScale : Vector3.one;
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = baseSortingOrder;
            ApplyCutVisual();
        }
    }

    private void PlayFlipSound()
    {
        AudioClip clipToPlay = flipSound != null ? flipSound : softSound;
        if (clipToPlay == null) return;

        AudioSource src = GetComponent<AudioSource>();
        if (src != null)
        {
            src.PlayOneShot(clipToPlay);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position);
        }
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
            TutorialManager.NotifyMeatStateChanged(this);
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

        spriteRenderer.flipX = !isSideA;
    }

    void OnMouseEnter()
    {
        if (isHeldByMouse) return;
        ShowHover();
    }

    void OnMouseExit()
    {
        if (MeatHoverBubble.Instance != null)
            MeatHoverBubble.Instance.Hide();

        if (MeatCookHoverBar.Instance != null)
            MeatCookHoverBar.Instance.HideIfTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (isFlipping)
            CancelFlipAnimation();
    }

    void OnDestroy()
    {
        if (isFlipping)
            CancelFlipAnimation();

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