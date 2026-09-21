using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de strikes: una X por cada strike posible (la cantidad se adapta a
/// <see cref="StrikeSystem.MaxStrikes"/>). Inactiva = apagada, activa = roja. La X recién
/// activada hace un shake. Nunca muestra más X que el máximo configurado.
///
/// Vive en HudCanvas/MainPanel como un contenedor más. Las X se generan en runtime debajo
/// de este objeto; si no hay sprite asignado se dibuja una X procedural (mismo patrón que
/// otros visuales del proyecto), a reemplazar cuando Arte defina el estilo definitivo.
/// </summary>
public class StrikeHudView : MonoBehaviour
{
    [Header("Icono")]
    [Tooltip("Sprite de la X. Vacío = X procedural.")]
    [SerializeField] private Sprite strikeSprite;
    [Tooltip("Tamaño de cada X en unidades del canvas (el HudCanvas es world-space).")]
    [SerializeField] private float iconSize = 0.42f;
    [SerializeField] private float iconSpacing = 0.08f;
    [SerializeField] private Color inactiveColor = new Color(0.16f, 0.16f, 0.16f, 0.45f);
    [SerializeField] private Color activeColor = new Color(0.93f, 0.2f, 0.16f, 1f);

    [Header("Shake al sumar strike (ajuste UX)")]
    [SerializeField] private float shakeDuration = 0.4f;
    [Tooltip("Desplazamiento máximo del shake, en unidades del canvas.")]
    [SerializeField] private float shakeStrength = 0.06f;
    [Tooltip("Escala inicial del punch al activarse (vuelve a 1 durante el shake).")]
    [SerializeField] private float punchScale = 1.45f;

    // Una entrada por X: el holder lo posiciona el layout, la imagen es la que se sacude.
    private readonly List<RectTransform> icons = new List<RectTransform>();
    private readonly List<Image> iconImages = new List<Image>();
    private Coroutine shakeRoutine;
    private StrikeSystem subscribedTo;
    private static Sprite proceduralSprite;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // StrikeSystem.Instance se asigna en Awake; en OnEnable de la misma pasada puede no existir todavía.
        TrySubscribe();
        RebuildIcons();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        StrikeSystem system = StrikeSystem.Instance;
        if (system == null || subscribedTo == system) return;

        Unsubscribe();

        system.OnStrikeAdded += HandleStrikeAdded;
        system.OnReset += HandleReset;
        subscribedTo = system;
    }

    private void Unsubscribe()
    {
        if (subscribedTo == null) return;

        subscribedTo.OnStrikeAdded -= HandleStrikeAdded;
        subscribedTo.OnReset -= HandleReset;
        subscribedTo = null;
    }

    private void HandleReset()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        RebuildIcons();
        RefreshAll();
    }

    private void HandleStrikeAdded(int current, int max)
    {
        if (icons.Count != max)
            RebuildIcons();

        RefreshAll();

        int index = current - 1;
        if (index < 0 || index >= icons.Count) return;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeIcon(icons[index]));
    }

    /// <summary>Genera tantas X como el máximo configurado (o 0 si no hay StrikeSystem).</summary>
    private void RebuildIcons()
    {
        for (int i = icons.Count - 1; i >= 0; i--)
        {
            if (icons[i] != null)
                Destroy(icons[i].parent.gameObject);
        }

        icons.Clear();
        iconImages.Clear();

        int count = StrikeSystem.Instance != null ? StrikeSystem.Instance.MaxStrikes : 0;

        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.spacing = iconSpacing;

        Sprite sprite = strikeSprite != null ? strikeSprite : GetProceduralSprite();

        for (int i = 0; i < count; i++)
        {
            // Holder: lo acomoda el HorizontalLayoutGroup del contenedor.
            GameObject holder = new GameObject("Strike " + (i + 1), typeof(RectTransform), typeof(LayoutElement));
            holder.layer = gameObject.layer;
            RectTransform holderRect = holder.GetComponent<RectTransform>();
            holderRect.SetParent(transform, false);
            holderRect.sizeDelta = new Vector2(iconSize, iconSize);

            LayoutElement element = holder.GetComponent<LayoutElement>();
            element.preferredWidth = iconSize;
            element.preferredHeight = iconSize;

            // Imagen: es la que se mueve en el shake, así el layout no la pisa.
            GameObject iconObject = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = gameObject.layer;
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(holderRect, false);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            icons.Add(iconRect);
            iconImages.Add(image);
        }
    }

    private void RefreshAll()
    {
        int current = StrikeSystem.Instance != null ? StrikeSystem.Instance.CurrentStrikes : 0;

        for (int i = 0; i < iconImages.Count; i++)
        {
            if (iconImages[i] == null) continue;

            iconImages[i].color = i < current ? activeColor : inactiveColor;
            icons[i].anchoredPosition = Vector2.zero;
            icons[i].localScale = Vector3.one;
        }
    }

    private IEnumerator ShakeIcon(RectTransform icon)
    {
        float duration = Mathf.Max(0.01f, shakeDuration);
        float t = 0f;

        while (t < duration && icon != null)
        {
            t += Time.deltaTime;
            float remaining = 1f - Mathf.Clamp01(t / duration);

            // El shake se apaga hacia el final; el punch de escala vuelve a 1 con la misma curva.
            Vector2 offset = Random.insideUnitCircle * shakeStrength * remaining;
            icon.anchoredPosition = offset;
            icon.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, remaining * remaining);

            yield return null;
        }

        if (icon != null)
        {
            icon.anchoredPosition = Vector2.zero;
            icon.localScale = Vector3.one;
        }

        shakeRoutine = null;
    }

    /// <summary>X de dos trazos diagonales con borde suavizado. Placeholder hasta que Arte entregue la definitiva.</summary>
    private static Sprite GetProceduralSprite()
    {
        if (proceduralSprite != null) return proceduralSprite;

        const int size = 64;
        const float halfThickness = 7f;
        const float margin = 8f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        float last = size - 1f;
        float invSqrt2 = 1f / Mathf.Sqrt(2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distancia a las dos diagonales del cuadrado.
                float d1 = Mathf.Abs(x - y) * invSqrt2;
                float d2 = Mathf.Abs(x + y - last) * invSqrt2;
                float d = Mathf.Min(d1, d2);

                // Recorta las puntas para que la X no llegue hasta el borde del icono.
                float edge = Mathf.Min(Mathf.Min(x, last - x), Mathf.Min(y, last - y));
                float alpha = Mathf.Clamp01(halfThickness - d + 0.5f) * Mathf.Clamp01(edge - margin + 1f);

                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        proceduralSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        proceduralSprite.name = "StrikeX_Procedural";
        return proceduralSprite;
    }
}
