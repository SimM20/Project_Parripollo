using System.Text;
using UnityEngine;

/// <summary>
/// Arma el <see cref="HoverInfo"/> de lo que está bajo el puntero: qué es, en qué estado está y qué
/// se puede hacer con eso ahora. Solo lee el estado del juego, nunca lo cambia.
///
/// Orden: lo que se está arrastrando (carne, carbón, el plato) y el stock en la mano mandan sobre lo
/// que haya debajo, porque el click va a hacer eso. Después, el objeto bajo el puntero.
/// Los textos están en HoverInfo.csv (hover.*).
/// </summary>
public static class HoverInfoResolver
{
    /// <summary>Con menos de esta paciencia el estado del cliente sale como aviso.</summary>
    private const float LowPatience = 0.3f;

    private static readonly StringBuilder Builder = new StringBuilder(96);
    private static BuildStationSystem buildStation;

    public static bool TryResolve(GameObject hovered, GameObject pressed, HoverInfo info)
    {
        info.Clear();

        if (PlateDeliveryDraggable.IsDraggingWholePlate)
            return DraggedPlate(info);

        if (pressed != null && TryHeldPiece(pressed, info))
            return true;

        if (hovered != null)
        {
            // La celda del stock y la pestaña se leen siempre: ahí el click no pone la pieza de la mano.
            StockPanelSlot cell = hovered.GetComponentInParent<StockPanelSlot>();
            if (cell != null)
                return StockCell(cell, info);
        }

        if (StockPlacementMode.IsActive)
            return HandStock(info);

        if (hovered != null && TryObject(hovered, info))
            return true;

        // Lo de arriba no dice nada (el tablero tapa al plato): se prueba con lo que hay debajo.
        Camera cam = Camera.main;
        if (cam == null)
            return false;

        int count = Physics2D.GetRayIntersectionNonAlloc(cam.ScreenPointToRay(InputManager.PointerPosition), Hits, Mathf.Infinity);
        for (int i = 0; i < count; i++)
        {
            GameObject below = Hits[i].collider != null ? Hits[i].collider.gameObject : null;
            if (below != null && below != hovered && TryObject(below, info))
                return true;
        }
        return false;
    }

    private static readonly RaycastHit2D[] Hits = new RaycastHit2D[8];

    private static bool TryObject(GameObject hovered, HoverInfo info)
    {
        Meat meat = hovered.GetComponentInParent<Meat>();
        if (meat != null)
            return MeatPiece(meat, info);

        Coal coal = hovered.GetComponentInParent<Coal>();
        if (coal != null)
            return CoalPiece(coal, info);

        CustomerView customer = hovered.GetComponentInParent<CustomerView>();
        if (customer != null)
            return Customer(customer, info);

        GrillLayerToggle layerButton = hovered.GetComponentInParent<GrillLayerToggle>();
        if (layerButton != null)
            return LayerButton(layerButton, info);

        // El plato: la carne que ya está servida (se arrastra para entregar) o el plato mismo.
        if (hovered.GetComponentInParent<PlateDeliveryDraggable>() != null
            || hovered.GetComponentInParent<BuildFoodDropZone>() != null)
            return Plate(info);

        ToppingDraggable sauce = hovered.GetComponentInParent<ToppingDraggable>();
        if (sauce != null)
            return Sauce(sauce, info);

        BuildDraggableFoodItem food = hovered.GetComponentInParent<BuildDraggableFoodItem>();
        if (food != null)
            return Food(food, info);

        return false;
    }

    // ── Lo que está en la mano ──────────────────────────────────────────────

    private static bool TryHeldPiece(GameObject pressed, HoverInfo info)
    {
        Meat meat = pressed.GetComponentInParent<Meat>();
        if (meat != null && meat.IsHeld)
        {
            info.Title = CutName(meat.cut);
            info.HideCursorLabel = true;
            info.Add(HintInput.Drag, "hover.meat.drop");
            if (meat.CanRotate)
                info.Add(GameAction.Rotate, "hover.rotate");
            return true;
        }

        Coal coal = pressed.GetComponentInParent<Coal>();
        if (coal != null && coal.IsHeld)
        {
            info.Title = Loc.Get("item.coal");
            info.HideCursorLabel = true;
            info.Add(HintInput.Drag, "hover.coal.drop");
            return true;
        }

        return false;
    }

    private static bool DraggedPlate(HoverInfo info)
    {
        info.Title = Loc.Get("hover.plate");
        info.HideCursorLabel = true;
        info.Add(HintInput.Drag, "hover.plate.deliver_drop");
        return true;
    }

