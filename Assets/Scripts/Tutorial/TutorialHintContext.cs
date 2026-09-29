using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Foto de la partida para decidir qué carteles del tutorial mostrar y a qué apuntan. El director la
/// rearma en cada revisión (unas 5 por segundo y después de cada señal) en vez de que cada cartel
/// consulte por su cuenta. Solo lee: nunca cambia nada del juego.
///
/// Usa los registros que ya existen (slots de la parrilla, <see cref="Coal.ActiveCoals"/>, clientes
/// activos, <see cref="BuildFoodDropZone.Zones"/>, stock). Las piezas fijas de la escena (parrilla,
/// botón de capa, pestañas, HUD, frascos del panel) se buscan una sola vez. Lo que no se puede leer
/// del juego (qué pieza se agarró, si el cliente rechazó el plato) lo anota de las señales, en
/// <see cref="Observe"/>.
///
/// Sirve para las dos escenas con carteles: en la partida la tienda no existe y en la tienda
/// (EndScene, ver TutorialHintContext.Shop.cs) no hay parrilla ni clientes, y lo que falta da falso.
/// </summary>
public partial class TutorialHintContext
{
    private bool bound;
    private GrillSystem grill;
    private GrillLayerToggle layerToggle;
    private Transform stockTab;
    private Transform toppingsTab;
    private Transform grillTopCenter;
    private Transform strikeHud;
    private Transform clockHud;
    private Transform undoButton;
    private Transform pauseButton;
    private readonly List<BuildDraggableFoodItem> panelItems = new List<BuildDraggableFoodItem>();
    private readonly List<ToppingDraggable> sauceJars = new List<ToppingDraggable>();

    private readonly HashSet<Meat> seenMeats = new HashSet<Meat>();
    private readonly HashSet<MeatCutSO> cutsOnCounter = new HashSet<MeatCutSO>();

    private bool stockPanelOpen;
    private bool toppingsPanelOpen;
    private bool meatLayer;
    private bool coalOnGrill;
    private Coal firstAsh;
    private Transform ashUnderFire;
    private bool plateHasMeat;
    private bool plateHasBurnt;
    private bool plateHasRaw;
    private Transform plate;
    private Meat firstMeatOnGrill;
    private Meat meatToFlip;
    private Meat meatReadyForOrder;
    private Meat meatAboutToBurn;
    private CustomerView waitingCustomer;
    private CustomerView hoverableCustomer;
    private CustomerView selectedMissingCut;
    private CustomerView otherMissingCut;
    private Customer plateCustomer;
    private bool plateNeedsBread;
    private ToppingSO missingTopping;
    private Transform breadTarget;
    private Transform sauceTarget;
    private bool strikeWarning;
    private bool closedWithCustomers;

    // Anotado de las señales.
    private Transform heldPiece;
    private bool plateRejected;

    public void Refresh()
    {
        if (!bound)
            Bind();

        stockPanelOpen = StockPanelController.Instance != null && StockPanelController.Instance.IsOpen;
        toppingsPanelOpen = ToppingsPanelController.Instance != null && ToppingsPanelController.Instance.IsOpen;
        meatLayer = GrillLayerToggle.IsItemTypeAllowed(ItemType.Meat);

        RefreshCoals();

        CustomerSystem customers = GameManager.Instance != null ? GameManager.Instance.Customers : null;
        RefreshCustomers(customers);
        RefreshMeats(customers);
        RefreshPlate(customers);
        RefreshMissingCuts(customers);
        RefreshShift();
        RefreshShop();

        if (HeldPiece == null)
            heldPiece = null;
    }

    /// <summary>Anota lo que solo se sabe por una señal. Lo llama el director, después de <see cref="Refresh"/>.</summary>
    public void Observe(TutorialSignal signal, TutorialSignalArgs args)
    {
        switch (signal)
        {
            case TutorialSignal.PieceGrabbed:
                heldPiece = args.Cut != null && args.Cut.CanRotate ? args.Target : null;
                break;

            case TutorialSignal.DeliveryRejected:
                plateRejected = true;
                break;
        }
    }

