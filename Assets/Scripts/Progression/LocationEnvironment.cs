using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entorno de la parada: al cargar la escena de juego o la tienda, cambia el fondo de la escena
/// por el de la parada alcanzada en la ruta (<see cref="LocationSO.backgroundPrefab"/> /
/// <see cref="LocationSO.shopBackgroundPrefab"/>).
///
/// No hay que poner nada en las escenas: se engancha solo a <see cref="SceneManager.sceneLoaded"/>,
/// que corre después de los Awake y antes de los Start. Mientras ninguna parada tenga un fondo
/// asignado no hace nada y cada escena muestra el suyo, como siempre.
///
/// El cambio se ve al cargar la escena: comprar una parada en la tienda no cambia el fondo de la
/// tienda en el momento, se ve desde la jornada siguiente.
/// </summary>
public static class LocationEnvironment
{
    // Las mismas convenciones de nombre que ya usa SceneManagementUtils para cargar las escenas.
    private const string GameSceneName = "GameScene";
    private const string ShopSceneName = "EndScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isShop = scene.name == ShopSceneName;
        if (!isShop && scene.name != GameSceneName)
            return;

        // Sin run en curso (no hay tracker) no hay parada: queda el fondo de la escena.
        CoalConsumptionTracker tracker = CoalConsumptionTracker.Instance;
        FoodCatalogSO catalog = tracker != null ? tracker.Catalog : null;
        LocationRouteSO route = catalog != null ? catalog.LocationRoute : null;
        if (route == null)
            return;

        GameObject prefab = route.ResolveBackground(LocationProgression.CurrentIndex, isShop);
        if (prefab == null)
            return;

        DayCycleBackground current = Object.FindFirstObjectByType<DayCycleBackground>();
        if (current == null)
        {
            Debug.LogWarning("[Entorno] " + scene.name + " no tiene un fondo (DayCycleBackground) para reemplazar.");
            return;
        }

        // La parada usa el mismo fondo que ya trae la escena: no hay nada que cambiar.
        if (current.gameObject.name == prefab.name)
            return;

        Replace(current, prefab);
    }

    /// <summary>
    /// Pone el fondo nuevo en el lugar exacto del de la escena (padre, orden y transform de la
    /// raíz) y saca el viejo antes de que llegue a dibujarse.
    /// </summary>
    private static void Replace(DayCycleBackground current, GameObject prefab)
    {
        Transform old = current.transform;

        GameObject instance = Object.Instantiate(prefab, old.parent);
        instance.name = prefab.name;
        instance.transform.SetSiblingIndex(old.GetSiblingIndex());
        instance.transform.localPosition = old.localPosition;
        instance.transform.localRotation = old.localRotation;
        instance.transform.localScale = old.localScale;

        // La hora fija de la escena (la tienda es de noche) se conserva en el fondo nuevo.
        if (instance.TryGetComponent(out DayCycleBackground replacement))
            replacement.CopySettingsFrom(current);
        else
            Debug.LogWarning("[Entorno] El fondo " + prefab.name + " no tiene DayCycleBackground en la raíz: " +
                             "no va a seguir la hora del día.");

        old.gameObject.SetActive(false);
        Object.Destroy(old.gameObject);

        Debug.Log("[Entorno] Fondo de la parada aplicado: " + prefab.name);
    }
}