    /// <summary>Stock en la mano: el click pone la pieza en la parrilla.</summary>
    private static bool HandStock(HoverInfo info)
    {
        bool coal = StockPlacementMode.IsCoalActive;
        MeatCutSO cut = StockPlacementMode.HeldCut;

        info.Title = Loc.Format("hover.in_hand", coal ? Loc.Get("item.coal") : CutName(cut));
        info.HideCursorLabel = true;

        GrillLayerToggle.GrillLayer needed = coal ? GrillLayerToggle.GrillLayer.Coal : GrillLayerToggle.GrillLayer.Meat;
        GrillLayerToggle layers = GrillLayerToggle.Current;
        if (layers != null && layers.CurrentLayer != needed)
        {
            info.Status = Loc.Get(coal ? "hover.need_coal_layer" : "hover.need_meat_layer");
            info.Warning = true;
            info.Add(GameAction.ToggleGrillLayer, coal ? "hover.layer.to_coal" : "hover.layer.to_meat");
        }
        else
        {
            info.Status = Loc.Get(coal ? "hover.hand.coal_status" : "hover.hand.meat_status");
        }

        info.Add(HintInput.Click, "hover.place");
        if (!coal && cut != null && cut.CanRotate)
            info.Add(GameAction.Rotate, "hover.rotate");
        info.Add(HintInput.RightClick, "hover.release");
        return true;
    }

    // ── Stock ───────────────────────────────────────────────────────────────

    private static bool StockCell(StockPanelSlot cell, HoverInfo info)
    {
        ItemDataSO item = cell.Item;
        if (item == null)
            return false;

        bool coal = item is CoalSO;
        info.Title = coal ? Loc.Get("item.coal") : CutName(item as MeatCutSO);

        int count = CoolerSystem.Instance != null ? CoolerSystem.Instance.GetCount(item) : 0;
        bool armedHere = coal ? StockPlacementMode.IsCoalActive : StockPlacementMode.HeldCut == item;

        if (count <= 0)
        {
            info.Status = Loc.Get("hover.stock.empty");
            info.Warning = true;
            return true;
        }

        info.Status = Loc.Format("hover.stock.count", count);

        if (armedHere)
        {
            info.Add(HintInput.Click, "hover.release");
            return true;
        }

        info.Add(HintInput.Click, coal ? "hover.stock.take_coal" : "hover.stock.take_meat");
        if (item is MeatCutSO cut && cut.CanRotate)
            info.Add(GameAction.Rotate, "hover.rotate");
        return true;
    }

    // ── Parrilla ────────────────────────────────────────────────────────────

    private static bool MeatPiece(Meat meat, HoverInfo info)
    {
        info.Title = CutName(meat.cut);

        if (meat.IsOnGrill)
        {
            string below = MeatHoverText.GetStateDisplayName(meat.ActiveSideState);
            string above = MeatHoverText.GetStateDisplayName(meat.IsSideAActive ? meat.SideBState : meat.SideAState);

            if (meat.IsAnySideBurned)
            {
                info.Status = Loc.Get("hover.meat.burnt");
                info.Warning = true;
            }
            else if (meat.ActiveSideState == MeatStates.Pasado)
            {
                info.Status = Loc.Format("hover.meat.about_to_burn", below, above);
                info.Warning = true;
            }
            else if (!meat.IsCooking)
            {
                info.Status = Loc.Format("hover.meat.no_fire", below, above);
                info.Warning = true;
            }
            else
            {
                info.Status = Loc.Format("hover.meat.cooking", below, above);
            }

            info.Add(HintInput.RightClick, "hover.meat.flip");
            info.Add(HintInput.Drag, "hover.meat.move");
        }
        else
        {
            info.Add(HintInput.Drag, "hover.meat.move");
        }

        if (meat.CanRotate)
            info.Add(GameAction.Rotate, "hover.rotate");
        return true;
    }

    private static bool CoalPiece(Coal coal, HoverInfo info)
    {
        info.Title = Loc.Get("item.coal");

        // Las cenizas de la pila bajan el calor de las brasas que tienen encima.
        int ashes = 0;
        int stack = 0;
        GridSlot slot = coal.CurrentSlot;
        if (slot != null)
        {
            stack = slot.stackedCoals.Count;
            for (int i = 0; i < stack; i++)
            {
                if (slot.stackedCoals[i] != null && slot.stackedCoals[i].state == CoalStates.Ceniza)
                    ashes++;
            }
        }

        switch (coal.state)
        {
            case CoalStates.Encendido:
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(coal.MaxBurnTime - coal.currentBurnTime));
                info.Status = ashes > 0
                    ? Loc.Format("hover.coal.lit_with_ash", seconds)
                    : Loc.Format("hover.coal.lit", seconds);
                info.Warning = ashes > 0;
                break;
            }

            case CoalStates.Ceniza:
                info.Status = Loc.Get("hover.coal.ash");
                info.Warning = true;
                break;