    public bool Evaluate(HintCondition condition)
    {
        switch (condition)
        {
            case HintCondition.Always: return true;
            case HintCondition.StockPanelOpen: return stockPanelOpen;
            case HintCondition.StockPanelClosed: return !stockPanelOpen;
            case HintCondition.ToppingsPanelClosed: return !toppingsPanelOpen;
            case HintCondition.MeatLayerActive: return meatLayer;
            case HintCondition.CoalLayerActive: return !meatLayer;
            case HintCondition.MeatOnGrill: return firstMeatOnGrill != null;
            case HintCondition.CoalOnGrill: return coalOnGrill;
            case HintCondition.NoCoalOnGrill: return !coalOnGrill;
            case HintCondition.CustomerWaiting: return waitingCustomer != null;
            case HintCondition.MeatNeedsFlip: return meatToFlip != null;
            case HintCondition.MeatReadyForOrder: return meatReadyForOrder != null;
            case HintCondition.PlateHasMeat: return plateHasMeat;
            case HintCondition.PlateEmpty: return !plateHasMeat;
            case HintCondition.ToppingsPanelOpen: return toppingsPanelOpen;
            case HintCondition.AshOnGrill: return firstAsh != null;
            case HintCondition.DraggingRotatablePiece: return HeldPiece != null;
            case HintCondition.PlateNeedsBread: return plateNeedsBread;
            case HintCondition.PlateNeedsTopping: return missingTopping != null;
            case HintCondition.PlateNeedsExtras: return plateNeedsBread || missingTopping != null;
            case HintCondition.PlateReadyForCustomer:
                return plateCustomer != null && !plateNeedsBread && missingTopping == null && !plateHasBurnt && !plateHasRaw;
            case HintCondition.SelectedWantsMissingCut: return selectedMissingCut != null;
            case HintCondition.OtherWantsMissingCut: return otherMissingCut != null;
            case HintCondition.PlateRejected: return plateRejected && plateHasMeat && plateCustomer == null;
            case HintCondition.MeatAboutToBurn: return meatAboutToBurn != null;
            case HintCondition.StrikeWarning: return strikeWarning;
            case HintCondition.ClosedWithCustomers: return closedWithCustomers;
            case HintCondition.AshUnderFire: return ashUnderFire != null;
            case HintCondition.PlateHasUnusableMeat:
                return plateHasMeat && (plateHasBurnt || (waitingCustomer != null && plateCustomer == null));
            default: return EvaluateShop(condition);
        }
    }

    /// <summary>Objeto al que apunta el cartel. Null si ahora no hay ninguno (por ejemplo, no hay carne en la parrilla).</summary>
    public Transform ResolveAnchor(HintAnchorId anchor)
    {
        switch (anchor)
        {
            case HintAnchorId.StockTab: return stockTab;
            case HintAnchorId.ToppingsTab: return toppingsTab;
            case HintAnchorId.LayerButton: return layerToggle != null ? layerToggle.transform : null;
            case HintAnchorId.Grill: return grillTopCenter;
            case HintAnchorId.Plate: return plate;
            case HintAnchorId.MeatOnGrill: return firstMeatOnGrill != null ? firstMeatOnGrill.transform : null;
            case HintAnchorId.MeatToFlip: return meatToFlip != null ? meatToFlip.transform : null;
            case HintAnchorId.WaitingCustomer: return BubbleOf(hoverableCustomer);
            case HintAnchorId.Ash: return firstAsh != null ? firstAsh.transform : null;
            case HintAnchorId.DraggedPiece: return HeldPiece;
            case HintAnchorId.BreadForOrder: return breadTarget;
            case HintAnchorId.SauceForOrder: return sauceTarget;
            case HintAnchorId.MissingCutCustomer: return BubbleOf(selectedMissingCut);
            case HintAnchorId.CustomerToPick: return BubbleOf(otherMissingCut);
            case HintAnchorId.UndoButton: return undoButton != null && undoButton.gameObject.activeInHierarchy ? undoButton : null;
            case HintAnchorId.MeatAboutToBurn: return meatAboutToBurn != null ? meatAboutToBurn.transform : null;
            case HintAnchorId.StrikeHud: return strikeHud;
            case HintAnchorId.ClockHud: return clockHud;
            case HintAnchorId.AshUnderFire: return ashUnderFire;
            case HintAnchorId.PauseButton: return pauseButton != null && pauseButton.gameObject.activeInHierarchy ? pauseButton : null;
            default: return ResolveShopAnchor(anchor);
        }
    }

