using System;
using UnityEngine;

/// <summary>
/// Cosas que hizo el jugador (o que pasaron en la partida) y que le importan al tutorial.
/// ⚠️ Los carteles del tutorial la van a serializar como int: los valores nuevos van siempre al final.
/// </summary>
public enum TutorialSignal
{
    StockPanelOpened,
    /// <summary>Un corte llegó a la parrilla arrastrado desde el stock (o desde la cola legada de la heladera).</summary>
    MeatDraggedToGrill,
    /// <summary>Un corte quedó en la parrilla, venga del stock, de la bandeja o del plato.</summary>
    MeatPlacedOnGrill,
    CoalDraggedToGrill,
    CoalPlacedOnGrill,
    GrillLayerChanged,
    MeatFlipped,
    /// <summary>Cambió el punto de la cara activa de una carne.</summary>
    MeatStateChanged,
    /// <summary>Un corte pasó de la parrilla al plato.</summary>
    MeatDraggedToBuild,
    /// <summary>Un corte quedó en el plato, venga de la parrilla o de la bandeja.</summary>
    MeatPlacedOnBuildZone,
    /// <summary>Se agarró el plato entero para entregarlo.</summary>
    DeliverySelectionBegun,
    /// <summary>Entrega aceptada y cobrada. Una entrega cruda o quemada no cuenta.</summary>
    ProductDelivered,
    ToppingsPanelOpened,
}

/// <summary>Datos de una señal. Cada una llena solo lo que tiene sentido; el resto queda en null.</summary>
public readonly struct TutorialSignalArgs
{
    public readonly MeatCutSO Cut;
    public readonly CoalSO Coal;
    public readonly Meat Meat;
    /// <summary>Solo en <see cref="TutorialSignal.GrillLayerChanged"/>.</summary>
    public readonly GrillLayerToggle.GrillLayer Layer;
    /// <summary>Lo que conviene señalar con un cartel: la carne, el carbón, la carne ya en el plato. Puede ser null.</summary>
    public readonly Transform Target;

    public TutorialSignalArgs(MeatCutSO cut, CoalSO coal, Meat meat, GrillLayerToggle.GrillLayer layer, Transform target)
    {
        Cut = cut;
        Coal = coal;
        Meat = meat;
        Layer = layer;
        Target = target;
    }
}

/// <summary>
/// Punto único por donde el juego avisa al tutorial. Reemplaza a los viejos
/// <c>TutorialManager.Notify*</c>: el juego no sabe quién escucha (los carteles contextuales,
/// el TutorialManager de las escenas de tutorial o nadie).
///
/// Las señales se disparan en el mismo frame y en el mismo orden en que pasan las cosas.
/// Ninguna sale de un <c>Update</c>: nada de esto corre por frame.
/// </summary>
public static class TutorialSignals
{
    public static event Action<TutorialSignal, TutorialSignalArgs> Raised;

    // Con "Enter Play Mode" sin domain reload el estático sobrevive entre sesiones de play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Raised = null;

    /// <summary>
    /// Avisa que pasó algo. Sin nadie escuchando no hace nada. Con <paramref name="meat"/>, el
    /// corte y el objetivo del cartel salen de esa carne si no se pasan aparte.
    /// </summary>
    public static void Raise(
        TutorialSignal signal,
        MeatCutSO cut = null,
        CoalSO coal = null,
        Meat meat = null,
        GrillLayerToggle.GrillLayer layer = default,
        Transform target = null)
    {
        if (Raised == null)
            return;

        if (meat != null)
        {
            if (cut == null) cut = meat.cut;
            if (target == null) target = meat.transform;
        }

        Raised(signal, new TutorialSignalArgs(cut, coal, meat, layer, target));
    }
}
