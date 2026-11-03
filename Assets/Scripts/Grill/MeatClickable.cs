using UnityEngine;

public class MeatClickable : MonoBehaviour, IWorldPointerOverHandler
{
    private Meat meat;

    void Awake()
    {
        meat = GetComponent<Meat>();
    }

    public void OnWorldPointerOver()
    {
        if (meat == null) return;
        if (!InputManager.SecondaryPressed) return;
        // Con stock en la mano el click derecho lo suelta (StockPlacementMode): no da vuelta la carne de abajo.
        if (StockPlacementMode.IsActive) return;
        if (!meat.IsOnGrill) return;
        if (meat.IsFlipping) return;

        meat.Flip();
    }
}
