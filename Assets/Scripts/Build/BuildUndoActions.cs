using UnityEngine;

/// <summary>Deshace agregar un acompañamiento: quita el dato del armado y su visual del plato.</summary>
public class AddSideUndoAction : IBuildUndoAction
{
    private readonly BuildStationSystem station;
    private readonly BuildFoodDropZone zone;
    private readonly SideSO side;
    private readonly bool hasPlateVisual;

    public AddSideUndoAction(BuildStationSystem station, BuildFoodDropZone zone, SideSO side, bool hasPlateVisual)
    {
        this.station = station;
        this.zone = zone;
        this.side = side;
        this.hasPlateVisual = hasPlateVisual;
    }

    public void Undo()
    {
        if (station != null)
            station.RemoveLastSide(side);

        if (hasPlateVisual && zone != null)
            zone.RemoveLastPlateVisual();
    }
}

/// <summary>
/// Deshace agregar un topping: quita el dato del armado, su visual del plato y,
/// si vino de un frasco vertible, los splatters de esa vertida y la salsa consumida.
/// </summary>
public class AddToppingUndoAction : IBuildUndoAction
{
    private readonly BuildStationSystem station;
    private readonly BuildFoodDropZone zone;
    private readonly ToppingSO topping;
    private readonly bool hasPlateVisual;
    private readonly ToppingDraggable source;
    private readonly int splatterStartIndex;
    private readonly float sauceAmountBefore;

    public AddToppingUndoAction(BuildStationSystem station, BuildFoodDropZone zone, ToppingSO topping,
        bool hasPlateVisual, ToppingDraggable source, int splatterStartIndex, float sauceAmountBefore)
    {
        this.station = station;
        this.zone = zone;
        this.topping = topping;
        this.hasPlateVisual = hasPlateVisual;
        this.source = source;
        this.splatterStartIndex = splatterStartIndex;
        this.sauceAmountBefore = sauceAmountBefore;
    }

    public void Undo()
    {
        if (station != null)
            station.RemoveLastTopping(topping);

        if (hasPlateVisual && zone != null)
            zone.RemoveLastPlateVisual();

        if (source != null)
        {
            source.RemoveSplattersFrom(splatterStartIndex);
            source.SetSauceAmount(sauceAmountBefore);
        }
    }
}

/// <summary>
/// Deshace asignar pan: restaura el pan previo (o ninguno) y el sprite/escala/rotación
/// que tenía el visual de carne del plato antes de transformarse en sándwich.
/// La carne nunca se elimina.
/// </summary>
public class SetBreadUndoAction : IBuildUndoAction
{
    private readonly BuildStationSystem station;
    private readonly MeatTransferBuffer meatBuffer;
    private readonly BreadSO previousBread;
    private readonly GameObject plateMeatVisual;
    private readonly Sprite previousSprite;
    private readonly Vector3 previousScale;
    private readonly Vector3 previousEuler;

    public SetBreadUndoAction(BuildStationSystem station, MeatTransferBuffer meatBuffer, BreadSO previousBread,
        GameObject plateMeatVisual, Sprite previousSprite, Vector3 previousScale, Vector3 previousEuler)
    {
        this.station = station;
        this.meatBuffer = meatBuffer;
        this.previousBread = previousBread;
        this.plateMeatVisual = plateMeatVisual;
        this.previousSprite = previousSprite;
        this.previousScale = previousScale;
        this.previousEuler = previousEuler;
    }

    public void Undo()
    {
        if (station != null)
            station.SetBread(previousBread);

        if (meatBuffer != null && plateMeatVisual != null)
            meatBuffer.RestorePlateMeatVisual(plateMeatVisual, previousSprite, previousScale, previousEuler);
    }
}
