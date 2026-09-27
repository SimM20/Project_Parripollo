using System.Collections.Generic;
using UnityEngine;

public class BuildFoodDropZone : MonoBehaviour
{
    /// <summary>Qué tipo de visual se apoya sobre el plato. Decide en qué slot del layout cae.</summary>
    public enum PlateVisualKind { Side, Topping }

    [SerializeField] private BuildStationSystem buildStationSystem;
    [SerializeField] private MeatTransferBuffer meatTransferBuffer;
    [SerializeField] private Collider2D zoneCollider;

    [Tooltip("Sprite del plato en sí. Al entregar viaja junto con la carne y los acompañamientos, así se " +
             "arrastra el plato entero y no la comida flotando. Si falta, se toma el SpriteRenderer de este objeto.")]
    [SerializeField] private SpriteRenderer plateRenderer;

    public BuildStationSystem BuildStation => buildStationSystem;

    /// <summary>Zonas de plato vivas. Solo lectura: el alta/baja la hace OnEnable/OnDestroy.</summary>
    public static IReadOnlyList<BuildFoodDropZone> Zones => ActiveZones;

    /// <summary>Transform del sprite del plato (el que se arrastra al entregar), o null si no hay sprite.</summary>
    public Transform PlateBody => plateRenderer != null ? plateRenderer.transform : null;

    /// <summary>Área del plato donde se sueltan carne, pan y guarniciones.</summary>
    public Collider2D ZoneCollider => zoneCollider;

    /// <summary>True si este plato tiene una carne montada: hay algo para entregar.</summary>
    public bool HasLoadedPlate => isActiveAndEnabled && buildStationSystem != null && buildStationSystem.HasAnyCut;

