using UnityEngine;

public class GameManager : MonoBehaviour
{
    public CustomerSystem customerSystem;
    public GrillSystem grillSystem;
    public CoolerSystem coolerSystem;
    public ViewManager viewManager;
    public MonoBehaviour meatTransferBuffer;

    private ViewType lastView;

    void Start()
    {
        lastView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (grillSystem != null)
            grillSystem.SetMeatVisualsVisible(lastView == ViewType.Grill);
    }

    void Update()
    {
        if (viewManager != null && Input.GetKeyDown(KeyCode.Tab))
        {
            viewManager.Toggle();

            if (coolerSystem != null && viewManager.CurrentView == ViewType.Cooler)
                Debug.Log(coolerSystem.GetDebugStockString());
        }

        ViewType currentView = viewManager != null ? viewManager.CurrentView : ViewType.Grill;

        if (currentView != lastView && grillSystem != null)
            grillSystem.SetMeatVisualsVisible(currentView == ViewType.Grill);

        if (currentView == ViewType.Grill && lastView != ViewType.Grill && meatTransferBuffer != null)
            meatTransferBuffer.SendMessage("MoveToMeatHolder", SendMessageOptions.DontRequireReceiver);

        lastView = currentView;

        if (currentView != ViewType.Grill)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
            TryServe();
    }

    void TryServe()
    {
        var customer = customerSystem.currentCustomer;

        if (customer == null) return;

        var meat = grillSystem.GetCookedMeat(customer.order.meat);

        if (meat != null)
        {
            Debug.Log("✔ Pedido correcto");

            grillSystem.RemoveMeat(meat);
            customerSystem.SpawnCustomer();
        }
        else
        {
            Debug.Log("❌ Carne incorrecta o no lista");
        }
    }
}
