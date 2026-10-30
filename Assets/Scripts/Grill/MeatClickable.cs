using UnityEngine;

public class MeatClickable : MonoBehaviour
{
    private Meat meat;

    void Awake()
    {
        meat = GetComponent<Meat>();
    }

    void OnWorldPointerOver()
    {
        if (meat == null) return;
        if (!InputManager.SecondaryPressed) return;
        // Con stock en la mano el click derecho lo suelta (StockPlacementMode): no da vuelta la carne de abajo.
        if (StockPlacementMode.IsActive) return;
        if (!meat.IsOnGrill) return;
        if (meat.IsFlipping) return;
        if (!TutorialManager.CheckMeatFlipAllowed()) return;

        meat.Flip();
    }
}
