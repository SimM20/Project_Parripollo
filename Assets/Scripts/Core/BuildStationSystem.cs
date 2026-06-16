using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildStationSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FoodCatalogSO catalog;

    private readonly List<MeatCutSO> assembledCuts = new List<MeatCutSO>();
    private readonly List<MeatStates> assembledCutStates = new List<MeatStates>();
    private BreadSO assembledBread = null;
    private readonly List<SideSO> assembledSides = new List<SideSO>();
    private readonly List<ToppingSO> assembledToppings = new List<ToppingSO>();

    public event Action OnAssemblyChanged;


    public IReadOnlyList<MeatCutSO> AssembledCuts => assembledCuts;
    public IReadOnlyList<MeatStates> AssembledCutStates => assembledCutStates;
    public BreadSO AssembledBread => assembledBread;
    public IReadOnlyList<SideSO> AssembledSides => assembledSides;
    public IReadOnlyList<ToppingSO> AssembledToppings => assembledToppings;
    public bool HasAnyCut => assembledCuts.Count > 0;
    public bool HasBread => assembledBread != null;


    public void AddCut(MeatCutSO cut, MeatStates state = MeatStates.Crudo)
    {
        if (cut == null) return;
        assembledCuts.Add(cut);
        assembledCutStates.Add(state);
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Corte agregado: " + cut.cutName + " (Estado: " + state + ")");
    }

    public void RemoveLastCut()
    {
        if (assembledCuts.Count == 0) return;
        assembledCuts.RemoveAt(assembledCuts.Count - 1);
        if (assembledCutStates.Count > assembledCuts.Count)
            assembledCutStates.RemoveAt(assembledCutStates.Count - 1);
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
        assembledCutStates.Clear();
        assembledBread = null;
        assembledSides.Clear();
        assembledToppings.Clear();
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Armado limpiado.");
    }

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
