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
        if (CustomerHoverBubble.Instance != null)
            CustomerHoverBubble.Instance.Show(customer.order.ToHoverString(), transform);
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