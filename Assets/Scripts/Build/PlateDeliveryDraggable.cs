using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arrastre de lo que hay sobre el plato. Se agrega en runtime a cada visual de carne que
/// queda en la zona del plato (ver MeatTransferBuffer.AdoptVisualIntoPlate), así que no
/// necesita setup de escena.
///
/// Dos gestos, según qué cae bajo el click:
///
///  • Agarrar EL PLATO (BuildFoodDropZone.PlateBody, cualquier parte que no sea la carne)
///    → se lleva el plato completo como un bloque: sprite del plato + carne + sides/toppings.
///    Es la única vía de entrega: termina en GameManager.TryDeliverToCustomer. Si no se
///    concreta (se soltó al vacío o el cliente rechazó) todo vuelve intacto al mostrador;
///    si se concreta, la comida se destruye y el plato vuelve vacío.
///
///  • Agarrar LA CARNE → se mueve solo la carne. Dentro del plato se reposiciona (es libre);
///    sobre la bandeja vuelve a la bandeja; sobre un hueco libre de la parrilla vuelve a
///    cocinarse con sus tiempos (MeatTransferBuffer.TryReturnPlateMeatToTray / ToGrill), con
///    el mismo preview de slots del arrastre desde la bandeja. Sobre un cliente o en
///    cualquier otro lado vuelve a donde estaba: la carne sola NUNCA entrega, para no ver
///    comida volando. Durante el tutorial este gesto se apaga
///    (TutorialManager.CheckPlateMeatDragAllowed) y agarrar la carne lleva el plato entero.
///
/// El agarre NO usa OnWorldPointerDown/Drag/Up (WorldPointerDispatcher): el visual del plato queda
/// apoyado sobre el collider de la zona 'ToBuild', que está en el mismo plano z y no
/// tiene handler de mouse. Con la cámara en perspectiva ese collider se queda con el
/// click y la carne deja de ser agarrable. El pick se resuelve acá, proyectando el
/// mouse sobre el plano z del propio visual (mismo patrón que Item.GetMouseWorldPosition).
/// </summary>
public class PlateDeliveryDraggable : MonoBehaviour
{
    private const int DragSortingBoost = 5000;

    /// <summary>
    /// Margen de agarre alrededor de la silueta del corte, en unidades locales del sprite
    /// (100 px = 1 unidad, y el visual del plato va escalado a 0.5). Da un par de píxeles de
    /// tolerancia para agarrar un corte finito sin volver a tragarse el plato: el margen viejo
    /// era del 20% del lienzo entero, no del corte.
    /// </summary>
    private const float GrabPadding = 0.04f;

    private enum DragMode { WholePlate, MeatOnly }

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
    private static readonly List<Transform> PlateBodies = new List<Transform>();
    private static readonly Collider2D[] OverlapResults = new Collider2D[16];

    /// <summary>Instancia que conduce el arrastre en curso. Hay un solo mouse: nunca hay dos a la vez.</summary>
    private static PlateDeliveryDraggable activeDragger;
    private static DragMode activeMode;

    /// <summary>Hay un arrastre del plato entero en curso (el que entrega al cliente).</summary>
    public static bool IsDraggingWholePlate => activeDragger != null && activeMode == DragMode.WholePlate;
    /// <summary>Hay un arrastre de solo la carne del plato en curso.</summary>
    public static bool IsDraggingPlateMeat => activeDragger != null && activeMode == DragMode.MeatOnly;

    /// <summary>Corte y rotación de la carne del plato que se está arrastrando (solo carne).</summary>
    public static bool TryGetDraggedCut(out MeatCutSO cut, out bool rotated)
    {
        cut = IsDraggingPlateMeat ? activeDragger.draggedCut : null;
        rotated = cut != null && activeDragger.draggedCutRotated;
        return cut != null;
    }

    /// <summary>Frame en el que ya se resolvió qué visual agarra el click, para no repetir el pick por instancia.</summary>
    private static int lastPickFrame = -1;