            default:
                info.Status = Loc.Get("hover.coal.unlit");
                break;
        }

        if (stack > 0)
            info.Status += "  ·  " + Loc.Format("hover.coal.stack", stack);

        if (ashes > 0)
        {
            info.Add(HintInput.RightClick, "hover.coal.clean_stack");
            info.Add(GameAction.CleanAshes, "hover.coal.clean_all");
        }

        if (coal.state != CoalStates.Ceniza)
            info.Add(HintInput.Drag, "hover.coal.move");
        return true;
    }

    private static bool LayerButton(GrillLayerToggle layers, HoverInfo info)
    {
        bool onCoal = layers.CurrentLayer == GrillLayerToggle.GrillLayer.Coal;
        info.Title = Loc.Get("hover.layer");
        info.Status = Loc.Get(onCoal ? "hover.layer.now_coal" : "hover.layer.now_meat");
        info.Add(HintInput.Click, onCoal ? "hover.layer.to_meat" : "hover.layer.to_coal");
        info.Add(GameAction.ToggleGrillLayer, "hover.shortcut");
        return true;
    }

    // ── Clientes ────────────────────────────────────────────────────────────

    private static bool Customer(CustomerView view, HoverInfo info)
    {
        Customer customer = view.Customer;
        if (customer == null || customer.IsInFeedback || customer.order == null)
            return false;

        Order order = customer.order;
        MeatCutSO cut = order.PrimaryCut;

        info.Title = Loc.Get("hover.customer");

        Builder.Clear();
        Builder.Append(cut != null ? CutName(cut) : Loc.Get("order.no_cut"));
        if (cut != null)
            Builder.Append(" · ").Append(MeatHoverText.GetStateDisplayName(order.GetRequestedState(0)));
        Builder.Append(" · ").Append(order.IsSandwich
            ? Loc.Format("order.in_bread", order.bread.DisplayName)
            : Loc.Get("order.plated"));

        int patience = Mathf.RoundToInt(customer.Patience01 * 100f);
        Builder.Append("  ·  ").Append(Loc.Format("hover.customer.patience", patience));
        info.Status = Builder.ToString();
        info.Warning = customer.Patience01 < LowPatience;

        info.Add(HintInput.Drag, "hover.customer.deliver");

        bool outOfStock = cut != null && CoolerSystem.Instance != null && CoolerSystem.Instance.GetCount(cut) <= 0;
        if (outOfStock)
            info.Add(HintInput.RightClick, "hover.customer.change_order");
        return true;
    }

    // ── Plato y extras ──────────────────────────────────────────────────────

    private static bool Plate(HoverInfo info)
    {
        info.Title = Loc.Get("hover.plate");

        if (buildStation == null)
            buildStation = Object.FindObjectOfType<BuildStationSystem>();

        if (buildStation == null || buildStation.IsEmpty)
        {
            info.Status = Loc.Get("hover.plate.empty");
            return true;
        }

        Builder.Clear();
        for (int i = 0; i < buildStation.AssembledCuts.Count; i++)
        {
            if (Builder.Length > 0)
                Builder.Append(", ");
            Builder.Append(CutName(buildStation.AssembledCuts[i]));
            if (i < buildStation.AssembledCutStates.Count)
                Builder.Append(" (").Append(MeatHoverText.GetStateDisplayName(buildStation.AssembledCutStates[i])).Append(')');
        }
        if (buildStation.HasBread)
        {
            if (Builder.Length > 0)
                Builder.Append(" · ");
            Builder.Append(buildStation.AssembledBread.DisplayName);
        }
        info.Status = Builder.ToString();

        info.Add(HintInput.Drag, "hover.plate.deliver");
        info.Add(GameAction.ClearPlate, "hover.plate.clear");
        return true;
    }

    private static bool Sauce(ToppingDraggable sauce, HoverInfo info)
    {
        if (sauce.ToppingData == null)
            return false;

        info.Title = sauce.ToppingData.toppingName;
        info.Status = Loc.Format("hover.sauce.left", Mathf.RoundToInt(sauce.GetSauceRatio() * 100f));
        info.Warning = sauce.GetSauceRatio() <= 0.15f;
        info.Add(HintInput.Drag, "hover.sauce.pour");
        return true;
    }

    private static bool Food(BuildDraggableFoodItem food, HoverInfo info)
    {
        if (food.breadData != null)
            info.Title = food.breadData.DisplayName;
        else if (food.sideData != null)
            info.Title = food.sideData.DisplayName;
        else if (food.toppingData != null)
            info.Title = food.toppingData.toppingName;
        else
            return false;

        info.Add(HintInput.Drag, "hover.food.to_plate");
        return true;
    }

    private static string CutName(MeatCutSO cut) => cut != null ? cut.cutName : Loc.Get("meat.generic");
}
