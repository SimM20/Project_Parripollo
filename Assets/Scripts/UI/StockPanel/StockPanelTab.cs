using UnityEngine;

/// <summary>
/// Pestaña lateral que abre y cierra el panel de stock al hacer clic.
/// </summary>
public class StockPanelTab : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StockPanelController controller;

    void Awake()
    {
        EnsureCollider2D();
    }

    void Start()
    {
        if (controller == null)
            Debug.LogWarning("[StockPanelTab] Falta la referencia 'controller'. La pestaña no abre el panel de stock.");
    }

    void OnMouseDown()
    {
        if (controller == null)
            return;

        if (!controller.IsOpen && !TutorialManager.CheckStockPanelOpenAllowed())
            return;

        controller.Toggle();
    }

    private void EnsureCollider2D()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
            return;

        box = gameObject.AddComponent<BoxCollider2D>();
        box.size = Vector2.one;
    }
}
