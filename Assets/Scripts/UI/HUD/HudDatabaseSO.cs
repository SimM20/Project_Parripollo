using UnityEngine;

[System.Serializable]
public struct HudData
{
    public HudContainers type;
    public Sprite icon;
    public string defaultText;
}

[CreateAssetMenu(fileName = "NewHudDatabase", menuName = "UI/Hud Database")]
public class HudDatabaseSO : ScriptableObject
{
    public HudData[] allContainersData;

    public HudData GetData(HudContainers typeToFind)
    {
        foreach (var data in allContainersData)
        {
            if (data.type == typeToFind)
                return data;
        }
        return new HudData();
    }
}