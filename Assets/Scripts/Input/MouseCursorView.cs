using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Cursor del sistema con el arte del juego (solo con mouse: con gamepad el cursor del sistema
/// está oculto y el puntero lo dibuja <see cref="GamepadCursorView"/>):
///  • Flecha de madera por defecto.
///  • Sobre la parrilla, la herramienta de la capa activa (<see cref="GrillLayerToggle"/>):
///    pinza en la capa de carne, pala en la de carbón.
///  • Sobre una <see cref="MouseCursorZone"/>, el cursor de la zona (el tenedor en la bandeja
///    y el plato de la zona de armado).
/// Va en el prefab de <see cref="InputManager"/>, así vale en todas las escenas. Solo llama a
/// <see cref="Cursor.SetCursor(Texture2D, Vector2, CursorMode)"/> cuando cambia el cursor que corresponde.
/// Las texturas van importadas como Texture Type = Cursor.
/// </summary>
[RequireComponent(typeof(InputManager))]
public class MouseCursorView : MonoBehaviour
{
    [System.Serializable]
    private struct CursorSkin
    {
        public Texture2D texture;
        [Tooltip("Punto que hace click, en píxeles desde la esquina superior izquierda de la textura.")]
        public Vector2 hotspot;
    }

    public enum Skin { None, Arrow, Tongs, Shovel, Fork }

    [SerializeField] private CursorSkin arrow;
    [Tooltip("Sobre la parrilla en la capa de carne.")]
    [SerializeField] private CursorSkin tongs;
    [Tooltip("Sobre la parrilla en la capa de carbón.")]
    [SerializeField] private CursorSkin shovel;
    [Tooltip("Sobre las zonas marcadas con MouseCursorZone (bandeja y plato de armado).")]
    [SerializeField] private CursorSkin fork;

    private Skin current = Skin.None;

    private void LateUpdate()
    {
        // Con gamepad el cursor del sistema no se ve: se resuelve al volver al mouse.
        if (InputManager.UsingGamepad) return;

        Apply(Resolve());
    }

    private void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        current = Skin.None;
    }

    private static Skin Resolve()
    {
        if (GamePause.IsPaused || IsPointerOverUI()) return Skin.Arrow;

        Skin grill = ResolveGrill();
        if (grill != Skin.None) return grill;

        return MouseCursorZone.TryGetSkinUnderPointer(out Skin zone) ? zone : Skin.Arrow;
    }

    /// <summary>Herramienta de la capa activa si el puntero está sobre la parrilla; si no, <see cref="Skin.None"/>.</summary>
    private static Skin ResolveGrill()
    {
        GrillLayerToggle toggle = GrillLayerToggle.Current;
        if (toggle == null || toggle.Grill == null) return Skin.None;

        bool coalLayer = toggle.CurrentLayer == GrillLayerToggle.GrillLayer.Coal;
        GrillSystem grill = toggle.Grill;
        if (!TryGetPointerOnPlane(grill.transform.position.z, out Vector3 point)
            || !GridSlot.IsOverSlotsOfType(grill.slots, coalLayer ? ItemType.Coal : ItemType.Meat, point))
            return Skin.None;

        return coalLayer ? Skin.Shovel : Skin.Tongs;
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>Punto del mundo bajo el puntero en el plano z dado (la cámara es en perspectiva).</summary>
    public static bool TryGetPointerOnPlane(float z, out Vector3 point)
    {
        point = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(InputManager.PointerPosition);
        if (!new Plane(Vector3.forward, new Vector3(0f, 0f, z)).Raycast(ray, out float distance)) return false;

        point = ray.GetPoint(distance);
        return true;
    }

    private void Apply(Skin skin)
    {
        if (skin == current) return;

        CursorSkin chosen = skin switch
        {
            Skin.Tongs => tongs,
            Skin.Shovel => shovel,
            Skin.Fork => fork,
            _ => arrow,
        };
        if (chosen.texture == null) chosen = arrow;

        // Sin textura asignada queda el cursor de Windows.
        Cursor.SetCursor(chosen.texture, chosen.texture != null ? chosen.hotspot : Vector2.zero, CursorMode.Auto);
        current = skin;
    }
}
