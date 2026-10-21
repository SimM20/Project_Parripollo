using System.Collections.Generic;
using UnityEngine;

public class TrashZone : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private Collider2D zoneCollider;
    [SerializeField] private bool onlyWhenGrillView = true;
    [SerializeField] private ViewManager viewManager;

    [Header("Feedback")]
    [Tooltip("Segundos que tarda lo tirado en caer adentro del tacho (solo visual: ya se destruyo).")]
    [Min(0.01f)] [SerializeField] private float swallowSeconds = 0.2f;
    [Tooltip("Onda que se abre detras del tacho al tragar algo. No toca su transform ni su collider.")]
    [SerializeField] private bool binEcho = true;

    private static readonly List<TrashZone> ActiveZones = new List<TrashZone>();

    void Awake() => EnsureReferences();

    void OnEnable()
    {
        if (!ActiveZones.Contains(this))
            ActiveZones.Add(this);
    }

    void OnDisable() => ActiveZones.Remove(this);

    public static bool TryConsumeAtWorldPoint(Vector3 worldPoint, Item item)
    {
        if (item == null)
            return false;

        for (int i = 0; i < ActiveZones.Count; i++)
        {
            TrashZone zone = ActiveZones[i];
            if (zone == null)
                continue;

            if (!zone.CanConsumeNow())
                continue;

            if (!zone.ContainsPoint(worldPoint))
                continue;

            zone.Consume(item);
            return true;
        }

        return false;
    }

    private bool CanConsumeNow()
    {
        if (!onlyWhenGrillView)
            return true;

        if (viewManager == null)
            return true;

        return viewManager.CurrentView == ViewType.Grill;
    }

    private bool ContainsPoint(Vector3 worldPoint)
    {
        Collider2D targetCollider = GetZoneCollider();
        if (targetCollider == null)
            return false;

        return targetCollider.OverlapPoint(new Vector2(worldPoint.x, worldPoint.y));
    }

    private void Consume(Item item)
    {
        if (item == null)
            return;

        string itemName = "Item desconocido";

        if (item is Meat meat)
        {
            itemName = meat.cut != null ? meat.cut.cutName : "Carne sin corte";
            meat.ReleaseOccupiedSlots();
        }
        else if (item is Coal coal)
        {
            itemName = "Carbón";
            coal.ReleaseOccupiedSlots();
        }

        PlaySwallowFeedback(item);
        Destroy(item.gameObject);

        Debug.Log("Objeto tirado a la basura: " + itemName);
    }

    /// <summary>
    /// Lo tirado cae por la boca del tacho (un fantasma: el objeto real ya se destruye) y el tacho
    /// "traga" con una onda detras. Sin escalar ni mover el tacho: es una zona de drop.
    /// </summary>
    private void PlaySwallowFeedback(Item item)
    {
        SpriteRenderer source = item is Meat meat ? meat.VisualRenderer
                              : item is Coal coal ? coal.VisualRenderer
                              : item.GetComponent<SpriteRenderer>();

        SpriteRenderer bin = GetComponent<SpriteRenderer>();
        Vector3 mouth = transform.position;
        if (bin != null)
        {
            Bounds b = bin.bounds;
            mouth = new Vector3(b.center.x, b.max.y - b.size.y * 0.2f, transform.position.z);
        }

        SpriteGhost.Spawn(source, SpriteGhost.Motion.Into(mouth, swallowSeconds));

        if (binEcho && bin != null)
            SpriteGhost.Spawn(bin, SpriteGhost.Motion.Echo(1.15f, 0.35f, 0.22f));
    }

    private Collider2D GetZoneCollider()
    {
        EnsureReferences();
        return zoneCollider;
    }

    private void EnsureReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (zoneCollider == null)
            zoneCollider = GetComponentInChildren<Collider2D>();

        if (viewManager == null)
            viewManager = FindFirstObjectByType<ViewManager>();
    }

    void OnValidate() => EnsureReferences();
}
