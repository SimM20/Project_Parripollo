using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Burbuja de pedido pegada a cada cliente. A diferencia de la vieja CustomerHoverBubble
/// (una sola burbuja compartida en la escena, que aparecia solo al pasar el mouse y ya no
/// existe), cada cliente tiene la suya y siempre esta a la vista, a la altura del pecho.
///
/// Es un unico panel con dos estados:
/// - <b>Base</b>: corte + acompaniamientos en iconos. Siempre visible.
/// - <b>Expandida</b>: el mismo panel crece y agrega el punto de coccion y los nombres.
///   Se dispara con el hover, con la seleccion por teclado y con el arrastre del plato
///   (que ademas mete la linea de preview economico).
///
/// La jerarquia se arma por codigo (mismo patron que <see cref="CustomerFeedbackBubble"/>)
/// para que ande sin tener que configurar nada en los prefabs.
/// </summary>
public class CustomerOrderBubble : MonoBehaviour
{
    [Header("Posicion")]
    [Tooltip("Offset en unidades de mundo desde el origen del cliente. El Y cae sobre el pecho " +
             "y el Z acompania al plano del personaje: separarlos en Z desalinea la burbuja " +
             "porque la camara del juego es perspectiva.")]
    [SerializeField] private Vector3 chestOffset = new Vector3(0f, 0.04f, -0.13f);

    [Header("Tamanio del panel (unidades de mundo)")]
    [Tooltip("Los clientes estan a 2.6 de distancia entre si, asi que el ancho base no deberia " +
             "pasar de ~2.1 o las burbujas de clientes vecinos se tocan.")]
    [SerializeField] private Vector2 baseSize = new Vector2(1.95f, 0.9f);
    [Tooltip("Ancho de la expandida. El alto es el maximo: el real sale del contenido, para " +
             "que un pedido corto no deje medio panel vacio.")]
    [SerializeField] private Vector2 expandedSize = new Vector2(3.2f, 2.1f);
    [Tooltip("El panel crece hacia arriba desde el borde inferior, asi la burbuja no se le va " +
             "encima a la parrilla ni tapa las manos del cliente.")]
    [SerializeField] private bool growUpwards = true;

    [Header("Transicion")]
    [SerializeField] private float expandDuration = 0.13f;

    [Header("Colores")]
    [SerializeField] private Color panelColor = new Color(0.97f, 0.94f, 0.86f, 0.97f);
    [SerializeField] private Color borderColor = new Color(0.29f, 0.19f, 0.13f, 1f);
    [SerializeField] private Color titleColor = new Color(0.16f, 0.12f, 0.09f, 1f);
    [SerializeField] private Color detailColor = new Color(0.29f, 0.23f, 0.18f, 1f);
    [Tooltip("Acento del punto de coccion pedido. Es el dato que aparece recien al expandir.")]
    [SerializeField] private Color pointColor = new Color(0.72f, 0.31f, 0.09f, 1f);

    [Header("Orden de dibujo")]
    [Tooltip("Base y expandida usan bandas distintas para que la burbuja abierta pase por " +
             "delante de las de los clientes de al lado. Todo por debajo de los paneles " +
             "deslizantes (90) para que un panel abierto las tape.")]
    [SerializeField] private int baseSortingOrder = 20;
    [SerializeField] private int expandedSortingOrder = 30;

    private Transform contentRoot;
    private SpriteRenderer panelRenderer;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer iconRenderer;
    private TextMeshPro titleText;
    private TextMeshPro detailText;
    private readonly List<SpriteRenderer> extraIcons = new List<SpriteRenderer>();

    private Customer customer;
    private CustomerView view;

    private bool expanded;
    private float expandT;             // 0 = base, 1 = expandida
    private string previewLine;        // linea de preview economico del arrastre
    private bool built;
    private bool hidden;

    private static Sprite panelSprite;
    private static Sprite borderSprite;

    /// <summary>Sprites de los acompaniamientos del pedido, reusando el buffer entre refrescos.</summary>
    private readonly List<Sprite> extraSprites = new List<Sprite>();
    private readonly List<string> extraNames = new List<string>();
    private static readonly StringBuilder textBuilder = new StringBuilder();

    public bool IsExpanded => expanded;

    private void Awake()
    {
        Build();
    }

    // El idioma se puede cambiar desde la pausa con clientes esperando.
    private void OnEnable() => Loc.OnTextsChanged += Refresh;
    private void OnDisable() => Loc.OnTextsChanged -= Refresh;

