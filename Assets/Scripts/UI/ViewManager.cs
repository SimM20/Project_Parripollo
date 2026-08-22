using UnityEngine;

public class ViewManager : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject grillRoot;
    [SerializeField] private GameObject coolerRoot;
    [SerializeField] private GameObject buildRoot;
    [SerializeField] private GameObject shopRoot;

    [Header("State")]
    [SerializeField] private ViewType startView = ViewType.Grill;

    public ViewType CurrentView { get; private set; }
    public event System.Action<ViewType> OnViewChanged;

    void Awake()
    {
        if (shopRoot == null)
        {
            GameObject sr = GameObject.Find("ShopRoot");
            if (sr != null)
                shopRoot = sr;
            else
            {
                ShopSystem shop = FindFirstObjectByType<ShopSystem>(FindObjectsInactive.Include);
                if (shop != null && shop.transform != null)
                {
                    Transform t = shop.transform.Find("ShopRoot");
                    if (t != null)
                        shopRoot = t.gameObject;
                }
            }
        }
    }

    void Start() => Show(startView);

    public void NextView()
    {
        if (CurrentView == ViewType.Grill)
            Show(ViewType.Build);
    }

    public void PreviousView()
    {
        if (CurrentView == ViewType.Build)
            Show(ViewType.Grill);
    }

    public void Show(ViewType view)
    {
        if (view == ViewType.Cooler)
        {
            Debug.LogWarning("[ViewManager] La Cooler View esta deprecada; el stock vive en el StockPanel de la Vista Parrilla. Redirigiendo a Grill.");
            view = ViewType.Grill;
        }

        if (!TutorialManager.CheckViewChangeAllowed(view))
        {
            Debug.Log($"[ViewManager] Cambio de vista a {view} bloqueado por el tutorial.");
            return;
        }

        ViewType oldView = CurrentView;
        CurrentView = view;

        if (grillRoot != null)
            SetVisualVisibility(grillRoot, view == ViewType.Grill);

        if (coolerRoot != null)
            coolerRoot.SetActive(view == ViewType.Cooler);

        if (buildRoot != null)
            buildRoot.SetActive(view == ViewType.Build);

        if (shopRoot != null)
            shopRoot.SetActive(view == ViewType.Shop);
        else if (view == ViewType.Shop)
        {
            GameObject sr = GameObject.Find("ShopRoot");
            if (sr != null) sr.SetActive(true);
        }
        
        if (oldView != view)
        {
            OnViewChanged?.Invoke(view);
        }
    }

    private static void SetVisualVisibility(GameObject root, bool visible)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = visible;

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = visible;

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
            canvases[i].enabled = visible;
    }
}
