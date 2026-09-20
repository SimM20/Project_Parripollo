using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Solo editor: devuelve las mejoras de tienda a nivel 0 al salir del Play Mode.
/// `UpgradeSO.currentLevel` se serializa en el asset, asi que una compra hecha jugando en el Editor
/// queda comprada para siempre. Esto la limpia sola, sin tocar el inspector a mano.
///
/// Tambien restaura `CoalSO.maxBurnTime`: la mejora `CoalBurnTime` escribe un valor absoluto en el
/// carbon, y poner `currentLevel` en 0 no lo desharia. El valor previo al Play se guarda en
/// `SessionState` porque entrar a Play descarga el dominio y un campo estatico se perderia.
///
/// Vive en una carpeta `Editor`: no entra en las builds.
/// </summary>
[InitializeOnLoad]
internal static class UpgradeStateResetter
{
    const string SnapshotKey = "Parripollo.CoalBurnTimeSnapshot";

    [Serializable]
    struct CoalEntry
    {
        public string guid;
        public float maxBurnTime;
    }

    [Serializable]
    struct Snapshot
    {
        public List<CoalEntry> coals;
    }

    static UpgradeStateResetter()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) CaptureCoalBurnTimes();
        else if (state == PlayModeStateChange.EnteredEditMode) RestoreUpgrades();
    }

    /// <summary>Guarda el maxBurnTime de cada carbon antes de que el Play lo pueda mutar.</summary>
    static void CaptureCoalBurnTimes()
    {
        var snapshot = new Snapshot { coals = new List<CoalEntry>() };

        foreach (string guid in AssetDatabase.FindAssets("t:CoalSO"))
        {
            var coal = AssetDatabase.LoadAssetAtPath<CoalSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (coal != null)
                snapshot.coals.Add(new CoalEntry { guid = guid, maxBurnTime = coal.maxBurnTime });
        }

        SessionState.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
    }

    /// <summary>Niveles a 0 y carbones al valor que tenian antes de entrar a Play.</summary>
    static void RestoreUpgrades()
    {
        int resetLevels = 0;
        int restoredCoals = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:UpgradeSO"))
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (upgrade == null || upgrade.currentLevel == 0) continue;

            upgrade.currentLevel = 0;
            EditorUtility.SetDirty(upgrade);
            resetLevels++;
        }

        string json = SessionState.GetString(SnapshotKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            var snapshot = JsonUtility.FromJson<Snapshot>(json);
            if (snapshot.coals != null)
            {
                for (int i = 0; i < snapshot.coals.Count; i++)
                {
                    CoalEntry entry = snapshot.coals[i];
                    var coal = AssetDatabase.LoadAssetAtPath<CoalSO>(AssetDatabase.GUIDToAssetPath(entry.guid));
                    if (coal == null || Mathf.Approximately(coal.maxBurnTime, entry.maxBurnTime)) continue;

                    coal.SetMaxBurnTime(entry.maxBurnTime);
                    EditorUtility.SetDirty(coal);
                    restoredCoals++;
                }
            }

            SessionState.EraseString(SnapshotKey);
        }

        if (resetLevels == 0 && restoredCoals == 0) return;

        AssetDatabase.SaveAssets();
        Debug.Log("[UpgradeStateResetter] Mejoras reseteadas: " + resetLevels
                + " nivel(es) a 0, " + restoredCoals + " carbon(es) restaurado(s).");
    }
}