    /// <summary>Engancha la burbuja al pedido del cliente y la deja en estado base.</summary>
    public void Bind(Customer boundCustomer, CustomerView boundView)
    {
        customer = boundCustomer;
        view = boundView;

        hidden = false;
        expanded = false;
        expandT = 0f;
        previewLine = null;

        Build();
        NormalizeScale();
        Refresh();

        if (contentRoot != null)
            contentRoot.gameObject.SetActive(true);

        ApplyLayout(0f);
    }

    /// <summary>
    /// Igual que <see cref="SetExpanded"/> pero sin animacion. Sirve cuando la burbuja se
    /// rebindea con el mouse ya encima y no corresponde ver el crecimiento.
    /// </summary>
    public void SetExpandedImmediate(bool value, string preview = null)
    {
        SetExpanded(value, preview);
        ApplyLayout(value ? 1f : 0f);
    }

    /// <summary>
    /// Vuelve a leer el pedido. Hay que llamarla cuando el pedido cambia en caliente,
    /// como en el cambio por faltante.
    /// </summary>
    public void Refresh()
    {
        if (!built || customer == null) return;

        Order order = customer.order;

        CollectExtras(order);
        EnsureExtraIconCount(extraSprites.Count);

        if (titleText != null)
            titleText.text = BuildTitle(order);

        if (detailText != null)
            detailText.text = BuildDetail(order);

        ApplyIcon();
        ApplyLayout(expandT);
    }

    /// <summary>
    /// Abre o cierra la burbuja. <paramref name="preview"/> es la linea de preview
    /// economico del arrastre del plato; en null no se muestra nada extra.
    /// </summary>
    public void SetExpanded(bool value, string preview = null)
    {
        if (hidden) return;

        bool previewChanged = preview != previewLine;
        previewLine = preview;

        if (expanded == value && !previewChanged) return;

        expanded = value;

        if (detailText != null && customer != null)
            detailText.text = BuildDetail(customer.order);

        ApplyLayout(expandT);
    }

    /// <summary>Apaga la burbuja entera (durante el feedback manda la burbuja de feedback).</summary>
    public void HideAll()
    {
        hidden = true;
        expanded = false;
        expandT = 0f;
        previewLine = null;

        if (contentRoot != null)
            contentRoot.gameObject.SetActive(false);
    }

    /// <summary>Vuelve a mostrar la burbuja en estado base despues de un <see cref="HideAll"/>.</summary>
    public void ShowBase()
    {
        hidden = false;
        expanded = false;
        previewLine = null;
        expandT = 0f;

        if (contentRoot != null)
            contentRoot.gameObject.SetActive(true);

        Refresh();
    }

    private void LateUpdate()
    {
        if (!built || hidden) return;

        // La burbuja es un hijo del cliente, que viene escalado x2 en el prefab: se
        // normaliza la escala para que todas las medidas de arriba sean unidades de mundo
        // y la burbuja mida lo mismo aunque se reescale al personaje.
        NormalizeScale();

        float target = expanded ? 1f : 0f;

        if (!Mathf.Approximately(expandT, target))
        {
            // Tiempo sin escalar: la burbuja es respuesta al mouse, no algo del reloj del
            // juego. Con Time.deltaTime se quedaba congelada a medio crecer cada vez que el
            // juego pausaba (timeScale 0).
            float step = expandDuration > 0.0001f ? Time.unscaledDeltaTime / expandDuration : 1f;
            expandT = Mathf.MoveTowards(expandT, target, step);
            ApplyLayout(expandT);
        }
    }