    private SpriteRenderer selfRenderer;
    private Collider2D selfCollider;
    private Vector3 grabWorldPoint;
    private CustomerView hoveredView;

    // Solo para MeatOnly: buffer y datos del corte para el preview de slots de la parrilla.
    private MeatTransferBuffer transferBuffer;
    private MeatCutSO draggedCut;
    private bool draggedCutRotated;

    void Awake()
    {
        selfRenderer = GetComponent<SpriteRenderer>();

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
    /// Reajusta el collider al sprite actual. Hay que volver a medir cada vez que cambia el
    /// sprite: el visual sale del prefab genérico 'StockPrefab' y el corte se le asigna después,
    /// y además el pan reemplaza sprite, escala y rotación del visual del plato.
    ///
    /// El collider calca la silueta del corte (ver SpriteColliderFitter). Antes era una caja
    /// del lienzo entero + 20%, igual para todos los cortes: como los lienzos son de 100x100 px
    /// y el corte ocupa solo una parte (el chorizo, 84x53), esa caja se comía los clicks del
    /// plato alrededor de la carne.
    /// </summary>
    public void RefreshCollider()
    {
        if (selfRenderer == null)
            selfRenderer = GetComponent<SpriteRenderer>();

        selfCollider = SpriteColliderFitter.Fit(gameObject, selfRenderer, GrabPadding);
    }

    void Update()
    {
        // Único pick que no pasa por OnWorldPointerXXX (ver nota de clase): eventMask no lo frena.
        if (GamePause.IsPaused)
            return;

        if (activeDragger == this)
        {
            UpdateDrag();
            return;
        }

        if (activeDragger != null || !InputManager.PrimaryPressed)
            return;

        // El pick es global: la primera instancia que corre este frame lo resuelve para todas.
        if (lastPickFrame == Time.frameCount)
            return;

        lastPickFrame = Time.frameCount;

        PlateDeliveryDraggable picked = PickUnderPointer(out DragMode mode);
        if (picked != null)
            picked.BeginDrag(mode);
    }

    private void UpdateDrag()
    {
        if (!InputManager.PrimaryHeld)
        {
            EndDrag();
            return;
        }

        Vector3 mouseWorld = GetMouseWorldPos();
        Vector3 delta = mouseWorld - grabWorldPoint;

        // Con gamepad y un bloque de la parrilla seleccionado, la carne se apoya justo donde va a caer.
        if (activeMode == DragMode.MeatOnly && DraggedVisuals.Count > 0 && InputManager.TryGetGridSnap(out Vector3 snap))
        {
            Vector3 start = DraggedVisuals[0].startPosition;
            delta = new Vector3(snap.x - start.x, snap.y - start.y, 0f);
        }

        for (int i = 0; i < DraggedVisuals.Count; i++)
        {
            Transform target = DraggedVisuals[i].target;
            if (target == null)
                continue;

            target.position = DraggedVisuals[i].startPosition + delta;
        }

        if (activeMode == DragMode.WholePlate)
        {
            SetHoveredView(FindCustomerViewAt(mouseWorld));
            return;
        }

        // MeatOnly: R rota el footprint como en la bandeja, y el preview de slots sigue al corte.
        // Los cortes de footprint cuadrado no se rotan: quedarian identicos.
        if (InputManager.WasPressed(GameAction.Rotate) && draggedCut != null && draggedCut.CanRotate)
            draggedCutRotated = !draggedCutRotated;

        if (transferBuffer != null)
            transferBuffer.UpdateMeatHolderHover(draggedCut, transform.position, draggedCutRotated);
    }

    /// <summary>
    /// Qué agarra el click. Primero la carne: cada candidato proyecta el mouse sobre su
    /// propio plano z (con cámara en perspectiva un punto calculado en otro z cae desplazado)
    /// y gana el de sortingOrder más alto, que es el que se ve arriba. Si ninguna carne está
    /// bajo el mouse, prueba el plato en sí.
    /// </summary>
    private static PlateDeliveryDraggable PickUnderPointer(out DragMode mode)
    {
        mode = DragMode.WholePlate;

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

        if (best != null)
        {
            // Agarrar la carne mueve solo la carne, salvo en el tutorial, donde lleva el plato.
            mode = TutorialManager.CheckPlateMeatDragAllowed() ? DragMode.MeatOnly : DragMode.WholePlate;
            return best;
        }

        return PickPlateBodyUnderPointer();
    }

    /// <summary>
    /// Agarre por el plato en sí (no por la carne): si el click cae sobre una zona de plato
    /// con carne montada, conduce el arrastre el primer visual de carne vivo. El plato vacío
    /// no se agarra: sin carne no hay instancias y este Update ni siquiera corre.
    /// </summary>
    private static PlateDeliveryDraggable PickPlateBodyUnderPointer()
    {
        PlateDeliveryDraggable driver = null;
        for (int i = 0; i < Instances.Count; i++)
        {
            PlateDeliveryDraggable candidate = Instances[i];
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                driver = candidate;
                break;
            }
        }

        if (driver == null)
            return null;

        IReadOnlyList<BuildFoodDropZone> zones = BuildFoodDropZone.Zones;
        for (int i = 0; i < zones.Count; i++)
        {
            BuildFoodDropZone zone = zones[i];
            if (zone == null || !zone.HasLoadedPlate)
                continue;

            Transform body = zone.PlateBody;
            if (body == null || !body.gameObject.activeInHierarchy)
                continue;

            // Mismo cuidado que con la carne: proyectar el mouse sobre el plano z del plato.
            Vector3 pointerWorld = GetMouseWorldPosAtZ(body.position.z);
            if (!zone.ContainsPoint(pointerWorld))
                continue;

            if (IsPointerOverSlidingPanel(pointerWorld))
                return null;

            return driver;
        }

        return null;
    }

