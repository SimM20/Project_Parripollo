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
        if (!meat.IsOnGrill) return;
        if (meat.IsFlipping) return;
        if (!TutorialManager.CheckMeatFlipAllowed()) return;

        meat.Flip();
    }
}
