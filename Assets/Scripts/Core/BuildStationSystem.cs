using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight container for the dish being assembled at the Build station.
/// Holds the current assembled state (cuts, optional bread, sides, toppings)
/// and provides validation via DishValidator.
/// Does not implement new drag/drop behavior — only tracks what has been placed.
/// </summary>
public class BuildStationSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FoodCatalogSO catalog;

    // Runtime assembled state
    private readonly List<MeatCutSO> assembledCuts = new List<MeatCutSO>();
    private BreadSO assembledBread = null;
    private readonly List<SideSO> assembledSides = new List<SideSO>();
    private readonly List<ToppingSO> assembledToppings = new List<ToppingSO>();

    public event Action OnAssemblyChanged;

    // ── Assembly state ──────────────────────────────────────────────────────

    public IReadOnlyList<MeatCutSO> AssembledCuts => assembledCuts;
    public BreadSO AssembledBread => assembledBread;
    public IReadOnlyList<SideSO> AssembledSides => assembledSides;
    public IReadOnlyList<ToppingSO> AssembledToppings => assembledToppings;
    public bool HasAnyCut => assembledCuts.Count > 0;
    public bool HasBread => assembledBread != null;

    // ── Add / Remove ────────────────────────────────────────────────────────

    public void AddCut(MeatCutSO cut)
    {
        if (cut == null) return;
        assembledCuts.Add(cut);
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Corte agregado: " + cut.cutName);
    }

    public void RemoveLastCut()
    {
        if (assembledCuts.Count == 0) return;
        assembledCuts.RemoveAt(assembledCuts.Count - 1);
        OnAssemblyChanged?.Invoke();
    }

    public void SetBread(BreadSO bread)
    {
        assembledBread = bread;
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Pan asignado: " + (bread != null ? bread.breadName : "ninguno"));
    }

    public void AddSide(SideSO side)
    {
        if (side == null) return;
        assembledSides.Add(side);
        OnAssemblyChanged?.Invoke();
    }

    public void AddTopping(ToppingSO topping)
    {
        if (topping == null) return;
        assembledToppings.Add(topping);
        OnAssemblyChanged?.Invoke();
    }

    /// <summary>Clears all assembled items.</summary>
    public void ClearAssembly()
    {
        assembledCuts.Clear();
        assembledBread = null;
        assembledSides.Clear();
        assembledToppings.Clear();
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Armado limpiado.");
    }

    // ── Build and validate ──────────────────────────────────────────────────

    /// <summary>
    /// Builds and validates the current assembly as a PlatedDish.
    /// Returns null if invalid.
    /// </summary>
    public PlatedDish TryBuildPlatedDish(out string reason)
    {
        if (assembledCuts.Count == 0)
        {
            reason = "No hay cortes en el armado.";
            return null;
        }

        var dish = new PlatedDish();
        dish.cuts.AddRange(assembledCuts);
        dish.sides.AddRange(assembledSides);
        dish.toppings.AddRange(assembledToppings);

        if (!DishValidator.ValidatePlatedDish(dish, out reason))
            return null;

        return dish;
    }

    /// <summary>
    /// Builds and validates the current assembly as a Sandwich.
    /// Returns null if invalid.
    /// </summary>
    public Sandwich TryBuildSandwich(out string reason)
    {
        if (assembledCuts.Count == 0)
        {
            reason = "No hay cortes en el armado.";
            return null;
        }

        if (assembledBread == null)
        {
            reason = "No hay pan en el armado.";
            return null;
        }

        var sandwich = new Sandwich(assembledCuts[0], assembledBread);
        sandwich.toppings.AddRange(assembledToppings);

        if (!DishValidator.ValidateSandwich(sandwich, out reason))
            return null;

        return sandwich;
    }

    /// <summary>
    /// Resolves the matching ProductVariantSO for the current assembly (plated or sandwich).
    /// Returns null if not resolvable.
    /// </summary>
    public ProductVariantSO TryResolveVariant()
    {
        if (catalog == null || !HasAnyCut) return null;

        if (HasBread)
        {
            var sandwich = new Sandwich(assembledCuts[0], assembledBread);
            return DishValidator.ResolveVariant(sandwich, catalog);
        }
        else
        {
            var dish = new PlatedDish();
            dish.cuts.AddRange(assembledCuts);
            return DishValidator.ResolveVariant(dish, catalog);
        }
    }

    // ── Debug ───────────────────────────────────────────────────────────────

    [ContextMenu("Debug: Print Assembly")]
    public void DebugPrintAssembly()
    {
        var sb = new System.Text.StringBuilder("[BuildStation] Armado actual:\n");
        sb.AppendLine("  Cortes: " + assembledCuts.Count);
        for (int i = 0; i < assembledCuts.Count; i++)
            sb.AppendLine("    - " + assembledCuts[i].cutName);
        sb.AppendLine("  Pan: " + (assembledBread != null ? assembledBread.breadName : "ninguno"));
        sb.AppendLine("  Acompañamientos: " + assembledSides.Count);
        sb.AppendLine("  Toppings: " + assembledToppings.Count);
        Debug.Log(sb.ToString());
    }
}
