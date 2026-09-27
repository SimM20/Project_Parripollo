using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decide qué carteles del tutorial se ven, cuándo y a qué apuntan, a partir de un set de
/// <see cref="TutorialHintSO"/>. Uno por escena, en el prefab TutorialHints junto a la capa.
///
/// Las señales del juego (<see cref="TutorialSignals"/>) solo se anotan: se procesan en el Update del
/// director, así un error acá nunca corta una acción del juego a mitad de camino. Revisa el contexto
/// unas 5 veces por segundo (tiempo sin escalar) y enseguida después de cada señal. En pausa, nada.
///
/// Reglas: un cartel aprendido no vuelve (<see cref="TutorialProgress"/>); se ven como mucho
/// <see cref="maxVisible"/>, los de mayor prioridad; el que deja de valer se retira, y si fue por algo
/// que acaba de hacer el jugador (apretó Q y se abrió el panel) sale como cumplido.
/// </summary>
public class TutorialHintDirector : MonoBehaviour
{
    [SerializeField] private TutorialHintSetSO set;
    [Tooltip("Carteles a la vista como máximo.")]
    [SerializeField] [Min(1)] private int maxVisible = 2;
    [Tooltip("Cada cuánto se revisa el contexto, en segundos sin escalar. Las señales además fuerzan una revisión.")]
    [SerializeField] [Min(0.05f)] private float evaluateInterval = 0.2f;

    private struct Shown
    {
        public TutorialHintView view;
        public int generation;
        public Transform target;

        /// <summary>El cartel sigue siendo este y está a la vista (no lo reciclaron ni lo retiraron desde afuera).</summary>
        public bool IsAlive => view != null && view.Generation == generation && view.IsShowing;
    }

    private readonly TutorialHintContext context = new TutorialHintContext();
    private readonly List<TutorialSignal> pendingSignals = new List<TutorialSignal>();
    private readonly Dictionary<TutorialHintSO, Shown> shown = new Dictionary<TutorialHintSO, Shown>();
    private readonly List<TutorialHintSO> eligible = new List<TutorialHintSO>();
    private readonly List<TutorialHintSO> retiring = new List<TutorialHintSO>();
    private float nextEvaluation;

    private void OnEnable() => TutorialSignals.Raised += HandleSignal;

    private void OnDisable()
    {
        TutorialSignals.Raised -= HandleSignal;
        pendingSignals.Clear();
        HideAll();
    }

    private void HandleSignal(TutorialSignal signal, TutorialSignalArgs args) => pendingSignals.Add(signal);

    private void Update()
    {
        if (!TutorialProgress.HintsEnabled || set == null)
        {
            pendingSignals.Clear();
            HideAll();
            return;
        }

        // En pausa no pasa nada del juego: las señales quedan anotadas para después.
        if (GamePause.IsPaused)
            return;

        bool playerActed = pendingSignals.Count > 0;
        if (!playerActed && Time.unscaledTime < nextEvaluation)
            return;

        nextEvaluation = Time.unscaledTime + evaluateInterval;
        context.Refresh();

        for (int i = 0; i < pendingSignals.Count; i++)
            LearnFromSignal(pendingSignals[i]);
        pendingSignals.Clear();

        UpdateVisibleHints(playerActed);
    }

    // ── QA ──────────────────────────────────────────────────────────────────

    /// <summary>Revisa en el próximo frame (por ejemplo, después de reiniciar las ayudas).</summary>
    public void Reevaluate() => nextEvaluation = 0f;

    /// <summary>Da por aprendidos todos los carteles del set.</summary>
    public void LearnAll()
    {
        if (set == null)
            return;

        foreach (TutorialHintSO hint in set.hints)
        {
            if (hint != null)
                TutorialProgress.MarkLearned(hint.Id);
        }
        HideAll();
    }

    // ── Aprendizaje ─────────────────────────────────────────────────────────

