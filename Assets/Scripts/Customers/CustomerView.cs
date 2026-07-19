using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerView : MonoBehaviour
{
    [Header("Patience Bar (fill sprite)")]
    [SerializeField] private Transform patienceFill; // escalar en X (0..1)
    [SerializeField] private float fillFullX = 1f;

    [Header("Selection Visual (optional)")]
    [SerializeField] private GameObject selectionHighlight;

    private Customer customer;
    private CustomerSystem system;

    public Customer Customer => customer;

    public void Init(Customer c, CustomerSystem owner)
    {
        customer = c;
        system = owner;
        RefreshSelection(false);
        RefreshPatience();
    }

    void Update()
    {
        if (customer == null) return;
        RefreshPatience();
    }

    void OnMouseDown()
    {
        if (system != null && customer != null)
            system.SelectCustomer(customer);
    }

    void OnMouseEnter()
    {
        if (customer?.order == null) return;
        if (CustomerHoverBubble.Instance == null) return;

        Sprite dishSprite = ResolveDishSprite(customer.order);

        CustomerHoverBubble.Instance.Show(
            customer.order.ToHoverString(),
            transform,
            dishSprite);
    }

    private Sprite ResolveDishSprite(Order order)
    {
        if (system == null || system.Catalog == null) return null;
        var variant = system.Catalog.GetVariantForOrder(order);
        return variant != null ? variant.variantSprite : null;
    }
    void OnMouseExit()
    {
        if (CustomerHoverBubble.Instance != null)
            CustomerHoverBubble.Instance.Hide();
    }

    public void RefreshSelection(bool selected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(selected);
    }

    private void RefreshPatience()
    {
        if (patienceFill == null) return;

        var s = patienceFill.localScale;
        s.x = fillFullX * customer.Patience01;
        patienceFill.localScale = s;
    }
}