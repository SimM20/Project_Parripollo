using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Un elemento al que puede saltar la selección del gamepad, medido en pantalla.</summary>
public struct NavTarget
{
    public enum Kind { UI, WorldCollider, GridSlot, GridBlock, Tray, PlateBody }

    public Object key;
    public Kind kind;
    /// <summary>true = lugar donde soltar lo que se arrastra; false = algo para agarrar o clickear.</summary>
    public bool isDropTarget;
    /// <summary>Punto en pantalla donde se apoya el puntero.</summary>
    public Vector2 point;
    /// <summary>Recuadro en pantalla para el resaltado.</summary>
    public Rect rect;
    /// <summary>
    /// Punto en mundo: el de agarre del plato (<see cref="Kind.PlateBody"/>) o el centro donde
    /// queda la pieza (<see cref="Kind.GridSlot"/> / <see cref="Kind.GridBlock"/>).
    /// </summary>
    public Vector3 worldAnchor;
    /// <summary>Slots que ocuparía el corte (solo <see cref="Kind.GridBlock"/>).</summary>
    public List<GridSlot> block;
    /// <summary>Footprint del corte con el que se armó el bloque; si cambia (rotación), el bloque deja de valer.</summary>
    public Vector2Int blockSize;
}

/// <summary>
/// Reglas de la navegación con gamepad: qué elementos son seleccionables en cada momento y
/// cómo se miden en pantalla. Todo lo que decide "a dónde puede saltar la selección" vive acá.
///
///  • Sin nada agarrado: botones de UI y, en el mundo, todo lo que se puede agarrar o clickear
///    (celdas del stock, panes/guarniciones/frascos, carne y carbón de la capa activa, bandeja,
///    carne del plato, el plato, clientes, pestañas de los paneles y el botón de capa).
///  • Arrastrando: solo los lugares donde soltar lo agarrado (huecos válidos de la parrilla,
///    plato, bandeja, tacho, zona de vertido o clientes), según <see cref="DragKind"/>. La carne
///    no recorre huecos sueltos sino bloques del tamaño real del corte (con su rotación): cada
///    salto corre el bloque un slot, y el corte se dibuja encima del bloque donde va a caer.
///  • Con el juego en pausa (eventMask = 0) el mundo no cuenta: solo la UI.
/// </summary>
public static class GamepadNavTargets
{
    public enum DragKind
    {
        None,
        StockMeat,
        StockCoal,
        GrillMeat,
        GrillCoal,
        TrayMeat,
        PlateMeat,
        WholePlate,
        PlateExtra,
        Sauce,
    }

    private const float SlotFallbackSize = 0.5f;

    private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
    private static readonly Vector3[] UiCorners = new Vector3[4];
    private static PointerEventData uiPointer;
    private static EventSystem uiPointerOwner;

    /// <summary>Qué se está arrastrando, a partir del objeto que recibió el Down.</summary>
    public static DragKind DetectDrag(GameObject pressed)
    {
        if (PlateDeliveryDraggable.IsDraggingWholePlate) return DragKind.WholePlate;
        if (PlateDeliveryDraggable.IsDraggingPlateMeat) return DragKind.PlateMeat;
        if (pressed == null) return DragKind.None;

        if (pressed.TryGetComponent(out StockPanelSlot slot))
        {
            if (slot.Item is MeatCutSO) return DragKind.StockMeat;
            if (slot.Item is CoalSO) return DragKind.StockCoal;
            return DragKind.None;
        }

        if (pressed.TryGetComponent<Meat>(out _)) return DragKind.GrillMeat;
        if (pressed.TryGetComponent<Coal>(out _)) return DragKind.GrillCoal;
        if (pressed.TryGetComponent<ToBuildDraggableMeat>(out _)) return DragKind.TrayMeat;
        if (pressed.TryGetComponent<BuildDraggableFoodItem>(out _)) return DragKind.PlateExtra;
        if (pressed.TryGetComponent<ToppingDraggable>(out _)) return DragKind.Sauce;
        return DragKind.None;
    }

    // ── Recolección ──

