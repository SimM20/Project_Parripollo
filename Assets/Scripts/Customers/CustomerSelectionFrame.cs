using UnityEngine;

/// <summary>
/// Recuadro de selección de 4 esquinas que envuelve al cliente seleccionado
/// durante el modo de selección de entrega. Singleton de escena,
/// mismo patrón que CustomerHoverBubble.
/// </summary>
public class CustomerSelectionFrame : MonoBehaviour
{
    public static CustomerSelectionFrame Instance { get; private set; }

    [Header("Visual")]
    [SerializeField] private Color color = Color.white;
    [SerializeField] private float padding = 0.15f;
    [SerializeField, Range(0.05f, 0.5f)] private float cornerLengthFraction = 0.22f;
    [SerializeField] private float barThickness = 0.07f;
    [SerializeField] private int sortingOrder = 200;

    private CustomerView target;
    private Vector3 centerOffset;
    private SpriteRenderer[] bars; // 8: barra horizontal + vertical por esquina
    private Sprite squareSprite;

    void Awake()
    {
        Instance = this;
        BuildBars();
        Hide();
    }

    void LateUpdate()
    {
        if (target != null)
            transform.position = target.transform.position + centerOffset;
    }

    public void ShowOver(CustomerView view)
    {
        if (view == null)
        {
            Hide();
            return;
        }

        target = view;
        gameObject.SetActive(true);
        FitToTarget();
    }

    public void Hide()
    {
        target = null;
        gameObject.SetActive(false);
    }

    private void BuildBars()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        bars = new SpriteRenderer[8];
        for (int i = 0; i < bars.Length; i++)
        {
            var go = new GameObject("Bar" + i);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = squareSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            bars[i] = sr;
        }
    }

    private void FitToTarget()
    {
        var renderers = target.GetComponentsInChildren<SpriteRenderer>(false);

        var b = new Bounds();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled) continue;

            if (!hasBounds)
            {
                b = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
            b = new Bounds(target.transform.position, Vector3.one);

        b.Expand(padding * 2f);

        centerOffset = new Vector3(b.center.x, b.center.y, target.transform.position.z) - target.transform.position;
        transform.position = target.transform.position + centerOffset;

        float len = Mathf.Min(b.size.x, b.size.y) * cornerLengthFraction;
        float hx = b.extents.x;
        float hy = b.extents.y;

        int barIndex = 0;
        for (int cx = -1; cx <= 1; cx += 2)
        {
            for (int cy = -1; cy <= 1; cy += 2)
            {
                var horizontal = bars[barIndex++].transform;
                horizontal.localPosition = new Vector3(cx * (hx - len * 0.5f), cy * hy, 0f);
                horizontal.localScale = new Vector3(len, barThickness, 1f);

                var vertical = bars[barIndex++].transform;
                vertical.localPosition = new Vector3(cx * hx, cy * (hy - len * 0.5f), 0f);
                vertical.localScale = new Vector3(barThickness, len, 1f);
            }
        }
    }
}
