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
///
/// La carne no queda bloqueada en el plato: si se suelta sobre la bandeja vuelve a la
/// bandeja, y si se suelta sobre un hueco libre de la parrilla vuelve a cocinarse ahí
/// (ver MeatTransferBuffer.TryReturnPlateMeatToTray / TryReturnPlateMeatToGrill).
///
/// El agarre NO usa OnMouseDown/OnMouseDrag/OnMouseUp: el visual del plato queda
/// apoyado sobre el collider de la zona 'ToBuild', que está en el mismo plano z y no
/// tiene handler de mouse. Con la cámara en perspectiva ese collider se queda con el
/// click y la carne deja de ser agarrable. El pick se resuelve acá, proyectando el
/// mouse sobre el plano z del propio visual (mismo patrón que Item.GetMouseWorldPosition).
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

    /// <summary>Instancia que conduce el arrastre en curso. Hay un solo mouse: nunca hay dos a la vez.</summary>
    private static PlateDeliveryDraggable activeDragger;

    /// <summary>Frame en el que ya se resolvió qué visual agarra el click, para no repetir el pick por instancia.</summary>
    private static int lastPickFrame = -1;

    private SpriteRenderer selfRenderer;
    private BoxCollider2D selfCollider;
    private Vector3 grabWorldPoint;
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

    void OnDisable()
    {
        if (activeDragger == this)
            CancelDrag();
    }

    void OnDestroy()
    {
        if (activeDragger == this)
            CancelDrag();

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

    void Update()
    {
        if (activeDragger == this)
        {
            UpdateDrag();
            return;
        }

        if (activeDragger != null || !Input.GetMouseButtonDown(0))
            return;

        // El pick es global: la primera instancia que corre este frame lo resuelve para todas.
        if (lastPickFrame == Time.frameCount)
            return;

        lastPickFrame = Time.frameCount;

        PlateDeliveryDraggable picked = PickUnderPointer();
        if (picked != null)
            picked.BeginDrag();
    }

    private void UpdateDrag()
    {
        if (!Input.GetMouseButton(0))
        {
            EndDrag();
            return;
        }

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

    /// <summary>
    /// Visual del plato bajo el puntero, o null. Cada candidato proyecta el mouse sobre su
    /// propio plano z: con cámara en perspectiva un punto calculado en otro z cae desplazado.
    /// Gana el de sortingOrder más alto, que es el que se ve arriba.
    /// </summary>
    private static PlateDeliveryDraggable PickUnderPointer()
    {
        PlateDeliveryDraggable best = null;
        int bestSortingOrder = 0;

        for (int i = 0; i < Instances.Count; i++)
        {
            PlateDeliveryDraggable candidate = Instances[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            if (candidate.selfCollider == null || !candidate.selfCollider.enabled)
                continue;

            Vector3 pointerWorld = candidate.GetMouseWorldPos();
            if (!candidate.selfCollider.OverlapPoint(pointerWorld))
                continue;

            // Los paneles deslizables tapan un borde de la pantalla: ahí el click es de ellos.
            if (IsPointerOverSlidingPanel(pointerWorld))
                return null;

            int sortingOrder = candidate.selfRenderer != null ? candidate.selfRenderer.sortingOrder : 0;
            if (best == null || sortingOrder > bestSortingOrder)
            {
                best = candidate;
                bestSortingOrder = sortingOrder;
            }
        }

        return best;
    }

    private static bool IsPointerOverSlidingPanel(Vector3 worldPoint)
    {
        if (StockPanelController.Instance != null && StockPanelController.Instance.IsPointOverPanel(worldPoint))
            return true;

        if (ToppingsPanelController.Instance != null && ToppingsPanelController.Instance.IsPointOverPanel(worldPoint))
            return true;

        return false;
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

        activeDragger = this;

        // Los clientes apagan su collider mientras hay un panel desplegado; durante el
        // arrastre hay que devolvérselo o FindCustomerViewAt no encuentra a nadie.
        CustomerView.SetDeliveryDragActive(true);

        // Equivalente por mouse de entrar en modo selección: mantiene vivo el paso del tutorial.
        TutorialManager.NotifyDeliverySelectionBegun();
    }

    private void EndDrag()
    {
        activeDragger = null;
        RestoreSortingOrders();

        Vector3 dropPoint = GetMouseWorldPos();
        CustomerView dropView = FindCustomerViewAt(dropPoint);

        SetHoveredView(null);

        bool delivered = TutorialManager.CheckDeliveryConfirmAllowed()
            && dropView != null
            && dropView.Customer != null
            && GameManager.Instance != null
            && GameManager.Instance.TryDeliverToCustomer(dropView.Customer);

        // Sin entrega el bloque vuelve al plato, y solo la carne agarrada puede cambiar de
        // destino: a la bandeja (MeatHolder / MeatList) o de vuelta a la parrilla.
        if (!delivered)
        {
            RestorePositions();

            MeatTransferBuffer transferBuffer = Object.FindAnyObjectByType<MeatTransferBuffer>();
            if (transferBuffer != null)
            {
                if (transferBuffer.IsOverMeatTray(dropPoint))
                    transferBuffer.TryReturnPlateMeatToTray(gameObject);
                else
                    transferBuffer.TryReturnPlateMeatToGrill(gameObject, dropPoint);
            }
        }

        DraggedVisuals.Clear();
        CustomerView.SetDeliveryDragActive(false);
    }

    /// <summary>Aborta el arrastre sin intentar el drop. Para cuando el visual que conduce se apaga o se destruye.</summary>
    private void CancelDrag()
    {
        activeDragger = null;
        RestoreSortingOrders();
        RestorePositions();
        SetHoveredView(null);
        DraggedVisuals.Clear();
        CustomerView.SetDeliveryDragActive(false);
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
            if (view != null && view.Customer != null && !view.Customer.IsInFeedback)
                return view;
        }

        return null;
    }

    /// <summary>
    /// Punto del mouse sobre el plano z de este visual. La distancia a la cámara es obligatoria:
    /// con cámara en perspectiva, ScreenToWorldPoint con z=0 devuelve la posición de la cámara.
    /// Mismo patrón que Item.GetMouseWorldPosition.
    /// </summary>
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
