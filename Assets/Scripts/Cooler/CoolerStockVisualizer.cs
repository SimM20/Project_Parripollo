using System;
using System.Collections.Generic;
using UnityEngine;

public class CoolerStockVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CoolerSystem coolerSystem;
    [SerializeField] private MonoBehaviour meatTransferBuffer;
    [SerializeField] private SpriteRenderer toGrillDropArea;
    [SerializeField] private GameObject stockVisualPrefab;

    [Header("Stack Config")]
    [SerializeField] private List<CoolerCutVisualConfig> cutVisuals = new List<CoolerCutVisualConfig>();
    [Min(0.01f)]
    [SerializeField] private float defaultSpacing = 0.2f;

    [Header("Visual Size")]
    [SerializeField] private Vector3 fixedWorldScale = Vector3.one;

    private readonly Dictionary<MeatCutSO, CoolerCutVisualConfig> configByCut = new Dictionary<MeatCutSO, CoolerCutVisualConfig>();
    private readonly Dictionary<MeatCutSO, List<GameObject>> spawnedByCut = new Dictionary<MeatCutSO, List<GameObject>>();
    private readonly HashSet<MeatCutSO> warnedMissingConfig = new HashSet<MeatCutSO>();
    private Type draggableType;

    void Awake()
    {
        BuildConfigLookup();
        draggableType = ResolveType("CoolerDraggableMeat");
    }

    void OnEnable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged += RefreshVisuals;
    }

    void Start()
    {
        RefreshVisuals();
    }

    void OnDisable()
    {
        if (coolerSystem != null)
            coolerSystem.OnInventoryChanged -= RefreshVisuals;
    }

    private void BuildConfigLookup()
    {
        configByCut.Clear();

        for (int i = 0; i < cutVisuals.Count; i++)
        {
            CoolerCutVisualConfig config = cutVisuals[i];
            if (config == null || config.cut == null)
                continue;

            if (configByCut.ContainsKey(config.cut))
                continue;

            configByCut.Add(config.cut, config);
        }
    }

    public void RefreshVisuals()
    {
        if (coolerSystem == null || stockVisualPrefab == null)
            return;

        warnedMissingConfig.Clear();

        HashSet<MeatCutSO> visitedCuts = new HashSet<MeatCutSO>();

        foreach (KeyValuePair<MeatCutSO, int> entry in coolerSystem.EnumerateStock())
        {
            MeatCutSO cut = entry.Key;
            int count = Mathf.Max(0, entry.Value);

            if (cut == null)
                continue;

            visitedCuts.Add(cut);
            SyncCutStack(cut, count);
        }

        for (int i = 0; i < cutVisuals.Count; i++)
        {
            CoolerCutVisualConfig config = cutVisuals[i];
            if (config == null || config.cut == null)
                continue;

            if (visitedCuts.Contains(config.cut))
                continue;

            SyncCutStack(config.cut, 0);
        }
    }

    private void SyncCutStack(MeatCutSO cut, int targetCount)
    {
        if (!configByCut.TryGetValue(cut, out CoolerCutVisualConfig config))
        {
            if (!warnedMissingConfig.Contains(cut))
            {
                warnedMissingConfig.Add(cut);
                string cutName = cut != null ? cut.cutName : "Sin corte";
                Debug.LogWarning("No hay config visual en CoolerStockVisualizer para el corte: " + cutName);
            }

            ClearCutObjects(cut);
            return;
        }

        if (!spawnedByCut.TryGetValue(cut, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            spawnedByCut.Add(cut, instances);
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
                renderer.sprite = cut.GetDefaultSprite();
                renderer.sortingOrder = config.baseSortingOrder + i;
            }

            Component draggable = draggableType != null ? go.GetComponent(draggableType) : null;
            if (draggable == null && draggableType != null)
                draggable = go.AddComponent(draggableType);

            if (draggable != null)
            {
                go.SendMessage("SetCut", cut, SendMessageOptions.DontRequireReceiver);
                go.SendMessage("SetCoolerSystem", coolerSystem, SendMessageOptions.DontRequireReceiver);
                go.SendMessage("SetTransferBuffer", meatTransferBuffer, SendMessageOptions.DontRequireReceiver);
                go.SendMessage("SetToGrillDropArea", toGrillDropArea, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type direct = Type.GetType(typeName);
        if (direct != null)
            return direct;

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type found = assemblies[i].GetType(typeName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ApplyFixedWorldScale(Transform target)
    {
        if (target == null)
            return;

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

    private void ClearCutObjects(MeatCutSO cut)
    {
        if (!spawnedByCut.TryGetValue(cut, out List<GameObject> instances))
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
public class CoolerCutVisualConfig
{
    public MeatCutSO cut;
    public Transform anchor;
    [Min(0.01f)] public float spacing = 0.2f;
    public Vector3 stackDirection = Vector3.up;
    public int baseSortingOrder = 0;
}