    /// <summary>True si el punto (ya proyectado al plano z de la zona) cae dentro del plato.</summary>
    public bool ContainsPoint(Vector3 worldPoint)
    {
        return zoneCollider != null && zoneCollider.enabled
            && zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y));
    }

    // Layout del plato. Todos los valores son en UNIDADES DE MUNDO desde el centro de la zona
    // (el transform del plato está escalado a ~0.22, así que los offsets locales serían ilegibles).
    // La carne es la protagonista: queda donde la soltó el jugador (libre dentro del plato) y los
    // acompañamientos, más chicos y dibujados por debajo, se acomodan en dos filas horizontales
    // (guarniciones y toppings) CENTRADAS en el plato. Cada vez que se agrega o se deshace uno,
    // la fila se vuelve a centrar; si no entra en el ancho del plato a esa altura, primero se
    // juntan y después se achican, así nunca se salen del plato.
    [Header("Plate Layout (world units from zone center)")]
    [Tooltip("Altura de la fila de guarniciones respecto del centro del plato.")]
    [SerializeField] private float sideRowY = 0.3f;

    [Tooltip("Tamaño de cada guarnición en unidades de mundo (media geométrica de ancho y alto del sprite).")]
    [SerializeField] private float sideVisualSize = 0.9f;

    [Tooltip("Altura de la fila de toppings/salsas respecto del centro del plato.")]
    [SerializeField] private float toppingRowY = -0.55f;

    [Tooltip("Tamaño de cada topping en unidades de mundo (media geométrica de ancho y alto del sprite).")]
    [SerializeField] private float toppingVisualSize = 0.55f;

    [Tooltip("Distancia entre centros de dos vecinos de la misma fila, como fracción del tamaño del visual " +
             "(1 = se tocan, menos de 1 = se enciman un poco, como comida servida).")]
    [Range(0.3f, 1.5f)]
    [SerializeField] private float itemSpacing = 0.85f;

    [Tooltip("Cuánto se pueden juntar como máximo cuando la fila no entra, antes de empezar a achicarlos.")]
    [Range(0.2f, 1f)]
    [SerializeField] private float minItemSpacing = 0.45f;

    [Tooltip("Margen libre entre la fila y el borde del plato. El plato se toma como un óvalo inscripto en su collider.")]
    [SerializeField] private float plateEdgeMargin = 0.1f;

    [Tooltip("Sorting base de sides/toppings. Va DEBAJO de la carne (MeatTransferBuffer.plateMeatSortingBase = 400) " +
             "para que nunca la tapen; cada visual suma su índice.")]
    [SerializeField] private int sideTopSortingOrder = 390;

    private class PlateVisualEntry
    {
        public GameObject go;
        public PlateVisualKind kind;
        /// <summary>Escala que lleva el sprite al tamaño de su tipo, antes de achicar la fila.</summary>
        public float fitScale;
    }

    private static readonly List<PlateVisualEntry> RowBuffer = new List<PlateVisualEntry>();

    private readonly List<PlateVisualEntry> plateSideTopVisuals = new List<PlateVisualEntry>();
    private static readonly List<BuildFoodDropZone> ActiveZones = new List<BuildFoodDropZone>();

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        if (!ActiveZones.Contains(this))
            ActiveZones.Add(this);
    }

    void OnDestroy()
    {
        ActiveZones.Remove(this);
    }

    public static bool TryAcceptAt(Vector3 worldPoint, BuildDraggableFoodItem item)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null || zone.buildStationSystem == null)
                continue;

            if (!zone.zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                continue;

            if (!item.HasExactlyOneData())
            {
                Debug.LogWarning("[BuildFoodDropZone] Item " + item.gameObject.name +
                    " does not have exactly one SO assigned.");
                return false;
            }

            if (item.breadData != null)
            {
                // El pan envuelve a la carne, no se sirve solo: sin corte en el plato no hay
                // sandwich posible y el armado quedaria con un pan que no se ve ni se entrega.
                if (!zone.buildStationSystem.HasAnyCut)
                {
                    Debug.Log("[Build] El plato no tiene carne: se rechaza el pan " + item.breadData.breadName + ".");
                    return false;
                }

                BreadSO previousBread = zone.buildStationSystem.AssembledBread;
                GameObject plateMeatVisual = null;
                Sprite previousSprite = null;
                Vector3 previousScale = Vector3.one;
                Vector3 previousEuler = Vector3.zero;
                bool visualCaptured = zone.meatTransferBuffer != null
                    && zone.meatTransferBuffer.TryCaptureLastPlateMeatVisual(
                        out plateMeatVisual, out previousSprite, out previousScale, out previousEuler);

                zone.buildStationSystem.SetBread(item.breadData);
                ProductVariantSO variant = zone.buildStationSystem.TryResolveVariant();
                if (variant != null)
                {
                    MeatStates meatState = MeatStates.Crudo;
                    if (zone.buildStationSystem.AssembledCutStates.Count > 0)
                        meatState = zone.buildStationSystem.AssembledCutStates[0];

                    Sprite variantSprite = variant.GetSpriteForState(meatState);
                    if (variantSprite != null)
                        zone.meatTransferBuffer?.UpdatePlateMeatSprite(variantSprite);
                    else
                        Debug.LogWarning("[Build] Sin sprite de variante para: " + item.breadData.breadName);
                }
                else
                {
                    Debug.LogWarning("[Build] Sin sprite de variante para: " + item.breadData.breadName);
                }
                BuildUndoHistory.Instance?.Push(new SetBreadUndoAction(
                    zone.buildStationSystem, zone.meatTransferBuffer, previousBread,
                    visualCaptured ? plateMeatVisual : null, previousSprite, previousScale, previousEuler));
                Debug.Log("[Build] Pan arrastrado: " + item.breadData.breadName);
            }
            else if (item.sideData != null)
            {
                zone.buildStationSystem.AddSide(item.sideData);
                bool sideVisualSpawned = zone.SpawnPlateVisual(
                    item.GetComponent<SpriteRenderer>()?.sprite, PlateVisualKind.Side);
                BuildUndoHistory.Instance?.Push(new AddSideUndoAction(
                    zone.buildStationSystem, zone, item.sideData, sideVisualSpawned));
                Debug.Log("[Build] Acompañamiento arrastrado: " + item.sideData.sideName);
            }
            else if (item.toppingData != null)
            {
                zone.buildStationSystem.AddTopping(item.toppingData);
                bool toppingVisualSpawned = zone.SpawnPlateVisual(
                    item.GetComponent<SpriteRenderer>()?.sprite, PlateVisualKind.Topping);
                BuildUndoHistory.Instance?.Push(new AddToppingUndoAction(
                    zone.buildStationSystem, zone, item.toppingData, toppingVisualSpawned, null, 0, 0f));
                Debug.Log("[Build] Topping arrastrado: " + item.toppingData.toppingName);
            }

            return true;
        }

        return false;
    }

    public static bool TryAcceptMeatAt(Vector3 worldPoint, MeatCutSO cut)
    {
        return TryAcceptMeatAt(worldPoint, cut, MeatStates.Crudo);
    }

    public static bool TryAcceptMeatAt(Vector3 worldPoint, MeatCutSO cut, MeatStates state)
    {
        return TryAcceptMeatAt(worldPoint, cut, state, state, state);
    }

    /// <summary>
    /// Acepta el corte si el punto cae sobre un plato libre. El visual queda donde se soltó:
    /// la carne es libre dentro del plato, no hay anclaje.
    /// </summary>
    public static bool TryAcceptMeatAt(Vector3 worldPoint, MeatCutSO cut, MeatStates state, MeatStates sideAState, MeatStates sideBState)
    {
        if (cut == null)
            return false;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null || zone.buildStationSystem == null)
                continue;

            if (!zone.zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                continue;

            // El plato admite un solo corte a la vez: el que sobra vuelve a su origen.
            if (zone.buildStationSystem.HasAnyCut)
            {
                Debug.Log("[Build] El plato ya tiene una carne: se rechaza " + cut.cutName + ".");
                return false;
            }

            zone.buildStationSystem.AddCut(cut, state, sideAState, sideBState);
            Debug.Log("[Build] Carne arrastrada: " + cut.cutName + " con estado " + state
                      + " (A: " + sideAState + " | B: " + sideBState + ")");
            return true;
        }

        return false;
    }

    /// <summary>
    /// True si el punto cae sobre una zona de plato que ya tiene una carne montada.
    /// Lo usan los arrastres para devolver el corte a su origen en vez de dejarlo caer
    /// en la grilla que el plato tapa.
    /// </summary>
    public static bool IsPlateOccupiedAt(Vector3 worldPoint)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.isActiveAndEnabled || zone.zoneCollider == null || zone.buildStationSystem == null)
                continue;

            if (!zone.zoneCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y)))
                continue;

            if (zone.buildStationSystem.HasAnyCut)
                return true;
        }

        return false;
    }

    /// <summary>
    /// True si el punto cae sobre cualquier zona de plato viva, tenga carne o no. Lo usa el
    /// arrastre de solo-carne para saber si el corte se reposicionó dentro del plato.
    /// </summary>
    public static bool IsOverPlateAt(Vector3 worldPoint)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone != null && zone.isActiveAndEnabled && zone.ContainsPoint(worldPoint))
                return true;
        }

        return false;
    }

    public static void ClearActivePlateVisuals()
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            if (ActiveZones[i] != null)
                ActiveZones[i].ClearPlateItemVisuals();
        }
    }

    public static void SetActivePlateVisualsVisible(bool visible)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            if (ActiveZones[i] == null) continue;
            List<PlateVisualEntry> visuals = ActiveZones[i].plateSideTopVisuals;
            for (int j = 0; j < visuals.Count; j++)
            {
                if (visuals[j].go != null)
                    visuals[j].go.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// Agrega a la lista los visuales de acompañamientos/toppings que están sobre el plato.
    /// Los usa PlateDeliveryDraggable para arrastrar el plato completo como un bloque.
    /// </summary>
    public static void CollectActivePlateVisuals(List<Transform> into)
    {
        if (into == null)
            return;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null)
                continue;

            List<PlateVisualEntry> visuals = zone.plateSideTopVisuals;
            for (int j = 0; j < visuals.Count; j++)
            {
                if (visuals[j].go != null && visuals[j].go.activeInHierarchy)
                    into.Add(visuals[j].go.transform);
            }
        }
    }

    /// <summary>
    /// Agrega a la lista el sprite del plato de cada zona que tiene una carne montada. Los usa
    /// PlateDeliveryDraggable para que el plato viaje con la comida durante la entrega y vuelva
    /// vacío al mostrador si se concreta. No entra en ClearActivePlateVisuals: el plato no se destruye.
    /// </summary>
    public static void CollectActivePlateBodies(List<Transform> into)
    {
        if (into == null)
            return;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            BuildFoodDropZone zone = ActiveZones[i];
            if (zone == null || !zone.HasLoadedPlate)
                continue;

            Transform body = zone.PlateBody;
            if (body != null && body.gameObject.activeInHierarchy)
                into.Add(body);
        }
    }

    /// <summary>
    /// Apoya un visual de guarnición/topping en la fila de su tipo, escalado a un tamaño uniforme y
    /// dibujado por debajo de la carne. La fila entera se vuelve a centrar con el recién llegado.
    /// </summary>
    public bool SpawnPlateVisual(Sprite sprite, PlateVisualKind kind)
    {
        if (sprite == null)
            return false;

        float size = kind == PlateVisualKind.Side ? sideVisualSize : toppingVisualSize;

        GameObject go = new GameObject("PlateVisual_" + kind + "_" + plateSideTopVisuals.Count);
        go.transform.position = transform.position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sideTopSortingOrder + plateSideTopVisuals.Count;

        plateSideTopVisuals.Add(new PlateVisualEntry { go = go, kind = kind, fitScale = FitScale(sprite, size) });
        LayoutRow(kind);
        return true;
    }

    /// <summary>Quita el último visual de acompañamiento/topping del plato. Usado por el undo.</summary>
    public void RemoveLastPlateVisual()
    {
        int last = plateSideTopVisuals.Count - 1;
        if (last < 0)
            return;

        PlateVisualEntry entry = plateSideTopVisuals[last];
        plateSideTopVisuals.RemoveAt(last);

        if (entry.go != null)
            Destroy(entry.go);

        // Los que quedan se vuelven a centrar (y a agrandar, si la fila estaba apretada).
        LayoutRow(entry.kind);
    }

    /// <summary>
    /// Acomoda todos los visuales de un tipo en su fila, centrados en el plato y en orden de
    /// llegada de izquierda a derecha. Si la fila no entra en el ancho del plato a esa altura,
    /// primero se juntan (hasta <see cref="minItemSpacing"/>) y después se achican.
    /// </summary>
    private void LayoutRow(PlateVisualKind kind)
    {
        RowBuffer.Clear();
        for (int i = 0; i < plateSideTopVisuals.Count; i++)
        {
            PlateVisualEntry entry = plateSideTopVisuals[i];
            if (entry.kind == kind && entry.go != null)
                RowBuffer.Add(entry);
        }

        int count = RowBuffer.Count;
        if (count == 0)
            return;

        float size = kind == PlateVisualKind.Side ? sideVisualSize : toppingVisualSize;
        float rowY = kind == PlateVisualKind.Side ? sideRowY : toppingRowY;
        float available = RowWidthAt(rowY, size);

        float spacing = size * itemSpacing;
        float shrink = 1f;

        if (count > 1)
        {
            // Ancho que ocupa la fila: los pasos entre centros + medio visual en cada punta.
            float needed = (count - 1) * spacing + size;
            if (needed > available)
            {
                spacing = Mathf.Max(size * minItemSpacing, (available - size) / (count - 1));
                needed = (count - 1) * spacing + size;

                // Ni juntándolos al máximo entran: se achica todo (visuales y pasos) en proporción.
                if (needed > available)
                {
                    shrink = Mathf.Max(0.2f, available / needed);
                    spacing *= shrink;
                }
            }
        }
        else if (size > available)
        {
            shrink = Mathf.Max(0.2f, available / size);
        }

        Vector3 origin = transform.position;
        float firstX = -(count - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            Transform t = RowBuffer[i].go.transform;
            t.position = new Vector3(origin.x + firstX + i * spacing, origin.y + rowY, origin.z);
            t.localScale = Vector3.one * (RowBuffer[i].fitScale * shrink);
        }

        RowBuffer.Clear();
    }

    /// <summary>
    /// Ancho utilizable de una fila a la altura <paramref name="rowY"/> (desde el centro del
    /// transform). El plato se toma como el óvalo inscripto en su collider; se mide en el borde
    /// del visual más alejado del centro, para que ni las puntas de arriba o abajo se salgan.
    /// </summary>
    private float RowWidthAt(float rowY, float itemSize)
    {
        if (zoneCollider == null)
            return float.MaxValue;

        Bounds b = zoneCollider.bounds;
        float a = b.extents.x;
        float h = b.extents.y;
        if (a <= 0f || h <= 0f)
            return float.MaxValue;

        float dy = Mathf.Abs(transform.position.y + rowY - b.center.y) + itemSize * 0.5f;
        float k = Mathf.Clamp01(dy / h);
        float halfWidth = a * Mathf.Sqrt(1f - k * k);

        // Nunca menos que un visual: si el layout pide una fila casi en el borde, que al menos entre uno.
        return Mathf.Max(itemSize * 0.5f, 2f * (halfWidth - plateEdgeMargin));
    }

    /// <summary>
    /// Escala uniforme para que el sprite mida 'targetSize' unidades de mundo. Se usa la media
    /// geométrica de ancho y alto (no el lado mayor): así un sprite apaisado como las papas no queda
    /// enano al lado de uno cuadrado, y todos los acompañamientos ocupan un área parecida.
    /// </summary>
    private static float FitScale(Sprite sprite, float targetSize)
    {
        Vector3 bounds = sprite.bounds.size;
        float reference = Mathf.Sqrt(Mathf.Max(0f, bounds.x * bounds.y));
        if (reference <= 0f)
            return 1f;
        return targetSize / reference;
    }

    private void ClearPlateItemVisuals()
    {
        for (int i = 0; i < plateSideTopVisuals.Count; i++)
        {
            if (plateSideTopVisuals[i].go != null)
                Destroy(plateSideTopVisuals[i].go);
        }

        plateSideTopVisuals.Clear();
    }

    private void EnsureReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (buildStationSystem == null)
            buildStationSystem = FindFirstObjectByType<BuildStationSystem>();

        if (meatTransferBuffer == null)
            meatTransferBuffer = FindFirstObjectByType<MeatTransferBuffer>();

        if (plateRenderer == null)
            plateRenderer = GetComponent<SpriteRenderer>();
    }

    void OnValidate()
    {
        EnsureReferences();
    }

#if UNITY_EDITOR
    // Dibuja el layout en la Scene view para ajustar los slots sin entrar en Play.
    void OnDrawGizmosSelected()
    {
        // Cada fila: una caja del ancho utilizable a esa altura y del alto de un visual.
        Vector3 center = transform.position;

        Gizmos.color = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(center + new Vector3(0f, sideRowY, 0f),
            new Vector3(Mathf.Min(RowWidthAt(sideRowY, sideVisualSize), 10f), sideVisualSize, 0f));

        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(center + new Vector3(0f, toppingRowY, 0f),
            new Vector3(Mathf.Min(RowWidthAt(toppingRowY, toppingVisualSize), 10f), toppingVisualSize, 0f));
    }
#endif
}
