using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildStationSystem : MonoBehaviour
{
    /// <summary>Estado de cocción de ambas caras de un corte armado.</summary>
    public struct CutSideStates
    {
        public MeatStates sideA;
        public MeatStates sideB;

        public CutSideStates(MeatStates a, MeatStates b)
        {
            sideA = a;
            sideB = b;
        }

        public bool IsBurned => sideA == MeatStates.Quemado || sideB == MeatStates.Quemado;
        public bool IsRaw => !IsBurned && (sideA == MeatStates.Crudo || sideB == MeatStates.Crudo);
    }

    [Header("References")]
    [SerializeField] private FoodCatalogSO catalog;

    private readonly List<MeatCutSO> assembledCuts = new List<MeatCutSO>();
    private readonly List<MeatStates> assembledCutStates = new List<MeatStates>();
    private readonly List<CutSideStates> assembledCutSideStates = new List<CutSideStates>();
    private BreadSO assembledBread = null;
    private readonly List<SideSO> assembledSides = new List<SideSO>();
    private readonly List<ToppingSO> assembledToppings = new List<ToppingSO>();

    public event Action OnAssemblyChanged;

    /// <summary>Disparado al limpiar el armado completo (entrega o reinicio). Usado para invalidar el historial de undo.</summary>
    public event Action OnAssemblyCleared;


    public IReadOnlyList<MeatCutSO> AssembledCuts => assembledCuts;
    public IReadOnlyList<MeatStates> AssembledCutStates => assembledCutStates;
    public IReadOnlyList<CutSideStates> AssembledCutSideStates => assembledCutSideStates;
    public BreadSO AssembledBread => assembledBread;
    public IReadOnlyList<SideSO> AssembledSides => assembledSides;
    public IReadOnlyList<ToppingSO> AssembledToppings => assembledToppings;
    public bool HasAnyCut => assembledCuts.Count > 0;
    public bool HasBread => assembledBread != null;


    public void AddCut(MeatCutSO cut, MeatStates state = MeatStates.Crudo)
    {
        AddCut(cut, state, state, state);
    }

    public void AddCut(MeatCutSO cut, MeatStates state, MeatStates sideAState, MeatStates sideBState)
    {
        if (cut == null) return;
        assembledCuts.Add(cut);
        assembledCutStates.Add(state);
        assembledCutSideStates.Add(new CutSideStates(sideAState, sideBState));
        OnAssemblyChanged?.Invoke();
        Debug.Log("[BuildStation] Corte agregado: " + cut.cutName + " (Estado: " + state
                  + " | A: " + sideAState + " | B: " + sideBState + ")");
    }

    public void RemoveLastCut()
    {
        if (assembledCuts.Count == 0) return;
        assembledCuts.RemoveAt(assembledCuts.Count - 1);
        if (assembledCutStates.Count > assembledCuts.Count)
            assembledCutStates.RemoveAt(assembledCutStates.Count - 1);
        if (assembledCutSideStates.Count > assembledCuts.Count)
            assembledCutSideStates.RemoveAt(assembledCutSideStates.Count - 1);
        OnAssemblyChanged?.Invoke();
    }

    /// <summary>Elimina un corte puntual del armado (descarte de quemados). No toca pan, sides ni toppings.</summary>
    public void RemoveCutAt(int index)
    {
        if (index < 0 || index >= assembledCuts.Count) return;
        assembledCuts.RemoveAt(index);
        if (index < assembledCutStates.Count)
            assembledCutStates.RemoveAt(index);
        if (index < assembledCutSideStates.Count)
            assembledCutSideStates.RemoveAt(index);
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

    /// <summary>Quita la última aparición del acompañamiento indicado. Usado por el undo. No toca cortes.</summary>
    public void RemoveLastSide(SideSO side)
    {
        if (side == null) return;

        for (int i = assembledSides.Count - 1; i >= 0; i--)
        {
            if (assembledSides[i] == side)
            {
                assembledSides.RemoveAt(i);
                OnAssemblyChanged?.Invoke();
                return;
            }
        }
    }

    /// <summary>Quita la última aparición del topping indicado. Usado por el undo. No toca cortes.</summary>
    public void RemoveLastTopping(ToppingSO topping)
    {
        if (topping == null) return;

        for (int i = assembledToppings.Count - 1; i >= 0; i--)
        {
            if (assembledToppings[i] == topping)
            {
                assembledToppings.RemoveAt(i);
                OnAssemblyChanged?.Invoke();
                return;
            }
        }
    }

    /// <summary>Clears all assembled items.</summary>
    public void ClearAssembly()
    {
        assembledCuts.Clear();
        assembledCutStates.Clear();
        assembledCutSideStates.Clear();
        assembledBread = null;
        assembledSides.Clear();
        assembledToppings.Clear();
        OnAssemblyCleared?.Invoke();
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