    private void NormalizeScale()
    {
        Transform parent = transform.parent;
        if (parent == null) return;

        Vector3 p = parent.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(p.x, 0f) ? 1f : 1f / p.x,
            Mathf.Approximately(p.y, 0f) ? 1f : 1f / p.y,
            Mathf.Approximately(p.z, 0f) ? 1f : 1f / p.z);
    }

    // ── Contenido ────────────────────────────────────────────────────────────

    /// <summary>
    /// Junta los acompaniamientos del pedido. Hoy <c>OrderSystem</c> solo genera toppings,
    /// pero el modelo ya tiene guarniciones: las dos entran por la misma fila de iconos.
    /// </summary>
    private void CollectExtras(Order order)
    {
        extraSprites.Clear();
        extraNames.Clear();

        if (order == null) return;

        if (order.sides != null)
        {
            for (int i = 0; i < order.sides.Count; i++)
            {
                SideSO side = order.sides[i];
                if (side == null) continue;

                extraSprites.Add(side.sideSprite);
                extraNames.Add(side.DisplayName);
            }
        }

        if (order.toppings != null)
        {
            for (int i = 0; i < order.toppings.Count; i++)
            {
                ToppingSO topping = order.toppings[i];
                if (topping == null) continue;

                extraSprites.Add(topping.toppingSprite);
                extraNames.Add(topping.toppingName);
            }
        }
    }

    private string BuildTitle(Order order)
    {
        if (order == null || order.PrimaryCut == null) return Loc.Get("order.title");

        return order.PrimaryCut.cutName;
    }

    /// <summary>
    /// Texto que aparece recien al expandir: punto pedido, formato (pan o plato) y,
    /// durante el arrastre, el preview de lo que se va a cobrar.
    /// </summary>
    private string BuildDetail(Order order)
    {
        if (order == null) return string.Empty;

        textBuilder.Length = 0;

        if (order.PrimaryCut != null)
        {
            string state = "<color=#" + ColorUtility.ToHtmlStringRGB(pointColor) + "><b>"
                           + MeatHoverText.GetStateDisplayName(order.GetRequestedState(0)) + "</b></color>";
            textBuilder.Append(Loc.Format("order.doneness", state));
        }

        textBuilder.Append("\n");
        textBuilder.Append(order.IsSandwich
            ? Loc.Format("order.in_bread", order.bread != null ? order.bread.DisplayName : Loc.Get("order.bread"))
            : Loc.Get("order.plated"));

        if (extraNames.Count > 0)
        {
            textBuilder.Append("\n");

            for (int i = 0; i < extraNames.Count; i++)
            {
                if (i > 0) textBuilder.Append(", ");
                textBuilder.Append(extraNames[i]);
            }
        }

        if (!string.IsNullOrEmpty(previewLine))
        {
            textBuilder.Append("\n");
            textBuilder.Append(previewLine);
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Icono del pedido: el plato en el punto que pidio el cliente, IGUAL en los dos estados.
    /// Antes la base mostraba una version neutra y recien al expandir aparecia el punto, pero
    /// el dibujo cambiaba al agrandar y parecia otro pedido; el dibujo es la identidad del
    /// plato y tiene que ser estable. Lo que agrega la expandida es el punto ESCRITO
    /// ("Punto: Pasado"), que es el dato que de verdad no se puede leer de un icono chico.
    /// La version neutra queda de reserva para los cortes que todavia no tienen sprite por punto.
    /// </summary>
    private void ApplyIcon()
    {
        if (iconRenderer == null) return;

        Sprite sprite = view != null ? view.GetDishSprite() : null;

        if (sprite == null)
            sprite = GetNeutralOrderSprite();

        iconRenderer.sprite = sprite;
        iconRenderer.enabled = sprite != null;
    }

    private Sprite GetNeutralOrderSprite()
    {
        Order order = customer?.order;
        if (order == null) return null;

        if (view != null && view.System != null && view.System.Catalog != null)
        {
            ProductVariantSO variant = view.System.Catalog.GetVariantForOrder(order);
            if (variant != null && variant.variantSprite != null)
                return variant.variantSprite;
        }

        MeatCutSO cut = order.PrimaryCut;
        return cut != null ? cut.GetDefaultSprite() : null;
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Alto de la expandida segun lo que hay para mostrar. Un pedido al plato sin
    /// acompaniamientos no necesita el mismo panel que un sandwich con dos salsas y el
    /// preview del arrastre, y un panel fijo le dejaria medio cuerpo vacio.
    /// </summary>
    private Vector2 ComputeExpandedSize()
    {
        int lines = 2; // punto + formato (al plato / en pan)

        if (extraNames.Count > 0)
            lines++;

        if (!string.IsNullOrEmpty(previewLine))
        {
            lines++;

            for (int i = 0; i < previewLine.Length; i++)
                if (previewLine[i] == '\n') lines++;
        }

        // Sin reserva al pie: en la expandida los iconos ya se fueron.
        float height = 0.56f + lines * 0.24f + 0.14f;

        return new Vector2(
            expandedSize.x,
            Mathf.Clamp(height, baseSize.y + 0.25f, expandedSize.y));
    }

    /// <summary>
    /// Coloca todo para un t de 0 (base) a 1 (expandida). Es una sola funcion para los dos
    /// estados asi la transicion es el mismo panel creciendo y no dos burbujas distintas.
    /// </summary>
    private void ApplyLayout(float t)
    {
        if (!built) return;

        expandT = t;
        float eased = t * t * (3f - 2f * t);

        ApplyIcon();

        Vector2 size = Vector2.Lerp(baseSize, ComputeExpandedSize(), eased);

        // El panel crece hacia arriba: se mantiene fijo el borde de abajo.
        float centerY = growUpwards ? (size.y - baseSize.y) * 0.5f : 0f;
        Vector3 center = chestOffset + new Vector3(0f, centerY, 0f);

        contentRoot.localPosition = center;

        // Apenas empieza a abrirse pasa a la banda alta: mientras crece ya tiene que estar
        // por delante de las burbujas base de los clientes vecinos.
        int order = eased > 0.01f ? expandedSortingOrder : baseSortingOrder;

        if (borderRenderer != null)
        {
            borderRenderer.size = size + new Vector2(0.09f, 0.09f);
            borderRenderer.color = borderColor;
            borderRenderer.sortingOrder = order;
        }

        if (panelRenderer != null)
        {
            panelRenderer.size = size;
            panelRenderer.color = panelColor;
            panelRenderer.sortingOrder = order + 1;
        }

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // Icono: chico y centrado a la izquierda en base, mas grande y arriba al expandir.
        float iconSize = Mathf.Lerp(0.48f, 0.78f, eased);
        float iconX = -halfW + 0.12f + iconSize * 0.5f;
        float iconY = Mathf.Lerp(0f, halfH - 0.14f - iconSize * 0.5f, eased);

        if (iconRenderer != null)
        {
            FitSprite(iconRenderer.transform, iconRenderer.sprite, iconSize, new Vector3(iconX, iconY, -0.01f));
            iconRenderer.sortingOrder = order + 2;
        }

        // Varios cortes todavia no tienen sprite: sin icono el texto se corre a la izquierda
        // en vez de dejar el hueco donde iria el dibujo.
        bool hasIcon = iconRenderer != null && iconRenderer.sprite != null;

        float textLeft = hasIcon ? iconX + iconSize * 0.5f + 0.1f : -halfW + 0.16f;
        float textWidth = halfW - 0.1f - textLeft;

        if (titleText != null)
        {
            titleText.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.2f, textWidth), Mathf.Lerp(0.42f, 0.4f, eased));
            titleText.transform.localPosition = new Vector3(
                textLeft + textWidth * 0.5f,
                Mathf.Lerp(halfH - 0.28f, halfH - 0.26f, eased),
                -0.02f);
            titleText.color = titleColor;
            titleText.sortingOrder = order + 3;
        }

        if (detailText != null)
        {
            bool showDetail = eased > 0.02f;
            detailText.gameObject.SetActive(showDetail);

            if (showDetail)
            {
                // Del pie del titulo al borde de abajo.
                float top = halfH - 0.46f;
                float bottom = -halfH + 0.12f;
                float height = Mathf.Max(0.2f, top - bottom);

                detailText.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.2f, textWidth), height);
                detailText.transform.localPosition = new Vector3(
                    textLeft + textWidth * 0.5f,
                    (top + bottom) * 0.5f,
                    -0.02f);
                detailText.color = detailColor;
                detailText.alpha = Mathf.InverseLerp(0.25f, 1f, eased);
                detailText.sortingOrder = order + 3;
            }
        }

        LayoutExtras(eased, size, order, textLeft);
    }

    /// <summary>
    /// Acompaniamientos: fila de iconos en la base, que se desvanece al expandir porque ahi
    /// los mismos acompaniamientos ya aparecen escritos. La burbuja chica cuenta con dibujos
    /// (no entra texto) y la grande con palabras; repetir las dos cosas dejaba media burbuja
    /// vacia al pie.
    /// </summary>
    private void LayoutExtras(float eased, Vector2 size, int order, float textLeft)
    {
        int count = extraSprites.Count;
        if (count == 0) return;

        float halfH = size.y * 0.5f;
        float alpha = 1f - Mathf.InverseLerp(0.05f, 0.55f, eased);

        const float iconSize = 0.3f;

        for (int i = 0; i < extraIcons.Count; i++)
        {
            SpriteRenderer sr = extraIcons[i];

            if (i >= count || extraSprites[i] == null || alpha <= 0.001f)
            {
                sr.enabled = false;
                continue;
            }

            Sprite sprite = extraSprites[i];
            sr.sprite = sprite;
            sr.enabled = true;
            sr.color = new Color(1f, 1f, 1f, alpha);
            sr.sortingOrder = order + 2;

            FitSprite(
                sr.transform,
                sprite,
                iconSize,
                new Vector3(
                    // El paso contempla el ancho maximo de FitSprite, no el nominal.
                    textLeft + iconSize * 0.65f + i * (iconSize * 1.3f + 0.08f),
                    -halfH + 0.14f + iconSize * 0.5f,
                    -0.01f));
        }
    }

    /// <summary>
    /// Escala y recentra un sprite para que entre en un cuadro de <paramref name="target"/>
    /// sin deformarse. Los sprites del proyecto tienen tamanios y pivots distintos, asi que
    /// se corrige por el centro real de los bounds.
    /// </summary>
    private static void FitSprite(Transform t, Sprite sprite, float target, Vector3 position)
    {
        if (sprite == null)
        {
            t.localPosition = position;
            return;
        }

        // Se ajusta por alto y no por el lado mayor: los iconos de salsas y guarniciones
        // vienen en lienzos de proporciones distintas (la criolla es 2048x1266 y el
        // chimichurri es cuadrado), y normalizar por el lado mayor hacia que los anchos
        // se vieran la mitad de chicos que el resto de la fila. El ancho se limita para
        // que un sprite muy apaisado no se le meta encima al de al lado.
        Vector3 size = sprite.bounds.size;
        float scale = size.y > 0.0001f ? target / size.y : 1f;

        const float maxWidthRatio = 1.3f;
        if (size.x * scale > target * maxWidthRatio)
            scale = target * maxWidthRatio / size.x;

        t.localScale = new Vector3(scale, scale, 1f);

        Vector3 center = sprite.bounds.center * scale;
        t.localPosition = position - new Vector3(center.x, center.y, 0f);
    }

    // ── Construccion de la jerarquia ─────────────────────────────────────────

    private void Build()
    {
        if (built) return;

        EnsureSprites();

        var rootGo = new GameObject("OrderBubbleContent");
        rootGo.transform.SetParent(transform, false);
        contentRoot = rootGo.transform;

        borderRenderer = CreatePanel("Border", borderSprite, borderColor);
        panelRenderer = CreatePanel("Panel", panelSprite, panelColor);

        var iconGo = new GameObject("OrderIcon");
        iconGo.transform.SetParent(contentRoot, false);
        iconRenderer = iconGo.AddComponent<SpriteRenderer>();

        titleText = CreateText("Title", 2.6f, FontStyles.Bold, TextAlignmentOptions.Left);
        // El nombre del corte va siempre en un renglon: con wrap, "Tira de asado" se parte
        // en dos y el segundo renglon se le monta a la fila de iconos.
        titleText.enableWordWrapping = false;

        detailText = CreateText("Detail", 1.9f, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        built = true;
    }

    private SpriteRenderer CreatePanel(string panelName, Sprite sprite, Color color)
    {
        var go = new GameObject(panelName);
        go.transform.SetParent(contentRoot, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;   // el panel crece sin deformar las esquinas
        sr.size = baseSize;

        return sr;
    }

    private TextMeshPro CreateText(string textName, float fontSize, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(textName);
        go.transform.SetParent(contentRoot, false);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1.1f;
        tmp.fontSizeMax = fontSize;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;

        return tmp;
    }

    private void EnsureExtraIconCount(int count)
    {
        while (extraIcons.Count < count)
        {
            var go = new GameObject("Extra" + extraIcons.Count);
            go.transform.SetParent(contentRoot, false);
            extraIcons.Add(go.AddComponent<SpriteRenderer>());
        }

        for (int i = count; i < extraIcons.Count; i++)
            extraIcons[i].enabled = false;
    }

    /// <summary>
    /// Panel redondeado 9-sliced generado por codigo, para no depender de un asset:
    /// el borde es el mismo cuadro con las esquinas un poco mas grandes por detras.
    /// </summary>
    private static void EnsureSprites()
    {
        if (panelSprite != null && borderSprite != null) return;

        const int size = 64;
        const int radius = 16;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = (x < radius) ? radius : ((x >= size - radius) ? size - radius - 1 : x);
                int cy = (y < radius) ? radius : ((y >= size - radius) ? size - radius - 1 : y);

                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();

        var rect = new Rect(0, 0, size, size);
        var pivot = new Vector2(0.5f, 0.5f);
        var border = new Vector4(radius, radius, radius, radius);

        panelSprite = Sprite.Create(tex, rect, pivot, 100f, 0, SpriteMeshType.FullRect, border);
        borderSprite = panelSprite;
    }
}
