using UnityEngine;

public class MeatClickable : MonoBehaviour
{
    private Meat meat;

    void Start()
    {
        meat = GetComponent<Meat>();
    }

    void OnMouseDown()
    {
        meat.Flip();
    }
}