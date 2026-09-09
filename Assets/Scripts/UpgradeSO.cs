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

    [Header("Efecto: duracion del carbon")]
    [Tooltip("Carbon afectado. Si es null, la mejora no aplica ningun efecto.")]
    public CoalSO targetCoal;

    [Tooltip("Nuevo maxBurnTime del carbon al comprar la mejora.")]
    public float upgradedMaxBurnTime = 0f;

    /// <summary>Aplica el efecto. Idempotente: fija un valor absoluto, no acumula.</summary>
    public void ApplyEffect()
    {
        if (targetCoal == null || upgradedMaxBurnTime <= 0f) return;
        targetCoal.SetMaxBurnTime(upgradedMaxBurnTime);
    }

    void OnValidate()
    {
    }
}