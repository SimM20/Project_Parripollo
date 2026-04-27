using System;
using System.Collections.Generic;
using UnityEngine;

public class CoalStockVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private SpriteRenderer toGrillDropArea;
    [SerializeField] private GameObject stockVisualPrefab;

    [Header("Stack Config")]
    [SerializeField] private List<CoalVisualConfig> coalVisuals = new List<CoalVisualConfig>();
    [Min(0.01f)]
    [SerializeField] private float defaultSpacing = 0.2f;

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

    private readonly Dictionary<CoalSO, CoalVisualConfig> configByCoal = new Dictionary<CoalSO, CoalVisualConfig>();
    private readonly Dictionary<CoalSO, List<GameObject>> spawnedByCoal = new Dictionary<CoalSO, List<GameObject>>();
    private readonly HashSet<CoalSO> warnedMissingConfig = new HashSet<CoalSO>();
    private Type draggableType;

    void Awake()
    {
        BuildConfigLookup();
        // Buscamos el cerebro del carbón
        draggableType = ResolveType("DraggableCoal");
    }

    void OnEnable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged += RefreshVisuals;
    }

    void Start() => RefreshVisuals();

    void OnDisable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged -= RefreshVisuals;
    }

    private void BuildConfigLookup()
    {
        configByCoal.Clear();

        for (int i = 0; i < coalVisuals.Count; i++)
        {
            CoalVisualConfig config = coalVisuals[i];
            if (config == null || config.coalType == null)
                continue;

            if (configByCoal.ContainsKey(config.coalType))
                continue;

            configByCoal.Add(config.coalType, config);
        }
    }

    public void RefreshVisuals()
    {
        if (coolerSystem == null || stockVisualPrefab == null)
            return;

        warnedMissingConfig.Clear();
        HashSet<CoalSO> visitedCoals = new HashSet<CoalSO>();

        foreach (KeyValuePair<ItemDataSO, int> entry in coolerSystem.EnumerateStock())
        {
            if (entry.Key is CoalSO coal)
            {
                int count = Mathf.Max(0, entry.Value);
                visitedCoals.Add(coal);
                SyncCoalStack(coal, count);
            }
        }

        for (int i = 0; i < coalVisuals.Count; i++)
        {
            CoalVisualConfig config = coalVisuals[i];
            if (config == null || config.coalType == null)
                continue;

            if (visitedCoals.Contains(config.coalType))
                continue;

            SyncCoalStack(config.coalType, 0);
        }
    }

    private void SyncCoalStack(CoalSO coal, int targetCount)
    {
        if (!configByCoal.TryGetValue(coal, out CoalVisualConfig config))
        {
            if (!warnedMissingConfig.Contains(coal))
            {
                warnedMissingConfig.Add(coal);
                Debug.LogWarning("No hay config visual para el carbón.");
            }

            ClearCoalObjects(coal);
            return;
        }

        if (!spawnedByCoal.TryGetValue(coal, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            spawnedByCoal.Add(coal, instances);
        }

        while (instances.Count < targetCount)
        {
            Transform parent = config.anchor != null ? config.anchor : transform;
            GameObject go = Instantiate(stockVisualPrefab, parent);
            instances.Add(go);
        }

        while (instances.Count > targetCount)
        {
            int lastIndex = instances.Count - 1;
            GameObject last = instances[lastIndex];
            instances.RemoveAt(lastIndex);

            if (last != null)
                Destroy(last);
        }

        Vector3 direction = config.stackDirection.sqrMagnitude > 0f ? config.stackDirection.normalized : Vector3.up;
        float spacing = config.spacing > 0f ? config.spacing : defaultSpacing;

        for (int i = 0; i < instances.Count; i++)
        {
            GameObject go = instances[i];
            if (go == null)
                continue;

            Transform parent = config.anchor != null ? config.anchor : transform;
            if (go.transform.parent != parent)
                go.transform.SetParent(parent, false);

            ApplyFixedWorldScale(go.transform);

            go.transform.localPosition = direction * spacing * i;
            go.transform.localRotation = Quaternion.identity;

            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = coal.coalSprite;
                renderer.sortingOrder = config.baseSortingOrder + i;
            }

            Component draggable = draggableType != null ? go.GetComponent(draggableType) : null;
            if (draggable == null && draggableType != null)
                draggable = go.AddComponent(draggableType);

            if (draggable != null)
            {
                // PASO DE DATOS CRÍTICO:
                go.SendMessage("SetCoalData", coal, SendMessageOptions.DontRequireReceiver);

                // CORRECCIÓN ACÁ: Cambiamos "SetInventorySystem" por "SetCoolerSystem"
                go.SendMessage("SetCoolerSystem", coolerSystem, SendMessageOptions.DontRequireReceiver);

                // Le pasamos la referencia del cuadro blanco de la izquierda ("ToGrill")
                go.SendMessage("SetToGrillDropArea", toGrillDropArea, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        Type direct = Type.GetType(typeName);
        if (direct != null) return direct;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type found = assemblies[i].GetType(typeName);
            if (found != null) return found;
        }
        return null;
    }

    private void ApplyFixedWorldScale(Transform target)
    {
        if (target == null) return;
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = fixedWorldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(fixedWorldScale.x, parentScale.x),
            SafeDivide(fixedWorldScale.y, parentScale.y),
            SafeDivide(fixedWorldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private void ClearCoalObjects(CoalSO coal)
    {
        if (!spawnedByCoal.TryGetValue(coal, out List<GameObject> instances))
            return;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null)
                Destroy(instances[i]);
        }
        instances.Clear();
    }
}

[Serializable]
public class CoalVisualConfig
{
    public CoalSO coalType;
    public Transform anchor;
    [Min(0.01f)] public float spacing = 0.2f;
    public Vector3 stackDirection = Vector3.up;
    public int baseSortingOrder = 0;
}