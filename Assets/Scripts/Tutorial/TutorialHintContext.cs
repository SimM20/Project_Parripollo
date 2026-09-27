using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Foto de la partida para decidir qué carteles del tutorial mostrar y a qué apuntan. El director la
/// rearma en cada revisión (unas 5 por segundo y después de cada señal) en vez de que cada cartel
/// consulte por su cuenta. Solo lee: nunca cambia nada del juego.
///
/// Usa los registros que ya existen (slots de la parrilla, <see cref="Coal.ActiveCoals"/>, clientes
/// activos, <see cref="BuildFoodDropZone.Zones"/>). Las piezas fijas de la escena (parrilla, botón de
/// capa, pestañas) se buscan una sola vez.
/// </summary>
public class TutorialHintContext
{
    private bool bound;
    private GrillSystem grill;
    private GrillLayerToggle layerToggle;
    private Transform stockTab;
    private Transform toppingsTab;
    private Transform grillTopCenter;

    private readonly HashSet<Meat> seenMeats = new HashSet<Meat>();

    private bool stockPanelOpen;
    private bool toppingsPanelOpen;
    private bool meatLayer;
    private bool coalOnGrill;
    private bool plateHasMeat;
    private Transform plate;
    private Meat firstMeatOnGrill;
    private Meat meatToFlip;
    private Meat meatReadyForOrder;
    private CustomerView waitingCustomer;
    private CustomerView hoverableCustomer;

    public void Refresh()
    {
        if (!bound)
            Bind();

        stockPanelOpen = StockPanelController.Instance != null && StockPanelController.Instance.IsOpen;
        toppingsPanelOpen = ToppingsPanelController.Instance != null && ToppingsPanelController.Instance.IsOpen;
        meatLayer = GrillLayerToggle.IsItemTypeAllowed(ItemType.Meat);

        coalOnGrill = false;
        for (int i = 0; i < Coal.ActiveCoals.Count; i++)
        {
            Coal coal = Coal.ActiveCoals[i];
            if (coal != null && coal.state != CoalStates.Ceniza)
            {
                coalOnGrill = true;
                break;
            }
        }

        plate = null;
        plateHasMeat = false;
        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            BuildFoodDropZone zone = zones[i];
            if (zone == null)
                continue;

            if (plate == null)
                plate = zone.PlateBody != null ? zone.PlateBody : zone.transform;
            if (zone.HasLoadedPlate)
                plateHasMeat = true;
        }

        CustomerSystem customers = GameManager.Instance != null ? GameManager.Instance.Customers : null;
        RefreshCustomers(customers);
        RefreshMeats(customers);
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
            default: return false;
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
            case HintAnchorId.WaitingCustomer:
                if (hoverableCustomer == null)
                    return null;
                return hoverableCustomer.OrderBubble != null ? hoverableCustomer.OrderBubble.Panel : hoverableCustomer.transform;
            default: return null;
        }
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

            if (view.PickCollider != null && view.PickCollider.enabled)
            {
                hoverableCustomer = view;
                return;
            }
        }
    }

    private void RefreshMeats(CustomerSystem customers)
    {
        firstMeatOnGrill = null;
        meatToFlip = null;
        meatReadyForOrder = null;
        seenMeats.Clear();

        if (grill == null)
            return;

        // Un corte grande ocupa varios slots: se cuenta una sola vez.
        List<GridSlot> slots = grill.slots;
        for (int i = 0; i < slots.Count; i++)
        {
            GridSlot slot = slots[i];
            if (slot == null || slot.acceptsType != ItemType.Meat || slot.currentItem == null)
                continue;
            if (!slot.currentItem.TryGetComponent(out Meat meat) || !meat.IsOnGrill || !seenMeats.Add(meat))
                continue;

            if (firstMeatOnGrill == null)
                firstMeatOnGrill = meat;
            if (meatToFlip == null && NeedsFlip(meat))
                meatToFlip = meat;
            if (meatReadyForOrder == null && IsReadyForOrder(meat, customers))
                meatReadyForOrder = meat;
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
}
