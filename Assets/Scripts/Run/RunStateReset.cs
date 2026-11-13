using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Devuelve al estado inicial los ScriptableObject que mutan durante una run.
///
/// Los DDOL (<see cref="PlayerWallet"/>, <see cref="CoolerSystem"/>,
/// <see cref="CoalConsumptionTracker"/>, <see cref="ToppingStock"/>) se destruyen y se
/// recrean solos, pero las mejoras viven en assets: sin esto, "Reintentar" arrancaría el
/// Día 1 con todas las mejoras ya compradas (ver nota 5 de ARQUITECTURA_PROYECTO.md).
///
/// Los <c>isUnlocked</c> de cortes, variantes y mejoras NO se tocan acá a propósito: los fija
/// <see cref="LocationProgression"/> según la parada alcanzada, que vive en
/// <see cref="CoalConsumptionTracker"/>. Con el tracker destruido la ruta vuelve a la parada
/// inicial y el tracker nuevo los corrige en su Awake.
///
/// Es el equivalente en runtime de <c>Scripts/Editor/UpgradeStateResetter.cs</c>, que solo
/// corre al salir del Play Mode.
/// </summary>
public static class RunStateReset
{
    // El efecto CoalBurnTime escribe un valor ABSOLUTO en CoalSO._maxBurnTime, así que bajar
    // currentLevel a 0 no lo desharía: hace falta el valor original. Mismo razonamiento que
    // el snapshot en SessionState de UpgradeStateResetter.
    private static readonly Dictionary<CoalSO, float> coalBaseline = new Dictionary<CoalSO, float>();

    private static bool captured;

    /// <summary>
    /// Guarda el <c>maxBurnTime</c> original de cada carbón al que apunte una mejora.
    /// Solo la primera llamada cuenta: después las mejoras ya pueden estar compradas.
    /// </summary>
    public static void CaptureBaseline(FoodCatalogSO catalog)
    {
        if (captured || catalog == null) return;

        captured = true;
        coalBaseline.Clear();

        var upgrades = catalog.GetAllUpgrades();
        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeSO upgrade = upgrades[i];
            if (upgrade == null || upgrade.targetCoal == null) continue;

            coalBaseline[upgrade.targetCoal] = upgrade.targetCoal.maxBurnTime;
        }
    }

    /// <summary>
    /// Mejoras a nivel 0 y carbones a su combustión original. Se llama al reiniciar la run.
    /// </summary>
    public static void ResetRunState(FoodCatalogSO catalog)
    {
        foreach (var entry in coalBaseline)
        {
            if (entry.Key != null)
                entry.Key.SetMaxBurnTime(entry.Value);
        }

        if (catalog == null)
        {
            Debug.LogWarning(
                "[RunStateReset] Sin FoodCatalogSO: no se pudieron reiniciar los niveles de mejora."
            );
            return;
        }

        var upgrades = catalog.GetAllUpgrades();
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i] != null)
                upgrades[i].currentLevel = 0;
        }

        Debug.Log("[RunStateReset] Mejoras y carbones devueltos a su estado inicial.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        // El Editor puede entrar a Play sin domain reload: el snapshot de la sesión anterior
        // no sirve para esta.
        coalBaseline.Clear();
        captured = false;
    }
}
