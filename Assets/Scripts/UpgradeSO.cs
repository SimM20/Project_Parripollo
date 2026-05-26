using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade", menuName = "Asado/Upgrade")]
public class UpgradeSO : ItemDataSO
{
    [Header("Display")]
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("Estado")]
    [Tooltip("Si está bloqueada, aparece oscurecida")]
    public bool isUnlocked = true;

    [Tooltip("Para evitr que se pueda comprar 2 veces")]
    public bool isPurchased = false;

    void OnValidate()
    {
    }
}