    /// <summary>Todos los elementos seleccionables ahora. <paramref name="holding"/> = botón principal apretado.</summary>
    public static void Collect(List<NavTarget> into, bool holding, DragKind drag, GameObject pressed)
    {
        into.Clear();

        if (!holding)
        {
            CollectUI(into);
            if (WorldAvailable(out _))
                CollectWorldIdle(into);
            return;
        }

        if (WorldAvailable(out _))
            CollectDrops(into, drag, pressed);
    }

    private static void CollectUI(List<NavTarget> into)
    {
        foreach (Selectable selectable in Selectable.allSelectablesArray)
            Add(into, selectable, NavTarget.Kind.UI, false);
    }

    private static void CollectWorldIdle(List<NavTarget> into)
    {
        StockPanelController stock = StockPanelController.Instance;
        if (stock != null && stock.IsOpen)
        {
            foreach (StockPanelSlot slot in Object.FindObjectsByType<StockPanelSlot>(FindObjectsSortMode.None))
            {
                if (slot.IsInteractable)
                    Add(into, slot.GetComponent<Collider2D>(), NavTarget.Kind.WorldCollider, false);
            }
        }

        ToppingsPanelController toppings = ToppingsPanelController.Instance;
        if (toppings == null || toppings.IsOpen)
        {
            foreach (BuildDraggableFoodItem item in Object.FindObjectsByType<BuildDraggableFoodItem>(FindObjectsSortMode.None))
                Add(into, item.GetComponent<Collider2D>(), NavTarget.Kind.WorldCollider, false);
            foreach (ToppingDraggable jar in Object.FindObjectsByType<ToppingDraggable>(FindObjectsSortMode.None))
                Add(into, jar.GetComponent<Collider2D>(), NavTarget.Kind.WorldCollider, false);
        }

        AddAllColliders<StockPanelTab>(into);
        AddAllColliders<GrillLayerToggle>(into);
        AddAllColliders<Meat>(into);
        for (int i = 0; i < Coal.ActiveCoals.Count; i++)
        {
            if (Coal.ActiveCoals[i] != null)
                Add(into, Coal.ActiveCoals[i].GetComponent<Collider2D>(), NavTarget.Kind.WorldCollider, false);
        }
        AddAllColliders<ToBuildDraggableMeat>(into);
        AddAllColliders<PlateDeliveryDraggable>(into);

        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null && TryFindPlateGrabPoint(zones[i], out Vector3 grab))
            {
                var target = new NavTarget { key = zones[i], kind = NavTarget.Kind.PlateBody, worldAnchor = grab };
                if (Measure(ref target))
                    into.Add(target);
            }
        }

        foreach (CustomerView view in Object.FindObjectsByType<CustomerView>(FindObjectsSortMode.None))
        {
            if (view.Customer != null && !view.Customer.IsInFeedback)
                Add(into, view.PickCollider, NavTarget.Kind.WorldCollider, false);
        }
    }

    private static void CollectDrops(List<NavTarget> into, DragKind drag, GameObject pressed)
    {
        switch (drag)
        {
            case DragKind.StockMeat:
                AddMeatBlocks(into, drag, pressed);
                break;
            case DragKind.StockCoal:
                AddGridSlots(into, ItemType.Coal, null);
                break;
            case DragKind.GrillMeat:
                AddMeatBlocks(into, drag, pressed);
                AddPlateZones(into);
                AddAllColliders<TrashZone>(into, true);
                break;
            case DragKind.GrillCoal:
                AddGridSlots(into, ItemType.Coal, pressed);
                AddAllColliders<TrashZone>(into, true);
                break;
            case DragKind.TrayMeat:
                AddPlateZones(into);
                AddMeatBlocks(into, drag, pressed);
                break;
            case DragKind.PlateMeat:
                AddTray(into);
                AddMeatBlocks(into, drag, pressed);
                AddPlateZones(into);
                break;
            case DragKind.WholePlate:
                foreach (CustomerView view in Object.FindObjectsByType<CustomerView>(FindObjectsSortMode.None))
                {
                    if (view.Customer != null && !view.Customer.IsInFeedback)
                        Add(into, view.PickCollider, NavTarget.Kind.WorldCollider, true);
                }
                break;
            case DragKind.PlateExtra:
                AddPlateZones(into);
                break;
            case DragKind.Sauce:
                if (pressed != null && pressed.TryGetComponent(out ToppingDraggable jar))
                    Add(into, jar.PouringZone, NavTarget.Kind.WorldCollider, true);
                break;
        }
    }

    private static void AddAllColliders<T>(List<NavTarget> into, bool drop = false) where T : Component
    {
        foreach (T component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            Add(into, component.GetComponent<Collider2D>(), NavTarget.Kind.WorldCollider, drop);
    }

    private static void AddGridSlots(List<NavTarget> into, ItemType type, GameObject dragged)
    {
        foreach (GridSlot slot in Object.FindObjectsByType<GridSlot>(FindObjectsSortMode.None))
        {
            if (slot.acceptsType != type || !slot.CanPlaceItem(type, dragged))
                continue;
            Add(into, slot, NavTarget.Kind.GridSlot, true);
        }
    }

    private static void AddMeatBlocks(List<NavTarget> into, DragKind drag, GameObject pressed)
    {
        if (!TryGetMeatFootprint(drag, pressed, out Vector2Int size, out GameObject incoming))
            return;

        var blocks = new List<List<GridSlot>>();
        GridSlot.CollectContiguousPlacements(Object.FindObjectsByType<GridSlot>(FindObjectsSortMode.None), size, ItemType.Meat, incoming, blocks);

        foreach (List<GridSlot> block in blocks)
        {
            // La clave del bloque es su primer slot (arriba a la izquierda): única para un tamaño dado.
            var target = new NavTarget { key = block[0], kind = NavTarget.Kind.GridBlock, isDropTarget = true, block = block, blockSize = size };
            if (Measure(ref target))
                into.Add(target);
        }
    }

    /// <summary>
    /// Tamaño en slots del corte que se arrastra, ya rotado, y la pieza que cuenta como
    /// "propia" al validar slots (la carne de la parrilla puede volver a sus propios slots).
    /// </summary>
    public static bool TryGetMeatFootprint(DragKind drag, GameObject pressed, out Vector2Int size, out GameObject incoming)
    {
        size = Vector2Int.one;
        incoming = null;
        MeatCutSO cut = null;
        bool rotated = false;

        switch (drag)
        {
            case DragKind.StockMeat:
                if (pressed != null && pressed.TryGetComponent(out StockPanelSlot slot))
                {
                    cut = slot.Item as MeatCutSO;
                    rotated = slot.IsDragRotated;
                }
                break;
            case DragKind.GrillMeat:
                if (pressed != null && pressed.TryGetComponent(out Meat meat))
                {
                    cut = meat.cut;
                    rotated = meat.IsGridRotated;
                    incoming = pressed;
                }
                break;
            case DragKind.TrayMeat:
                if (pressed != null && pressed.TryGetComponent(out ToBuildDraggableMeat tray))
                {
                    cut = tray.Cut;
                    rotated = tray.IsGridRotated;
                }
                break;
            case DragKind.PlateMeat:
                PlateDeliveryDraggable.TryGetDraggedCut(out cut, out rotated);
                break;
        }

        if (cut == null) return false;

        // Mismo cálculo que Meat.GetRequiredGridSize.
        Vector2Int space = cut.GrillSpace;
        if (rotated && cut.CanRotate) space = new Vector2Int(space.y, space.x);
        size = new Vector2Int(Mathf.Max(1, space.x), Mathf.Max(1, space.y));
        return true;
    }

    private static void AddPlateZones(List<NavTarget> into)
    {
        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i] != null)
                Add(into, zones[i].ZoneCollider, NavTarget.Kind.WorldCollider, true);
        }
    }

    private static void AddTray(List<NavTarget> into)
    {
        MeatTransferBuffer buffer = Object.FindFirstObjectByType<MeatTransferBuffer>();
        if (buffer != null && buffer.TrayAnchor != null)
            Add(into, buffer.TrayAnchor, NavTarget.Kind.Tray, true);
    }

    private static void Add(List<NavTarget> into, Object key, NavTarget.Kind kind, bool drop)
    {
        if (key == null) return;

        var target = new NavTarget { key = key, kind = kind, isDropTarget = drop };
        if (Measure(ref target))
            into.Add(target);
    }

    // ── Medición ──

    /// <summary>
    /// Revalida y vuelve a medir el objetivo (los objetos se mueven: paneles que deslizan,
    /// clientes que entran). Devuelve false si dejó de ser seleccionable en este contexto.
    /// </summary>
    public static bool Refresh(ref NavTarget target, bool holding, DragKind drag, GameObject pressed)
    {
        if (target.key == null || target.isDropTarget != holding)
            return false;

        if (target.kind == NavTarget.Kind.UI)
            return !holding && Measure(ref target);

        if (!WorldAvailable(out _))
            return false;

        switch (target.kind)
        {
            case NavTarget.Kind.PlateBody:
                if (!((BuildFoodDropZone)target.key).HasLoadedPlate) return false;
                break;
            case NavTarget.Kind.WorldCollider:
                if (!StillEligible((Collider2D)target.key)) return false;
                break;
            case NavTarget.Kind.GridBlock:
                // Rotar el corte cambia el footprint: el bloque viejo ya no es donde caería.
                if (!TryGetMeatFootprint(drag, pressed, out Vector2Int size, out GameObject incoming) || size != target.blockSize)
                    return false;
                for (int i = 0; i < target.block.Count; i++)
                {
                    if (target.block[i] == null || !target.block[i].CanPlaceItem(ItemType.Meat, incoming))
                        return false;
                }
                break;
            case NavTarget.Kind.GridSlot:
                GridSlot gridSlot = (GridSlot)target.key;
                if (!gridSlot.CanPlaceItem(gridSlot.acceptsType, pressed))
                    return false;
                break;
        }

        return Measure(ref target);
    }

    private static bool StillEligible(Collider2D collider)
    {
        if (collider.TryGetComponent(out StockPanelSlot slot))
            return slot.IsInteractable && StockPanelController.Instance != null && StockPanelController.Instance.IsOpen;
        if (collider.TryGetComponent(out CustomerView view))
            return view.Customer != null && !view.Customer.IsInFeedback;
        return true;
    }

    private static bool Measure(ref NavTarget target)
    {
        switch (target.kind)
        {
            case NavTarget.Kind.UI:
                return MeasureSelectable((Selectable)target.key, ref target);
            case NavTarget.Kind.WorldCollider:
                return MeasureCollider((Collider2D)target.key, ref target);
            case NavTarget.Kind.GridSlot:
                return MeasureSlot((GridSlot)target.key, ref target);
            case NavTarget.Kind.GridBlock:
                return MeasureBlock(ref target);
            case NavTarget.Kind.Tray:
                return MeasureTray((Transform)target.key, ref target);
            case NavTarget.Kind.PlateBody:
                return MeasurePlateBody((BuildFoodDropZone)target.key, ref target);
        }
        return false;
    }

    private static bool WorldAvailable(out Camera cam)
    {
        cam = Camera.main;
        return cam != null && cam.eventMask != 0 && !GamePause.IsPaused;
    }

    private static bool MeasureCollider(Collider2D collider, ref NavTarget target)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            return false;

        Bounds bounds = collider.bounds;
        Vector2 center = bounds.center;

        // Siluetas cóncavas: el centro de la caja puede caer fuera del collider y el click se perdería.
        Vector2 pick = center;
        if (!collider.OverlapPoint(center))
        {
            Vector2 closest = collider.ClosestPoint(center);
            pick = closest + (closest - center).normalized * Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.25f;
        }

        return Project(new Vector3(pick.x, pick.y, collider.transform.position.z), bounds, collider.transform, ref target);
    }

    private static bool MeasureSlot(GridSlot slot, ref NavTarget target)
    {
        if (slot == null || !slot.isActiveAndEnabled)
            return false;

        Bounds bounds = SlotBounds(slot);

        target.worldAnchor = slot.transform.position;
        return Project(slot.transform.position, bounds, slot.transform, ref target);
    }

    private static bool MeasureBlock(ref NavTarget target)
    {
        List<GridSlot> block = target.block;
        if (block == null || block.Count == 0)
            return false;

        Bounds bounds = default;
        for (int i = 0; i < block.Count; i++)
        {
            GridSlot slot = block[i];
            if (slot == null || !slot.isActiveAndEnabled)
                return false;

            Bounds slotBounds = SlotBounds(slot);
            if (i == 0) bounds = slotBounds; else bounds.Encapsulate(slotBounds);
        }

        target.worldAnchor = GridSlot.GetBlockCenter(block);
        return Project(target.worldAnchor, bounds, block[0].transform, ref target);
    }

    private static Bounds SlotBounds(GridSlot slot)
    {
        SpriteRenderer sr = slot.GetComponent<SpriteRenderer>();
        return sr != null && sr.sprite != null
            ? sr.bounds
            : new Bounds(slot.transform.position, new Vector3(SlotFallbackSize, SlotFallbackSize, 0f));
    }

    private static bool MeasureTray(Transform anchor, ref NavTarget target)
    {
        if (anchor == null || !anchor.gameObject.activeInHierarchy)
            return false;

        bool any = false;
        Bounds bounds = default;
        foreach (Collider2D col in anchor.GetComponentsInChildren<Collider2D>())
        {
            if (!col.enabled) continue;
            if (any) bounds.Encapsulate(col.bounds); else bounds = col.bounds;
            any = true;
        }
        foreach (SpriteRenderer sr in anchor.GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite == null) continue;
            if (any) bounds.Encapsulate(sr.bounds); else bounds = sr.bounds;
            any = true;
        }
        if (!any) return false;

        return Project(new Vector3(bounds.center.x, bounds.center.y, anchor.position.z), bounds, anchor, ref target);
    }

    private static bool MeasurePlateBody(BuildFoodDropZone zone, ref NavTarget target)
    {
        if (zone == null || !zone.HasLoadedPlate || zone.ZoneCollider == null)
            return false;

        return Project(target.worldAnchor, zone.ZoneCollider.bounds, zone.transform, ref target);
    }

    /// <summary>
    /// Punto del plato que no cae sobre la carne: ahí el agarre toma el plato entero (el
    /// que entrega) en vez de solo la carne. Prefiere el borde de abajo del plato.
    /// </summary>
    private static bool TryFindPlateGrabPoint(BuildFoodDropZone zone, out Vector3 grab)
    {
        grab = default;
        if (!zone.HasLoadedPlate || zone.ZoneCollider == null)
            return false;

        Transform body = zone.PlateBody;
        float z = body != null ? body.position.z : zone.transform.position.z;
        Bounds bounds = zone.ZoneCollider.bounds;
        Vector2 preferred = new Vector2(bounds.center.x, bounds.min.y + bounds.size.y * 0.2f);

        const int Steps = 9;
        float best = float.MaxValue;
        bool found = false;
        for (int y = 0; y < Steps; y++)
        {
            for (int x = 0; x < Steps; x++)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / Steps),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / Steps));
                Vector3 world = new Vector3(p.x, p.y, z);

                if (!zone.ContainsPoint(world) || IsOverPlateMeat(p))
                    continue;

                float d = (p - preferred).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    grab = world;
                    found = true;
                }
            }
        }
        return found;
    }

    private static bool IsOverPlateMeat(Vector2 point)
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
        {
            if (hit != null && hit.GetComponent<PlateDeliveryDraggable>() != null)
                return true;
        }
        return false;
    }

    private static bool Project(Vector3 worldPoint, Bounds bounds, Transform owner, ref NavTarget target)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 screen = cam.WorldToScreenPoint(worldPoint);
        if (screen.z <= 0f || !OnScreen(screen))
            return false;

        // Algo tapado por un panel desplegado no se ofrece, salvo que sea parte del panel.
        // Tampoco como destino: soltar sobre un panel cancela el arrastre.
        if (IsUnderOpenPanel(worldPoint, owner))
            return false;

        Vector3 min = cam.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, worldPoint.z));
        Vector3 max = cam.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, worldPoint.z));

        target.point = screen;
        target.rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        return true;
    }

    private static bool IsUnderOpenPanel(Vector3 worldPoint, Transform owner)
    {
        return IsUnder(StockPanelController.Instance, worldPoint, owner)
            || IsUnder(ToppingsPanelController.Instance, worldPoint, owner);
    }

    private static bool IsUnder(SlidingPanel panel, Vector3 worldPoint, Transform owner)
    {
        return panel != null && panel.IsOpen && !owner.IsChildOf(panel.transform) && panel.IsPointOverPanel(worldPoint);
    }

    private static bool MeasureSelectable(Selectable selectable, ref NavTarget target)
    {
        if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            return false;

        RectTransform rt = selectable.transform as RectTransform;
        Canvas canvas = selectable.GetComponentInParent<Canvas>();
        if (rt == null || canvas == null || !canvas.isActiveAndEnabled)
            return false;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

        Vector3[] corners = UiCorners;
        rt.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        if (rect.width < 1f || rect.height < 1f || !OnScreen(rect.center))
            return false;

        // Tiene que ser lo primero que toca el click en su centro: descarta botones tapados
        // por un popup o recortados por una máscara.
        if (!IsTopmostUI(selectable, rect.center))
            return false;

        target.point = rect.center;
        target.rect = rect;
        return true;
    }

    private static bool IsTopmostUI(Selectable selectable, Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        if (uiPointer == null || uiPointerOwner != eventSystem)
        {
            uiPointer = new PointerEventData(eventSystem);
            uiPointerOwner = eventSystem;
        }

        uiPointer.position = screenPoint;
        UiHits.Clear();
        eventSystem.RaycastAll(uiPointer, UiHits);

        for (int i = 0; i < UiHits.Count; i++)
        {
            // Solo cuenta la UI: la primera respuesta de un raycaster de física es el mundo.
            if (!(UiHits[i].module is GraphicRaycaster)) continue;
            GameObject hit = UiHits[i].gameObject;
            return hit != null && hit.transform.IsChildOf(selectable.transform);
        }
        return false;
    }

    private static bool OnScreen(Vector2 p) => p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;

    // ── Elección por dirección ──

    /// <summary>
    /// El mejor candidato en la dirección <paramref name="direction"/> desde <paramref name="origin"/>
    /// (pantalla). Puntúa avance en la dirección + desvío lateral penalizado: gana lo que está
    /// "más derecho" y cerca, como la navegación de UI.
    /// </summary>
    public static bool TryPickInDirection(List<NavTarget> candidates, Vector2 origin, Vector2 direction, Object exclude, out NavTarget best)
    {
        best = default;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();

        const float LateralWeight = 2.5f;
        const float MinStepPixels = 4f;
        float bestScore = float.MaxValue;
        bool found = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            NavTarget c = candidates[i];
            if (exclude != null && c.key == exclude) continue;

            Vector2 v = c.point - origin;
            float along = Vector2.Dot(v, direction);
            if (along < MinStepPixels) continue;

            float lateral = Mathf.Abs(v.x * direction.y - v.y * direction.x);
            // Cono de ~65°: lo que está más de costado que adelante no es "en esa dirección".
            if (lateral > along * 2.2f) continue;

            float score = along + lateral * LateralWeight;
            if (score < bestScore)
            {
                bestScore = score;
                best = c;
                found = true;
            }
        }
        return found;
    }

    /// <summary>El candidato más cercano a un punto de pantalla, dentro de <paramref name="maxDistance"/>.</summary>
    public static bool TryPickNearest(List<NavTarget> candidates, Vector2 point, float maxDistance, out NavTarget best)
    {
        best = default;
        float bestDist = maxDistance * maxDistance;
        bool found = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            float d = (candidates[i].point - point).sqrMagnitude;
            if (d <= bestDist)
            {
                bestDist = d;
                best = candidates[i];
                found = true;
            }
        }
        return found;
    }
}
