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

    void Start()
    {
        Show(startView);
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
        CurrentView = view;

        if (grillRoot != null)
            grillRoot.SetActive(view == ViewType.Grill);

        if (coolerRoot != null)
            coolerRoot.SetActive(view == ViewType.Cooler);

        if (buildRoot != null)
            buildRoot.SetActive(view == ViewType.Build);
    }
}
