using UnityEngine;

/// <summary>
/// Activa un GameObject de la escena cuando la mejora asociada ya tiene al menos un nivel comprado.
///
/// El objetivo arranca desactivado en la escena, asi que este componente NO puede vivir en el:
/// va en un objeto siempre activo (por ejemplo `[SYSTEMS]`) con la referencia serializada.
/// El `SetActive` es solo de runtime: la escena guardada no se toca y al salir de Play vuelve sola,
/// igual que `UpgradeStateResetter` devuelve `UpgradeSO.currentLevel` a 0.
/// </summary>
public class UpgradeUnlockActivator : MonoBehaviour
{
    [Tooltip("Mejora que desbloquea el objeto. Con CurrentLevel >= 1 se activa.")]
    [SerializeField] private UpgradeSO upgrade;

    [Tooltip("Objeto de la escena a activar. Debe estar desactivado por defecto.")]
    [SerializeField] private GameObject target;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning(
                "[UpgradeUnlockActivator] Sin objetivo asignado en " + name + "."
            );

            return;
        }

        if (upgrade == null)
        {
            Debug.LogWarning(
                "[UpgradeUnlockActivator] Sin mejora asignada en " + name +
                ": " + target.name + " queda desactivado."
            );

            target.SetActive(false);
            return;
        }

        bool unlocked = upgrade.CurrentLevel > 0;

        target.SetActive(unlocked);

        Debug.Log(
            "[UpgradeUnlockActivator] " + upgrade.itemName +
            " nivel " + upgrade.CurrentLevel +
            " -> " + target.name + (unlocked ? " activado" : " desactivado")
        );
    }
}
