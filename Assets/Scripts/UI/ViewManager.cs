using UnityEngine;

public class ViewManager : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private GameObject grillRoot;
    [SerializeField] private GameObject coolerRoot;
    [SerializeField] private GameObject buildRoot;

    [Header("State")]
    [SerializeField] private ViewType startView = ViewType.Grill;

    public ViewType CurrentView { get; private set; }
    public event System.Action<ViewType> OnViewChanged;

    void Start() => Show(startView);

    public void NextView()
    {
        if (CurrentView == ViewType.Cooler)
            Show(ViewType.Grill);
        else if (CurrentView == ViewType.Grill)
            Show(ViewType.Build);
    }

    public void PreviousView()
    {
        if (CurrentView == ViewType.Build)
            Show(ViewType.Grill);
        else if (CurrentView == ViewType.Grill)
            Show(ViewType.Cooler);
    }

    public void Toggle()
    {
        switch (CurrentView)
        {
            case ViewType.Grill:
                Show(ViewType.Cooler);
                break;
            case ViewType.Cooler:
                Show(ViewType.Build);
                break;
            default:
                Show(ViewType.Grill);
                break;
        }
    }

    public void Show(ViewType view)
    {
        ViewType oldView = CurrentView;
        CurrentView = view;

        if (grillRoot != null)
            SetVisualVisibility(grillRoot, view == ViewType.Grill);

        if (coolerRoot != null)
            coolerRoot.SetActive(view == ViewType.Cooler);

        if (buildRoot != null)
            buildRoot.SetActive(view == ViewType.Build);
        
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
