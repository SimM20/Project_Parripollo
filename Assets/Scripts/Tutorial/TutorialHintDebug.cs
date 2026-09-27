using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// QA de los carteles del tutorial. Menú contextual del componente (⋮), en Play: reiniciar o saltear
/// lo aprendido, y mostrar un cartel de prueba sobre cualquier objeto, por fuera del director. El
/// cartel de prueba puede retirarse solo cuando llega una señal del juego, como los de verdad.
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
    [Tooltip("Se retira como cumplido cuando llega esta señal. Apagado: queda hasta 'QA/Ocultar los carteles'.")]
    [SerializeField] private bool dismissOnSignal = true;
    [SerializeField] private TutorialSignal dismissSignal = TutorialSignal.StockPanelOpened;

    private struct Pending
    {
        public TutorialHintView view;
        public int generation;
        public TutorialSignal signal;
    }

    private readonly List<Pending> pending = new List<Pending>();

    private void OnEnable() => TutorialSignals.Raised += HandleSignal;

    private void OnDisable() => TutorialSignals.Raised -= HandleSignal;

    [ContextMenu("QA/Reiniciar las ayudas (todo sin aprender)")]
    private void ResetProgress()
    {
        TutorialProgress.Reset();
        TutorialHintDirector director = GetComponent<TutorialHintDirector>();
        if (director != null)
            director.Reevaluate();
    }

    [ContextMenu("QA/Dar todas por aprendidas")]
    private void LearnAll()
    {
        TutorialHintDirector director = GetComponent<TutorialHintDirector>();
        if (director != null)
            director.LearnAll();
    }

    [ContextMenu("QA/Mostrar el cartel de prueba")]
    private void ShowTestHint()
    {
        TutorialHintLayer layer = TutorialHintLayer.Instance;
        if (layer == null || target == null)
        {
            Debug.LogWarning("[TutorialHintDebug] Falta la capa de carteles o el objetivo del cartel de prueba.");
            return;
        }

        TutorialHintView view = layer.Show(prompt, textKey, target, placement, offset);
        if (view != null && dismissOnSignal)
            pending.Add(new Pending { view = view, generation = view.Generation, signal = dismissSignal });
    }

    /// <summary>Los del director vuelven en la próxima revisión si siguen valiendo.</summary>
    [ContextMenu("QA/Ocultar los carteles")]
    private void HideAll()
    {
        pending.Clear();
        if (TutorialHintLayer.Instance != null)
            TutorialHintLayer.Instance.HideAll();
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