    private static bool IsPointerOverSlidingPanel(Vector3 worldPoint)
    {
        if (StockPanelController.Instance != null && StockPanelController.Instance.IsPointOverPanel(worldPoint))
            return true;

        if (ToppingsPanelController.Instance != null && ToppingsPanelController.Instance.IsPointOverPanel(worldPoint))
            return true;

        return false;
    }

    private void BeginDrag(DragMode mode)
    {
        if (mode == DragMode.MeatOnly)
        {
            BeginMeatOnlyDrag();
            return;
        }

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

        // El plato viaja debajo de todo: su sortingOrder (0) + el boost queda por debajo del de
        // sides/toppings (390) y carne (400) con el mismo boost, así la composición no cambia.
        PlateBodies.Clear();
        BuildFoodDropZone.CollectActivePlateBodies(PlateBodies);

        for (int i = 0; i < PlateBodies.Count; i++)
            AddDraggedVisual(PlateBodies[i]);

        if (DraggedVisuals.Count == 0)
            return;

        activeDragger = this;
        activeMode = DragMode.WholePlate;
        GamePause.OnPaused += CancelDrag;

        // Los clientes apagan su collider mientras hay un panel desplegado; durante el
        // arrastre hay que devolvérselo o FindCustomerViewAt no encuentra a nadie.
        CustomerView.SetDeliveryDragActive(true);

        // Equivalente por mouse de entrar en modo selección: mantiene vivo el paso del tutorial.
        TutorialManager.NotifyDeliverySelectionBegun();
    }

