using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un cartel del tutorial: qué muestra, dónde, cuándo se ve y qué lo da por aprendido.
///
/// Se muestra mientras sus prerrequisitos estén aprendidos y valgan todas sus condiciones
/// (<see cref="showWhile"/>); si una deja de valer, se retira sin darse por aprendido. Se aprende con
/// la señal <see cref="completeOn"/>, se esté viendo o no: si el jugador ya lo hizo, no aparece.
/// Lo maneja <see cref="TutorialHintDirector"/>.
/// </summary>
[CreateAssetMenu(fileName = "NewTutorialHint", menuName = "Tutorial/Cartel")]
public class TutorialHintSO : ScriptableObject
{
    [Tooltip("Id estable: con él se guarda que el cartel ya se aprendió. Vacío = el nombre del asset. " +
             "No cambiarlo una vez publicado.")]
    [SerializeField] private string id;

    [Header("Qué muestra")]
    public HintPrompt prompt;
    [Tooltip("Clave de Tutorial.csv (hint.*). De 2 a 4 palabras: el ícono ya dice qué tecla.")]
    public string textKey;

    [Header("Dónde")]
    public HintAnchorId anchor;
    public HintPlacement placement = HintPlacement.Above;
    [Tooltip("Corrimiento extra, en px de la resolución de referencia (1920×1080).")]
    public Vector2 offset;

    [Header("Cuándo se muestra")]
    [Tooltip("Carteles que tienen que estar aprendidos antes.")]
    public List<TutorialHintSO> requires = new List<TutorialHintSO>();
    [Tooltip("Se ve solo mientras valen todas. Si una deja de valer, se retira sin darse por aprendido.")]
    public List<HintCondition> showWhile = new List<HintCondition>();
    [Tooltip("Con más carteles posibles que lugares, se ven los de mayor prioridad. " +
             "En empate gana el que está antes en el set.")]
    public int priority;

    [Header("Cuándo se aprende")]
    [Tooltip("La señal del juego que lo da por aprendido, se esté viendo o no.")]
    public TutorialSignal completeOn;
    [Tooltip("Además de la señal, tienen que valer todas en ese momento. Vacía = alcanza con la señal. " +
             "Ojo: algunas señales también salen al arrancar la escena (GrillLayerChanged, con la capa inicial).")]
    public List<HintCondition> completeOnlyIf = new List<HintCondition>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
}