    /// <summary>La pieza que se agarró sigue en la mano: existe, está activa y el botón sigue apretado.</summary>
    private Transform HeldPiece =>
        heldPiece != null && heldPiece.gameObject.activeInHierarchy && InputManager.PrimaryHeld ? heldPiece : null;

    private static Transform BubbleOf(CustomerView view)
    {
        if (view == null)
            return null;
        return view.OrderBubble != null ? view.OrderBubble.Panel : view.transform;
    }

    // ── Piezas fijas ────────────────────────────────────────────────────────

    private void Bind()
    {
        bound = true;
        grill = Object.FindFirstObjectByType<GrillSystem>();
        layerToggle = Object.FindFirstObjectByType<GrillLayerToggle>();

        foreach (StockPanelTab tab in Object.FindObjectsByType<StockPanelTab>(FindObjectsSortMode.None))
        {
            if (tab.Controller is StockPanelController)
                stockTab = tab.transform;
            else if (tab.Controller is ToppingsPanelController)
                toppingsTab = tab.transform;
        }

        grillTopCenter = FindGrillTopCenter(grill);
        BindShop();

        StrikeHudView strikes = Object.FindFirstObjectByType<StrikeHudView>();
        strikeHud = strikes != null ? strikes.transform : null;

        foreach (HudContainer container in Object.FindObjectsByType<HudContainer>(FindObjectsSortMode.None))
        {
            if (container.GetContainerType() == HudContainers.Time)
                clockHud = container.transform;
        }

        RollbackButtonUI undo = Object.FindFirstObjectByType<RollbackButtonUI>(FindObjectsInactive.Include);
        undoButton = undo != null ? undo.transform : null;

        pauseButton = HudManager.Instance != null && HudManager.Instance.PauseButton != null
            ? HudManager.Instance.PauseButton.transform
            : null;

        // Los panes y frascos del panel derecho son dispensers: están toda la noche.
        panelItems.AddRange(Object.FindObjectsByType<BuildDraggableFoodItem>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        sauceJars.AddRange(Object.FindObjectsByType<ToppingDraggable>(FindObjectsInactive.Include, FindObjectsSortMode.None));
    }

    /// <summary>
    /// Slot de carne del medio de la fila de arriba. El sprite de la parrilla tiene mucho margen
    /// transparente: su borde de arriba cae a la altura de los clientes.
    /// </summary>
    private static Transform FindGrillTopCenter(GrillSystem grillSystem)
    {
        if (grillSystem == null)
            return null;

        float topY = float.MinValue;
        float sumX = 0f;
        int count = 0;

        foreach (GridSlot slot in grillSystem.slots)
        {
            if (slot == null || slot.acceptsType != ItemType.Meat)
                continue;

            Vector3 position = slot.transform.position;
            topY = Mathf.Max(topY, position.y);
            sumX += position.x;
            count++;
        }

        if (count == 0)
            return grillSystem.transform;

        float centerX = sumX / count;
        Transform best = null;
        float bestDistance = float.MaxValue;

        foreach (GridSlot slot in grillSystem.slots)
        {
            if (slot == null || slot.acceptsType != ItemType.Meat)
                continue;

            Vector3 position = slot.transform.position;
            if (Mathf.Abs(position.y - topY) > 0.01f)
                continue;

            float distance = Mathf.Abs(position.x - centerX);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = slot.transform;
            }
        }

        return best;
    }

