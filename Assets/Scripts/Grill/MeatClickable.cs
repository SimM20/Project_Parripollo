using UnityEngine;

public class MeatClickable : MonoBehaviour
{
    private const int RightMouseButton = 1;

    private Meat meat;

    void Awake()
    {
        meat = GetComponent<Meat>();
    }

    void OnMouseOver()
    {
        if (meat == null) return;
        if (!Input.GetMouseButtonDown(RightMouseButton)) return;
        if (!meat.IsOnGrill) return;
        if (!TutorialManager.CheckMeatFlipAllowed()) return;

        meat.Flip();
    }
}
