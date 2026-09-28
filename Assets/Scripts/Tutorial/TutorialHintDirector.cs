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
/// <see cref="maxVisible"/>, los de mayor prioridad, y nunca dos sobre el mismo objeto; el que deja de
/// valer se retira, y si fue por algo que acaba de hacer el jugador (apretó Q y se abrió el panel) sale
/// como cumplido. Los que tienen <see cref="TutorialHintSO.completeAfterSeconds"/> se aprenden solos
/// después de verse ese tiempo.
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

    private struct PendingSignal
    {
        public TutorialSignal signal;
        public TutorialSignalArgs args;
    }

    private readonly TutorialHintContext context = new TutorialHintContext();
    private readonly List<PendingSignal> pendingSignals = new List<PendingSignal>();
    private readonly Dictionary<TutorialHintSO, Shown> shown = new Dictionary<TutorialHintSO, Shown>();
    private readonly Dictionary<TutorialHintSO, float> seenSeconds = new Dictionary<TutorialHintSO, float>();
    private readonly List<TutorialHintSO> eligible = new List<TutorialHintSO>();
    private readonly List<TutorialHintSO> visible = new List<TutorialHintSO>();
    private readonly List<Transform> visibleTargets = new List<Transform>();
    private readonly List<TutorialHintSO> retiring = new List<TutorialHintSO>();
    private float nextEvaluation;

    private void OnEnable()
    {
        TutorialSignals.Raised += HandleSignal;
        TutorialProgress.OnReset += HandleProgressReset;
    }

    private void OnDisable()
    {
        TutorialSignals.Raised -= HandleSignal;
        TutorialProgress.OnReset -= HandleProgressReset;
        pendingSignals.Clear();
        HideAll();
    }

    private void HandleSignal(TutorialSignal signal, TutorialSignalArgs args) =>
        pendingSignals.Add(new PendingSignal { signal = signal, args = args });

    /// <summary>El tutorial arranca de nuevo (Opciones o QA): el tiempo que se vio cada aviso también.</summary>
    private void HandleProgressReset()
    {
        seenSeconds.Clear();
        Reevaluate();
    }

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

        AccumulateSeenTime(Time.unscaledDeltaTime);

        bool playerActed = pendingSignals.Count > 0;
        if (!playerActed && Time.unscaledTime < nextEvaluation)
            return;

        nextEvaluation = Time.unscaledTime + evaluateInterval;
        context.Refresh();

        for (int i = 0; i < pendingSignals.Count; i++)
        {
            context.Observe(pendingSignals[i].signal, pendingSignals[i].args);
            LearnFromSignal(pendingSignals[i].signal);
        }
        pendingSignals.Clear();

        LearnFromTime();
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
            if (hint == null || hint.completeOn == TutorialSignal.None || hint.completeOn != signal
                || TutorialProgress.IsLearned(hint.Id))
                continue;
            if (!AllHold(hint.completeOnlyIf))
                continue;

            TutorialProgress.MarkLearned(hint.Id);
            Retire(hint, true);
        }
    }

    /// <summary>Suma el tiempo a la vista de los carteles que se aprenden por tiempo.</summary>
    private void AccumulateSeenTime(float deltaTime)
    {
        foreach (KeyValuePair<TutorialHintSO, Shown> entry in shown)
        {
            if (entry.Key.completeAfterSeconds <= 0f || !entry.Value.IsAlive)
                continue;

            seenSeconds.TryGetValue(entry.Key, out float seen);
            seenSeconds[entry.Key] = seen + deltaTime;
        }
    }

    /// <summary>Los que ya se vieron su tiempo se dan por aprendidos y se desvanecen: no pedían nada.</summary>
    private void LearnFromTime()
    {
        retiring.Clear();
        foreach (KeyValuePair<TutorialHintSO, float> entry in seenSeconds)
        {
            if (entry.Value >= entry.Key.completeAfterSeconds)
                retiring.Add(entry.Key);
        }

        for (int i = 0; i < retiring.Count; i++)
        {
            TutorialHintSO hint = retiring[i];
            seenSeconds.Remove(hint);
            TutorialProgress.MarkLearned(hint.Id);
            Retire(hint, false);
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

        // Entran los de mayor prioridad, uno por objeto: dos carteles sobre la misma pestaña o el
        // mismo cliente se tapan entre sí.
        visible.Clear();
        visibleTargets.Clear();
        for (int i = 0; i < eligible.Count && visible.Count < maxVisible; i++)
        {
            Transform target = context.ResolveAnchor(eligible[i].anchor);
            if (visibleTargets.Contains(target))
                continue;

            visible.Add(eligible[i]);
            visibleTargets.Add(target);
        }

        // Se retiran los que dejaron de valer y los que siguen valiendo pero no entran.
        retiring.Clear();
        foreach (KeyValuePair<TutorialHintSO, Shown> entry in shown)
        {
            if (!entry.Value.IsAlive || !visible.Contains(entry.Key))
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

        for (int i = 0; i < visible.Count; i++)
        {
            TutorialHintSO hint = visible[i];
            Transform target = visibleTargets[i];

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