    private void LearnFromSignal(TutorialSignal signal)
    {
        List<TutorialHintSO> hints = set.hints;
        for (int i = 0; i < hints.Count; i++)
        {
            TutorialHintSO hint = hints[i];
            if (hint == null || hint.completeOn != signal || TutorialProgress.IsLearned(hint.Id))
                continue;
            if (!AllHold(hint.completeOnlyIf))
                continue;

            TutorialProgress.MarkLearned(hint.Id);
            Retire(hint, true);
        }
    }

    private bool AllHold(List<HintCondition> conditions)
    {
        for (int i = 0; i < conditions.Count; i++)
        {
            if (!context.Evaluate(conditions[i]))
                return false;
        }
        return true;
    }

    // ── Qué se ve ───────────────────────────────────────────────────────────

    private void UpdateVisibleHints(bool playerActed)
    {
        eligible.Clear();
        List<TutorialHintSO> hints = set.hints;
        for (int i = 0; i < hints.Count; i++)
        {
            TutorialHintSO hint = hints[i];
            if (hint != null && IsEligible(hint))
                InsertByPriority(hint);
        }

        // Se retiran los que dejaron de valer y los que siguen valiendo pero no entran en el cupo.
        retiring.Clear();
        foreach (KeyValuePair<TutorialHintSO, Shown> entry in shown)
        {
            int rank = eligible.IndexOf(entry.Key);
            if (!entry.Value.IsAlive || rank < 0 || rank >= maxVisible)
                retiring.Add(entry.Key);
        }

        for (int i = 0; i < retiring.Count; i++)
        {
            // Sale como cumplido solo si lo sacó de contexto algo que acaba de hacer el jugador. El que
            // cede su lugar a uno más importante se desvanece: todavía no se aprendió.
            bool contextLost = !eligible.Contains(retiring[i]);
            Retire(retiring[i], playerActed && contextLost);
        }

        TutorialHintLayer layer = TutorialHintLayer.Instance;
        if (layer == null)
            return;

        int count = Mathf.Min(maxVisible, eligible.Count);
        for (int i = 0; i < count; i++)
        {
            TutorialHintSO hint = eligible[i];
            Transform target = context.ResolveAnchor(hint.anchor);

            if (shown.TryGetValue(hint, out Shown current) && current.IsAlive)
            {
                if (current.target == target)
                    continue;

                // Cambió a qué apunta (se atendió al primer cliente, es otra carne): reaparece en el nuevo.
                current.view.Dismiss(false);
            }

            TutorialHintView view = layer.Show(hint.prompt, hint.textKey, target, hint.placement, hint.offset);
            if (view != null)
                shown[hint] = new Shown { view = view, generation = view.Generation, target = target };
            else
                shown.Remove(hint);
        }
    }

    /// <summary>Sin aprender, con los prerrequisitos aprendidos, con todas sus condiciones y algo a qué apuntar.</summary>
    private bool IsEligible(TutorialHintSO hint)
    {
        if (TutorialProgress.IsLearned(hint.Id))
            return false;

        for (int i = 0; i < hint.requires.Count; i++)
        {
            TutorialHintSO required = hint.requires[i];
            if (required != null && !TutorialProgress.IsLearned(required.Id))
                return false;
        }

        return AllHold(hint.showWhile) && context.ResolveAnchor(hint.anchor) != null;
    }

    /// <summary>Mayor prioridad primero; en empate queda el orden del set.</summary>
    private void InsertByPriority(TutorialHintSO hint)
    {
        int index = eligible.Count;
        while (index > 0 && eligible[index - 1].priority < hint.priority)
            index--;
        eligible.Insert(index, hint);
    }

    private void Retire(TutorialHintSO hint, bool completed)
    {
        if (!shown.TryGetValue(hint, out Shown entry))
            return;

        if (entry.IsAlive)
            entry.view.Dismiss(completed);
        shown.Remove(hint);
    }

    private void HideAll()
    {
        if (shown.Count == 0)
            return;

        retiring.Clear();
        retiring.AddRange(shown.Keys);
        for (int i = 0; i < retiring.Count; i++)
            Retire(retiring[i], false);
    }
}
