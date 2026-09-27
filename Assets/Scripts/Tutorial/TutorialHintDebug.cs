using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QA de los carteles del tutorial: muestra cualquier cartel sobre cualquier objeto, sin director.
/// Menú contextual del componente (⋮), en Play. Un cartel mostrado desde acá puede retirarse solo
/// cuando llega una señal del juego (<see cref="TutorialSignals"/>), como van a hacer los de verdad.
/// </summary>
public class TutorialHintDebug : MonoBehaviour
{
    [Header("Cartel de prueba")]
    [Tooltip("Objeto del mundo o elemento de UI al que apunta el cartel.")]
    [SerializeField] private Transform target;
    [SerializeField] private HintPrompt prompt = HintPrompt.For(GameAction.ToggleStockPanel);
    [Tooltip("Clave de Tutorial.csv.")]
    [SerializeField] private string textKey = "hint.open_stock";
    [SerializeField] private HintPlacement placement = HintPlacement.Right;
    [Tooltip("Corrimiento extra, en px de la resolución de referencia.")]
    [SerializeField] private Vector2 offset;
    [Tooltip("Se retira como cumplido cuando llega esta señal. Apagado: queda hasta 'QA/Ocultar todos'.")]
    [SerializeField] private bool dismissOnSignal = true;
    [SerializeField] private TutorialSignal dismissSignal = TutorialSignal.StockPanelOpened;

    [Header("Arranque")]
    [Tooltip("Muestra Q y E al arrancar la escena, como en la primera noche. Solo para probar el cartel.")]
    [SerializeField] private bool showPanelHintsOnStart;

    private struct Pending
    {
        public TutorialHintView view;
        public int generation;
        public TutorialSignal signal;
    }

    private readonly List<Pending> pending = new List<Pending>();

    private void OnEnable() => TutorialSignals.Raised += HandleSignal;

    private void OnDisable() => TutorialSignals.Raised -= HandleSignal;

    private void Start()
    {
        if (showPanelHintsOnStart)
            ShowPanelHints();
    }

    /// <summary>
    /// Debajo de cada pestaña: al costado, el de la izquierda le tapa la cara al primer cliente.
    /// </summary>
    [ContextMenu("QA/Mostrar Q y E (se van al abrir cada panel)")]
    private void ShowPanelHints()
    {
        ShowOnPanelTab(StockPanelController.Instance, GameAction.ToggleStockPanel, "hint.open_stock",
                       HintPlacement.Below, TutorialSignal.StockPanelOpened);
        ShowOnPanelTab(ToppingsPanelController.Instance, GameAction.ToggleToppingsPanel, "hint.open_toppings",
                       HintPlacement.Below, TutorialSignal.ToppingsPanelOpened);
    }

    [ContextMenu("QA/Mostrar el cartel de prueba")]
    private void ShowTestHint()
    {
        if (target == null)
        {
            Debug.LogWarning("[TutorialHintDebug] Falta el objetivo del cartel de prueba.");
            return;
        }

        Show(target, prompt, textKey, placement, offset, dismissOnSignal, dismissSignal);
    }

    [ContextMenu("QA/Ocultar todos")]
    private void HideAll()
    {
        pending.Clear();
        if (TutorialHintLayer.Instance != null)
            TutorialHintLayer.Instance.HideAll();
    }

    private void ShowOnPanelTab(SlidingPanel panel, GameAction action, string key,
                                HintPlacement side, TutorialSignal signal)
    {
        if (panel == null || panel.IsOpen)
            return;

        StockPanelTab tab = FindTab(panel);
        if (tab == null)
        {
            Debug.LogWarning("[TutorialHintDebug] No se encontró la pestaña de " + panel.GetType().Name + ".");
            return;
        }

        Show(tab.transform, HintPrompt.For(action), key, side, Vector2.zero, true, signal);
    }

    private static StockPanelTab FindTab(SlidingPanel panel)
    {
        foreach (StockPanelTab tab in FindObjectsByType<StockPanelTab>(FindObjectsSortMode.None))
        {
            if (tab.Controller == panel)
                return tab;
        }
        return null;
    }

    private void Show(Transform hintTarget, HintPrompt hintPrompt, string key, HintPlacement side,
                      Vector2 hintOffset, bool dismissWithSignal, TutorialSignal signal)
    {
        TutorialHintLayer layer = TutorialHintLayer.Instance;
        if (layer == null)
        {
            Debug.LogWarning("[TutorialHintDebug] No hay TutorialHintLayer en la escena.");
            return;
        }

        TutorialHintView view = layer.Show(hintPrompt, key, hintTarget, side, hintOffset);
        if (view != null && dismissWithSignal)
            pending.Add(new Pending { view = view, generation = view.Generation, signal = signal });
    }

    private void HandleSignal(TutorialSignal signal, TutorialSignalArgs args)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending entry = pending[i];
            if (entry.signal != signal)
                continue;

            if (entry.view != null && entry.view.Generation == entry.generation)
                entry.view.Dismiss(true);

            pending.RemoveAt(i);
        }
    }
}
