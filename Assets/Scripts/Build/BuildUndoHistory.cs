using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Acción reversible del armado del plato (comando de undo).</summary>
public interface IBuildUndoAction
{
    void Undo();
}

/// <summary>
/// Pila de historial de acciones reversibles del armado.
/// Solo registra panes, acompañamientos y toppings — nunca la carne.
/// Se limpia sola cuando el armado se limpia (entrega o reinicio del plato).
/// </summary>
public class BuildUndoHistory : MonoBehaviour
{
    public static BuildUndoHistory Instance { get; private set; }

    [SerializeField] private BuildStationSystem buildStationSystem;

    private readonly List<IBuildUndoAction> undoStack = new List<IBuildUndoAction>();

    public event Action OnHistoryChanged;

    public int Count => undoStack.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (buildStationSystem == null)
            buildStationSystem = GetComponent<BuildStationSystem>();
    }

    void OnEnable()
    {
        if (buildStationSystem != null)
            buildStationSystem.OnAssemblyCleared += Clear;
    }

    void OnDisable()
    {
        if (buildStationSystem != null)
            buildStationSystem.OnAssemblyCleared -= Clear;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Push(IBuildUndoAction action)
    {
        if (action == null) return;
        undoStack.Add(action);
        OnHistoryChanged?.Invoke();
    }

    /// <summary>Deshace exactamente la última acción registrada. Sin acciones, no hace nada.</summary>
    public void UndoLast()
    {
        if (undoStack.Count == 0) return;

        int last = undoStack.Count - 1;
        IBuildUndoAction action = undoStack[last];
        undoStack.RemoveAt(last);
        action.Undo();
        OnHistoryChanged?.Invoke();
        Debug.Log("[BuildUndo] Acción deshecha. Restantes: " + undoStack.Count);
    }

    public void Clear()
    {
        if (undoStack.Count == 0) return;
        undoStack.Clear();
        OnHistoryChanged?.Invoke();
    }
}
