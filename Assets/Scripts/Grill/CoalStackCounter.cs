using TMPro;
using UnityEngine;

[System.Serializable]
public class CoalStackCounterStyle
{
    public TMP_FontAsset font;
    [Min(0.1f)] public float fontSize = 3f;
    public Color textColor = Color.white;
    [Tooltip("Desplazamiento en unidades locales desde la esquina inferior derecha del sprite del slot.")]
    public Vector2 offset = new Vector2(-0.05f, 0.05f);
    public int sortingOrder = 50;
}

// Contador "xN" que aparece abajo a la derecha del sprite del slot cuando hay dos o mas carbones apilados.
public class CoalStackCounter : MonoBehaviour
{
    private const int MIN_VISIBLE_COUNT = 2;

    private GridSlot slot;
    private TextMeshPro label;
    private int lastCount = -1;
    private bool viewVisible = true;

    public static CoalStackCounter Attach(GridSlot targetSlot, CoalStackCounterStyle style)
    {
        if (targetSlot == null || style == null) return null;

        CoalStackCounter counter = targetSlot.GetComponent<CoalStackCounter>();
        if (counter == null) counter = targetSlot.gameObject.AddComponent<CoalStackCounter>();

        counter.Build(targetSlot, style);
        return counter;
    }

    public void SetViewVisible(bool isVisible)
    {
        viewVisible = isVisible;
        Refresh();
    }

    private void Build(GridSlot targetSlot, CoalStackCounterStyle style)
    {
        slot = targetSlot;

        if (label == null)
        {
            // El componente se agrega mientras el objeto sigue en la raiz y activo: si se parenta antes a un
            // slot inactivo, el Awake de TextMeshPro no corre y asignar la fuente tira NullReferenceException.
            GameObject labelObject = new GameObject("CoalStackCounter");
            label = labelObject.AddComponent<TextMeshPro>();
            labelObject.transform.SetParent(slot.transform, false);
        }

        if (style.font != null) label.font = style.font;
        label.fontSize = style.fontSize;
        label.color = style.textColor;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = label.rectTransform;
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(2f, 1f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.localPosition = GetBottomRightLocalPosition(style.offset);

        MeshRenderer meshRenderer = label.GetComponent<MeshRenderer>();
        if (meshRenderer != null) meshRenderer.sortingOrder = style.sortingOrder;

        lastCount = -1;
        Refresh();
    }

    private Vector3 GetBottomRightLocalPosition(Vector2 offset)
    {
        SpriteRenderer slotRenderer = slot.GetComponent<SpriteRenderer>();
        if (slotRenderer == null || slotRenderer.sprite == null)
            return new Vector3(offset.x, offset.y, 0f);

        Bounds spriteBounds = slotRenderer.sprite.bounds;
        return new Vector3(spriteBounds.max.x + offset.x, spriteBounds.min.y + offset.y, 0f);
    }

    void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (slot == null || label == null) return;

        int count = (slot.stackedCoals != null) ? slot.stackedCoals.Count : 0;

        if (count != lastCount)
        {
            lastCount = count;
            if (count >= MIN_VISIBLE_COUNT) label.text = "x" + count;
        }

        bool shouldShow = viewVisible
                       && count >= MIN_VISIBLE_COUNT
                       && GrillLayerToggle.IsItemTypeAllowed(ItemType.Coal);

        if (label.gameObject.activeSelf != shouldShow) label.gameObject.SetActive(shouldShow);
    }
}
