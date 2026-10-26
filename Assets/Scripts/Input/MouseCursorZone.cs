using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área del mundo donde el cursor del mouse toma otra forma (<see cref="MouseCursorView"/>),
/// por ejemplo el tenedor sobre la bandeja y el plato de la zona de armado.
/// El área es la del SpriteRenderer del objeto: sigue al sprite si se mueve o se escala, y no
/// cuenta mientras el sprite está apagado.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MouseCursorZone : MonoBehaviour
{
    [SerializeField] private MouseCursorView.Skin skin = MouseCursorView.Skin.Fork;

    private static readonly List<MouseCursorZone> ActiveZones = new List<MouseCursorZone>();

    private SpriteRenderer area;

    private void Awake() => area = GetComponent<SpriteRenderer>();

    private void OnEnable() => ActiveZones.Add(this);

    private void OnDisable() => ActiveZones.Remove(this);

    /// <summary>Cursor de la zona que está bajo el puntero, si hay alguna.</summary>
    public static bool TryGetSkinUnderPointer(out MouseCursorView.Skin skin)
    {
        for (int i = 0; i < ActiveZones.Count; i++)
        {
            MouseCursorZone zone = ActiveZones[i];
            if (!zone.area.enabled) continue;

            Bounds bounds = zone.area.bounds;
            if (!MouseCursorView.TryGetPointerOnPlane(bounds.center.z, out Vector3 point)) continue;

            point.z = bounds.center.z;
            if (bounds.Contains(point))
            {
                skin = zone.skin;
                return true;
            }
        }

        skin = MouseCursorView.Skin.None;
        return false;
    }
}