    // ── Carbón ──────────────────────────────────────────────────────────────

    private void RefreshCoals()
    {
        coalOnGrill = false;
        firstAsh = null;

        for (int i = 0; i < Coal.ActiveCoals.Count; i++)
        {
            Coal coal = Coal.ActiveCoals[i];
            if (coal == null)
                continue;

            if (coal.state == CoalStates.Ceniza)
            {
                if (firstAsh == null)
                    firstAsh = coal;
            }
            else
            {
                coalOnGrill = true;
            }
        }

        ashUnderFire = null;
        if (grill == null || firstAsh == null)
            return;

        List<GridSlot> slots = grill.slots;
        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot != null && slot.acceptsType == ItemType.Coal && HasAshUnderFire(slot.stackedCoals))
            {
                ashUnderFire = slot.transform;
                return;
            }
        }
    }

    /// <summary>
    /// La pila tiene ceniza debajo de un carbón encendido. El calor de un slot pesa cada carbón por su
    /// lugar en la pila (100 %, 30 %, 15 %): la ceniza abajo se queda con el lugar que calienta entero.
    /// </summary>
    private static bool HasAshUnderFire(List<Coal> stack)
    {
        bool ashBelow = false;
        for (int i = 0; i < stack.Count; i++)
        {
            Coal coal = stack[i];
            if (coal == null)
                continue;

            if (coal.state == CoalStates.Ceniza)
                ashBelow = true;
            else if (ashBelow)
                return true;
        }
        return false;
    }

    // ── Clientes y carne ────────────────────────────────────────────────────

    /// <summary>
    /// El primer cliente que espera y, aparte, el primero al que se le puede pasar el puntero: un
    /// panel abierto encima le apaga el collider (<see cref="CustomerView.PickCollider"/>), y señalarlo
    /// ahí sería pedir algo que no se puede hacer.
    /// </summary>
    private void RefreshCustomers(CustomerSystem customers)
    {
        waitingCustomer = null;
        hoverableCustomer = null;

        if (customers == null || customers.ActiveCustomers == null)
            return;

        IReadOnlyList<Customer> active = customers.ActiveCustomers;
        for (int i = 0; i < active.Count; i++)
        {
            Customer customer = active[i];
            if (customer == null || !customers.IsCustomerActive(customer))
                continue;

            CustomerView view = customers.GetViewForCustomer(customer);
            if (view == null)
                continue;

            if (waitingCustomer == null)
                waitingCustomer = view;

            if (IsPickable(view))
            {
                hoverableCustomer = view;
                return;
            }
        }
    }

    private static bool IsPickable(CustomerView view) => view.PickCollider != null && view.PickCollider.enabled;

    private void RefreshMeats(CustomerSystem customers)
    {
        firstMeatOnGrill = null;
        meatToFlip = null;
        meatReadyForOrder = null;
        meatAboutToBurn = null;
        seenMeats.Clear();
        cutsOnCounter.Clear();

        if (grill == null)
            return;

        // Un corte grande ocupa varios slots: se cuenta una sola vez.
        List<GridSlot> slots = grill.slots;
        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null || slot.acceptsType != ItemType.Meat || slot.currentItem == null)
                continue;
            if (!slot.currentItem.TryGetComponent(out Meat meat) || !seenMeats.Add(meat))
                continue;

            // La que se está moviendo de lugar también cuenta como disponible para un pedido.
            if (meat.cut != null)
                cutsOnCounter.Add(meat.cut);

            if (!meat.IsOnGrill)
                continue;

            if (firstMeatOnGrill == null)
                firstMeatOnGrill = meat;
            if (meatToFlip == null && NeedsFlip(meat))
                meatToFlip = meat;
            if (meatReadyForOrder == null && IsReadyForOrder(meat, customers))
                meatReadyForOrder = meat;
            if (meatAboutToBurn == null && meat.ActiveSideState == MeatStates.Pasado)
                meatAboutToBurn = meat;
        }
    }

    /// <summary>La cara que se cocina (la de abajo) ya no está cruda y la de arriba sí.</summary>
    private static bool NeedsFlip(Meat meat)
    {
        MeatStates top = meat.IsSideAActive ? meat.SideBState : meat.SideAState;
        return meat.ActiveSideState != MeatStates.Crudo && top == MeatStates.Crudo;
    }

    /// <summary>Misma vara que la entrega: a un punto o menos de lo pedido, sin caras crudas ni quemadas.</summary>
    private static bool IsReadyForOrder(Meat meat, CustomerSystem customers)
    {
        if (customers == null || meat.cut == null)
            return false;

        MeatStates sideA = meat.SideAState;
        MeatStates sideB = meat.SideBState;
        if (sideA == MeatStates.Crudo || sideB == MeatStates.Crudo || sideA == MeatStates.Quemado || sideB == MeatStates.Quemado)
            return false;

        IReadOnlyList<Customer> active = customers.ActiveCustomers;
        for (int i = 0; i < active.Count; i++)
        {
            Customer customer = active[i];
            if (customer == null || customer.order == null || customer.order.PrimaryCut != meat.cut
                || !customers.IsCustomerActive(customer))
                continue;

            MeatStates requested = customer.order.GetRequestedState(0);
            if (CookingDeliveryEvaluator.EvaluateCut(sideA, sideB, requested, 0f).worstOffset <= 1)
                return true;
        }
        return false;
    }

    // ── Plato ───────────────────────────────────────────────────────────────

    /// <summary>
    /// El plato, qué tiene y para quién es: el primer cliente que espera el corte que hay en el plato.
    /// Si ese cliente pidió pan o salsas que faltan, dónde están en el panel derecho. Con carne cruda
    /// o quemada no se piden extras: primero hay que sacarla (o devolverla a la parrilla, si está cruda).
    /// </summary>
    private void RefreshPlate(CustomerSystem customers)
    {
        plate = null;
        plateHasMeat = false;
        plateHasBurnt = false;
        plateHasRaw = false;
        BuildStationSystem station = null;

        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            BuildFoodDropZone zone = zones[i];
            if (zone == null)
                continue;

            if (plate == null)
                plate = zone.PlateBody != null ? zone.PlateBody : zone.transform;
            if (zone.HasLoadedPlate)
            {
                plateHasMeat = true;
                if (station == null)
                    station = zone.BuildStation;
            }
        }

        // Rechazado o no, un plato vacío es otro plato.
        if (!plateHasMeat)
            plateRejected = false;

        plateCustomer = null;
        plateNeedsBread = false;
        missingTopping = null;
        breadTarget = null;
        sauceTarget = null;

        if (station == null)
            return;

        IReadOnlyList<MeatCutSO> plateCuts = station.AssembledCuts;
        for (int i = 0; i < plateCuts.Count; i++)
        {
            if (plateCuts[i] != null)
                cutsOnCounter.Add(plateCuts[i]);
        }

        IReadOnlyList<BuildStationSystem.CutSideStates> sides = station.AssembledCutSideStates;
        for (int i = 0; i < sides.Count; i++)
        {
            if (sides[i].IsBurned)
                plateHasBurnt = true;
            else if (sides[i].IsRaw)
                plateHasRaw = true;
        }

        plateCustomer = FindCustomerFor(plateCuts[0], customers);
        if (plateCustomer == null || plateHasBurnt || plateHasRaw)
            return;

        Order order = plateCustomer.order;
        if (order.IsSandwich && !station.HasBread)
        {
            plateNeedsBread = true;
            breadTarget = FindBread(order.bread);
        }

        for (int i = 0; i < order.toppings.Count; i++)
        {
            ToppingSO topping = order.toppings[i];
            if (topping != null && !Contains(station.AssembledToppings, topping))
            {
                missingTopping = topping;
                sauceTarget = FindTopping(topping);
                break;
            }
        }
    }

    private static Customer FindCustomerFor(MeatCutSO cut, CustomerSystem customers)
    {
        if (cut == null || customers == null)
            return null;

        IReadOnlyList<Customer> active = customers.ActiveCustomers;
        for (int i = 0; i < active.Count; i++)
        {
            Customer customer = active[i];
            if (customer != null && customer.order != null && customer.order.PrimaryCut == cut
                && customers.IsCustomerActive(customer))
                return customer;
        }
        return null;
    }

    private static bool Contains(IReadOnlyList<ToppingSO> toppings, ToppingSO topping)
    {
        for (int i = 0; i < toppings.Count; i++)
        {
            if (toppings[i] == topping)
                return true;
        }
        return false;
    }

    /// <summary>El pan pedido en el panel derecho; si no está, cualquier pan.</summary>
    private Transform FindBread(BreadSO bread)
    {
        Transform anyBread = null;
        for (int i = 0; i < panelItems.Count; i++)
        {
            BuildDraggableFoodItem item = panelItems[i];
            if (item == null || item.breadData == null || !item.gameObject.activeInHierarchy)
                continue;
            if (item.breadData == bread)
                return item.transform;
            if (anyBread == null)
                anyBread = item.transform;
        }
        return anyBread;
    }

    /// <summary>El frasco (o el topping sólido) de esa salsa en el panel derecho.</summary>
    private Transform FindTopping(ToppingSO topping)
    {
        for (int i = 0; i < sauceJars.Count; i++)
        {
            ToppingDraggable jar = sauceJars[i];
            if (jar != null && jar.ToppingData == topping && jar.gameObject.activeInHierarchy)
                return jar.transform;
        }

        for (int i = 0; i < panelItems.Count; i++)
        {
            BuildDraggableFoodItem item = panelItems[i];
            if (item != null && item.toppingData == topping && item.gameObject.activeInHierarchy)
                return item.transform;
        }
        return null;
    }

    // ── Cortes sin stock ────────────────────────────────────────────────────

    /// <summary>
    /// Clientes que piden un corte que no hay: sin stock y sin ninguno en la parrilla ni en el plato.
    /// La M va al cliente elegido (<see cref="CustomerSystem.SelectedCustomer"/>): si el que lo
    /// necesita es otro, primero hay que elegirlo. Solo los que se pueden señalar (sin panel encima).
    /// </summary>
    private void RefreshMissingCuts(CustomerSystem customers)
    {
        selectedMissingCut = null;
        otherMissingCut = null;

        CoolerSystem cooler = CoolerSystem.Instance;
        if (customers == null || cooler == null)
            return;

        Customer selected = customers.SelectedCustomer;
        IReadOnlyList<Customer> active = customers.ActiveCustomers;
        for (int i = 0; i < active.Count; i++)
        {
            Customer customer = active[i];
            if (customer == null || customer.order == null || !customers.IsCustomerActive(customer))
                continue;

            MeatCutSO cut = customer.order.PrimaryCut;
            if (cut == null || cooler.GetCount(cut) > 0 || cutsOnCounter.Contains(cut))
                continue;

            CustomerView view = customers.GetViewForCustomer(customer);
            if (view == null || !IsPickable(view))
                continue;

            if (customer == selected)
                selectedMissingCut = view;
            else if (otherMissingCut == null)
                otherMissingCut = view;
        }

        // Primero se resuelve el que ya recibe la M.
        if (selectedMissingCut != null)
            otherMissingCut = null;
    }

    // ── La jornada ──────────────────────────────────────────────────────────

    private void RefreshShift()
    {
        StrikeSystem strikes = StrikeSystem.Instance;
        bool limitReached = strikes != null && strikes.IsLimitReached;
        strikeWarning = strikes != null && strikes.CurrentStrikes > 0 && !limitReached;

        // Si cerró por strikes ya lo avisa el cartel de StrikeLimitNotice.
        DayClock clock = DayClock.Instance;
        closedWithCustomers = clock != null && clock.HasClosed && !limitReached && waitingCustomer != null;
    }
}
