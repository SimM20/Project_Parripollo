using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Permite entregar el plato armado arrastrándolo con el mouse hasta un cliente.
/// Se agrega en runtime a cada visual de carne que queda en la zona del plato
/// (ver MeatTransferBuffer.AdoptVisualIntoPlate), así que no necesita setup de escena.
///
/// Es la unica via de entrega: termina en GameManager.TryDeliverToCustomer.
///
/// Al arrastrar se mueve el plato completo como un bloque (visuales de carne +
/// acompañamientos/toppings). Si la entrega no se concreta, todo vuelve a su
/// posición original sobre el plato.
/// </summary>
public class PlateDeliveryDraggable : MonoBehaviour
{
    private const int DragSortingBoost = 5000;

    private struct DraggedVisual
    {
        public Transform target;
        public Vector3 startPosition;
        public SpriteRenderer renderer;
        public int startSortingOrder;
    }

    private static readonly List<PlateDeliveryDraggable> Instances = new List<PlateDeliveryDraggable>();
    private static readonly List<DraggedVisual> DraggedVisuals = new List<DraggedVisual>();
    private static readonly List<Transform> PlateItemVisuals = new List<Transform>();
    private static readonly Collider2D[] OverlapResults = new Collider2D[16];

    private SpriteRenderer selfRenderer;
    private BoxCollider2D selfCollider;
    private Vector3 grabWorldPoint;
    private bool dragging;
    private CustomerView hoveredView;

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();

        selfCollider = GetComponent<BoxCollider2D>();
        if (selfCollider == null)
            selfCollider = gameObject.AddComponent<BoxCollider2D>();

        RefreshCollider();

        if (!Instances.Contains(this))
            Instances.Add(this);
    }

    void OnDestroy()
    {
        Instances.Remove(this);
    }

    /// <summary>
    /// Reajusta el collider al sprite actual. El pan reemplaza sprite, escala y rotación
    /// del visual del plato, así que hay que volver a medir para poder agarrarlo.
    /// </summary>
    public void RefreshCollider()
    {
        if (selfCollider == null)
            return;

        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        if (selfRenderer != null && selfRenderer.sprite != null)
        {
            selfCollider.size = (Vector2)selfRenderer.sprite.bounds.size * 1.2f;
            selfCollider.offset = selfRenderer.sprite.bounds.center;
        }
        else
        {
            selfCollider.size = Vector2.one;
            selfCollider.offset = Vector2.zero;
        }
    }

    void OnMouseDown()
    {
        BeginDrag();
    }

    void OnMouseDrag()
    {
        if (!dragging)
            return;

        Vector3 mouseWorld = GetMouseWorldPos();
        Vector3 delta = mouseWorld - grabWorldPoint;

        for (int i = 0; i < DraggedVisuals.Count; i++)
        {
            Transform target = DraggedVisuals[i].target;
            if (target == null)
                continue;

            target.position = DraggedVisuals[i].startPosition + delta;
        }

        SetHoveredView(FindCustomerViewAt(mouseWorld));
    }

    void OnMouseUp()
    {
        if (!dragging)
            return;

        dragging = false;
        RestoreSortingOrders();

        Vector3 dropPoint = GetMouseWorldPos();
        CustomerView dropView = FindCustomerViewAt(dropPoint);

        SetHoveredView(null);

        if (!TutorialManager.CheckDeliveryConfirmAllowed())
        {
            RestorePositions();
            DraggedVisuals.Clear();
            return;
        }

        bool delivered = dropView != null
            && dropView.Customer != null
            && GameManager.Instance != null
            && GameManager.Instance.TryDeliverToCustomer(dropView.Customer);

        // Si la entrega no se concreta: verificar si se soltó sobre el MeatHolder / MeatList para devolver la carne.
        if (!delivered)
        {
            MeatTransferBuffer transferBuffer = Object.FindAnyObjectByType<MeatTransferBuffer>();
            if (transferBuffer != null && transferBuffer.IsOverMeatTray(dropPoint))
            {
                RestorePositions();
                transferBuffer.TryReturnPlateMeatToTray(gameObject);
            }
            else
            {
                RestorePositions();
            }
        }

        DraggedVisuals.Clear();
    }

    private void BeginDrag()
    {
        if (!TutorialManager.CheckDeliveryConfirmAllowed() && !TutorialManager.CheckDeliveryStartAllowed())
            return;

        DraggedVisuals.Clear();
        grabWorldPoint = GetMouseWorldPos();

        for (int i = 0; i < Instances.Count; i++)
        {
            PlateDeliveryDraggable instance = Instances[i];
            if (instance == null || !instance.gameObject.activeInHierarchy)
                continue;

            AddDraggedVisual(instance.transform);
        }

        PlateItemVisuals.Clear();
        BuildFoodDropZone.CollectActivePlateVisuals(PlateItemVisuals);

        for (int i = 0; i < PlateItemVisuals.Count; i++)
            AddDraggedVisual(PlateItemVisuals[i]);

        if (DraggedVisuals.Count == 0)
            return;

        dragging = true;

        // Equivalente por mouse de entrar en modo selección: mantiene vivo el paso del tutorial.
        TutorialManager.NotifyDeliverySelectionBegun();
    }

    private static void AddDraggedVisual(Transform target)
    {
        if (target == null)
            return;

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();

        DraggedVisuals.Add(new DraggedVisual
        {
            target = target,
            startPosition = target.position,
            renderer = renderer,
            startSortingOrder = renderer != null ? renderer.sortingOrder : 0
        });

        if (renderer != null)
            renderer.sortingOrder += DragSortingBoost;
    }

    private static void RestoreSortingOrders()
    {
        for (int i = 0; i < DraggedVisuals.Count; i++)
        {
            SpriteRenderer renderer = DraggedVisuals[i].renderer;
            if (renderer != null)
                renderer.sortingOrder = DraggedVisuals[i].startSortingOrder;
        }
    }

    private static void RestorePositions()
    {
        for (int i = 0; i < DraggedVisuals.Count; i++)
        {
            Transform target = DraggedVisuals[i].target;
            if (target == null)
                continue;

            target.position = DraggedVisuals[i].startPosition;
        }
    }

    /// <summary>Resalta al cliente bajo el mouse con el mismo recuadro del flujo por teclado.</summary>
    private void SetHoveredView(CustomerView view)
    {
        if (hoveredView == view)
            return;

        hoveredView = view;

        CustomerSystem customers = GameManager.Instance != null ? GameManager.Instance.Customers : null;
        if (customers != null)
            customers.SetDeliveryDragHover(view);
    }

    private static CustomerView FindCustomerViewAt(Vector3 worldPoint)
    {
        int count = Physics2D.OverlapPointNonAlloc(worldPoint, OverlapResults);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = OverlapResults[i];
            if (hit == null)
                continue;

            CustomerView view = hit.GetComponentInParent<CustomerView>();
            if (view != null)
                return view;
        }

        return null;
    }

    private Vector3 GetMouseWorldPos()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position;

        Vector3 pos = Input.mousePosition;
        pos.z = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = transform.position.z;
        return world;
    }
}
