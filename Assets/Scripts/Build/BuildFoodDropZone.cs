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

    // Layout del plato. Todos los offsets son en UNIDADES DE MUNDO desde el centro de la zona
    // (el transform del plato está escalado a ~0.22, así que los offsets locales serían ilegibles).
    // La carne es la protagonista: queda donde la soltó el jugador (libre dentro del plato) y los
    // acompañamientos se reparten en slots fijos, más chicos y dibujados por debajo, para
    // acompañarla y no taparla.
    [Header("Plate Layout (world units from zone center)")]
    [Tooltip("Slots para guarniciones, en orden de llegada.")]
    [SerializeField] private Vector2[] sideSlotOffsets =
    {
        new Vector2(0.35f, 0.3f),
        new Vector2(0.9f, -0.3f),
    };

    [Tooltip("Tamaño de cada guarnición en unidades de mundo (media geométrica de ancho y alto del sprite).")]
    [SerializeField] private float sideVisualSize = 0.9f;

    [Tooltip("Slots para toppings/salsas, en orden de llegada.")]
    [SerializeField] private Vector2[] toppingSlotOffsets =
    {
        new Vector2(-0.2f, -0.55f),
        new Vector2(0.35f, -0.6f),
    };

    [Tooltip("Tamaño de cada topping en unidades de mundo (media geométrica de ancho y alto del sprite).")]
    [SerializeField] private float toppingVisualSize = 0.55f;

    [Tooltip("Si entran más visuales que slots, los sobrantes siguen desde el último slot con este paso.")]
    [SerializeField] private Vector2 overflowStep = new Vector2(0.3f, -0.15f);

    [Tooltip("Sorting base de sides/toppings. Va DEBAJO de la carne (MeatTransferBuffer.plateMeatSortingBase = 400) " +
             "para que nunca la tapen; cada visual suma su índice.")]
    [SerializeField] private int sideTopSortingOrder = 390;

    private class PlateVisualEntry
    {
        public GameObject go;
        public PlateVisualKind kind;
    }

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
    /// Apoya un visual de guarnición/topping en el siguiente slot libre de su tipo, escalado a un
    /// tamaño uniforme y dibujado por debajo de la carne.
    /// </summary>
    public bool SpawnPlateVisual(Sprite sprite, PlateVisualKind kind)
    {
        if (sprite == null)
            return false;

        int index = CountVisualsOfKind(kind);
        Vector2[] slots = kind == PlateVisualKind.Side ? sideSlotOffsets : toppingSlotOffsets;
        float size = kind == PlateVisualKind.Side ? sideVisualSize : toppingVisualSize;

        Vector3 spawnPos = transform.position + (Vector3)ResolveSlotOffset(slots, index);
        spawnPos.z = transform.position.z;

        GameObject go = new GameObject("PlateVisual_" + kind + "_" + index);
        go.transform.position = spawnPos;
        go.transform.localScale = Vector3.one * FitScale(sprite, size);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sideTopSortingOrder + plateSideTopVisuals.Count;

        plateSideTopVisuals.Add(new PlateVisualEntry { go = go, kind = kind });
        return true;
    }

    /// <summary>Quita el último visual de acompañamiento/topping del plato. Usado por el undo.</summary>
    public void RemoveLastPlateVisual()
    {
        int last = plateSideTopVisuals.Count - 1;
        if (last < 0)
            return;

        GameObject go = plateSideTopVisuals[last].go;
        plateSideTopVisuals.RemoveAt(last);

        if (go != null)
            Destroy(go);
    }

    private int CountVisualsOfKind(PlateVisualKind kind)
    {
        int count = 0;
        for (int i = 0; i < plateSideTopVisuals.Count; i++)
        {
            if (plateSideTopVisuals[i].kind == kind)
                count++;
        }
        return count;
    }

    private Vector2 ResolveSlotOffset(Vector2[] slots, int index)
    {
        if (slots == null || slots.Length == 0)
            return overflowStep * index;

        if (index < slots.Length)
            return slots[index];

        // Sin slot libre: sigue en diagonal desde el último para no pisar el anterior.
        return slots[slots.Length - 1] + overflowStep * (index - slots.Length + 1);
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
        Vector3 center = transform.position;

        Gizmos.color = new Color(0.95f, 0.8f, 0.2f, 0.9f);
        if (sideSlotOffsets != null)
            for (int i = 0; i < sideSlotOffsets.Length; i++)
                Gizmos.DrawWireCube(center + (Vector3)sideSlotOffsets[i], new Vector3(sideVisualSize, sideVisualSize, 0f));

        Gizmos.color = new Color(0.3f, 0.8f, 0.3f, 0.9f);
        if (toppingSlotOffsets != null)
            for (int i = 0; i < toppingSlotOffsets.Length; i++)
                Gizmos.DrawWireCube(center + (Vector3)toppingSlotOffsets[i], new Vector3(toppingVisualSize, toppingVisualSize, 0f));
    }
#endif
}
