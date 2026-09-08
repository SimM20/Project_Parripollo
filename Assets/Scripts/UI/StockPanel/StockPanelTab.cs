using UnityEngine;

/// <summary>
/// Pestaña lateral que abre y cierra un panel deslizante al hacer clic.
/// Sirve para cualquier SlidingPanel (stock, items de armado).
/// </summary>
public class StockPanelTab : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SlidingPanel controller;

    void Awake()
    {
        EnsureCollider2D();
    }

    void Start()
    {
        if (controller == null)
            Debug.LogWarning("[StockPanelTab] Falta la referencia 'controller'. La pestaña no abre ningún panel.");
    }

    void OnMouseDown()
    {
        if (controller == null)
            return;

        // El gate del tutorial vive en SlidingPanel.CanOpen(): Toggle() se rechaza solo.
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