    /// <summary>Arrastre de solo esta carne: reposicionar en el plato, o devolverla a la bandeja / parrilla.</summary>
    private void BeginMeatOnlyDrag()
    {
        transferBuffer = Object.FindAnyObjectByType<MeatTransferBuffer>();
        if (transferBuffer == null || !transferBuffer.TryGetPlateMeatInfo(gameObject, out draggedCut, out draggedCutRotated))
            return;

        if (draggedCut != null && !draggedCut.CanRotate)
            draggedCutRotated = false;

        DraggedVisuals.Clear();
        grabWorldPoint = GetMouseWorldPos();
        AddDraggedVisual(transform);

        activeDragger = this;
        activeMode = DragMode.MeatOnly;
        GamePause.OnPaused += CancelDrag;

        transferBuffer.UpdateMeatHolderHover(draggedCut, transform.position, draggedCutRotated);
    }

    private void EndDrag()
    {
        if (activeMode == DragMode.MeatOnly)
        {
            EndMeatOnlyDrag();
            return;
        }

        activeDragger = null;
        GamePause.OnPaused -= CancelDrag;
        RestoreSortingOrders();

        Vector3 dropPoint = GetMouseWorldPos();
        CustomerView dropView = FindCustomerViewAt(dropPoint);

        SetHoveredView(null);

        bool delivered = TutorialManager.CheckDeliveryConfirmAllowed()
            && dropView != null
            && dropView.Customer != null
            && GameManager.Instance != null
            && GameManager.Instance.TryDeliverToCustomer(dropView.Customer);

        // El bloque siempre vuelve al mostrador tal cual estaba: se arrastra el plato servido,
        // así que soltarlo en cualquier lado que no sea un cliente (o que un cliente lo rechace)
        // lo deja intacto, con la carne donde estaba. Con entrega aceptada la comida ya fue
        // destruida (Destroy diferido, los transforms siguen vivos este frame) y lo que vuelve
        // es el plato vacío.
        RestorePositions();

        DraggedVisuals.Clear();
        CustomerView.SetDeliveryDragActive(false);
    }

    private void EndMeatOnlyDrag()
    {
        activeDragger = null;
        GamePause.OnPaused -= CancelDrag;
        RestoreSortingOrders();

        if (transferBuffer != null)
            transferBuffer.ClearMeatHolderHover();

        Vector3 dropPoint = GetMouseWorldPos();

        // Dentro del plato: queda donde se soltó (la carne es libre). No hay nada que restaurar.
        if (BuildFoodDropZone.IsOverPlateAt(dropPoint))
        {
            DraggedVisuals.Clear();
            return;
        }

        // Fuera del plato: primero vuelve a su lugar y recién después se intenta el destino.
        // Sobre un cliente nunca: la carne sola no entrega, y los clientes pueden pisar slots
        // de la parrilla, así que sin este gate el corte terminaba cocinándose bajo el cliente.
        RestorePositions();

        bool overCustomer = FindCustomerViewAt(dropPoint) != null;
        if (!overCustomer && transferBuffer != null)
        {
            if (transferBuffer.IsOverMeatTray(dropPoint))
                transferBuffer.TryReturnPlateMeatToTray(gameObject);
            else
                transferBuffer.TryReturnPlateMeatToGrill(gameObject, dropPoint, draggedCutRotated);
        }

        DraggedVisuals.Clear();
    }

    /// <summary>Aborta el arrastre sin intentar el drop. Para cuando el visual que conduce se apaga o se destruye.</summary>
    private void CancelDrag()
    {
        activeDragger = null;
        GamePause.OnPaused -= CancelDrag;
        RestoreSortingOrders();
        RestorePositions();
        SetHoveredView(null);
        DraggedVisuals.Clear();

        if (transferBuffer != null)
            transferBuffer.ClearMeatHolderHover();

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

        return GetMouseWorldPosAtZ(transform.position.z);
    }

    /// <summary>Punto del mouse sobre un plano z cualquiera (el del plato al agarrarlo por el plato).</summary>
    private static Vector3 GetMouseWorldPosAtZ(float z)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return new Vector3(0f, 0f, z);

        Vector3 pos = InputManager.PointerPosition;
        pos.z = Mathf.Abs(z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(pos);
        world.z = z;
        return world;
    }
}
