using TMPro;
using UnityEngine;

/// <summary>
/// Burbuja de estado de la carne. Aparece al pasar el mouse por una pieza y la sigue mientras
/// se cocina. Singleton de escena: hay una sola y se reengancha a la pieza apuntada.
///
/// Usa el mismo diseño que la burbuja de pedido del cliente (<see cref="CustomerOrderBubble"/>):
/// panel crema con borde marron, el dibujo del corte a la izquierda, el nombre en negrita y
/// abajo el punto ("Punto: Jugoso") con el mismo acento. Asi lo que pide el cliente y lo que
/// tiene la parrilla se leen igual: el icono es el corte en su punto actual, como el del pedido
/// es el plato en el punto pedido.
///
/// Con la pieza en la parrilla la burbuja suma una fila al pie con la barra de coccion
/// (<see cref="MeatCookHoverBar"/>): antes eran dos carteles separados, uno arriba del otro.
///
/// La jerarquia se arma por codigo con <see cref="WorldBubbleStyle"/>. En la escena el GameObject
/// lleva este componente y, como hija, la instancia del prefab de la barra.
/// </summary>
public class MeatHoverBubble : MonoBehaviour
{
    public static MeatHoverBubble Instance { get; private set; }

    [Header("Referencias")]
    [Tooltip("Barra de coccion que va al pie de la burbuja. Si queda vacio se busca entre los hijos.")]
    [SerializeField] private MeatCookHoverBar cookBar;

    [Header("Posicion")]
    [Tooltip("Espacio, en unidades de mundo, entre el borde de arriba de la pieza " +
             "(Meat.VisualTopOffset) y el de abajo de la burbuja.")]
    [SerializeField] private float gapAboveMeat = 0.12f;

    [Header("Tamanio del panel (unidades de mundo)")]
    [Tooltip("Alto de la fila de arriba (icono, nombre y punto). Mismo alto que la burbuja base del cliente.")]
    [SerializeField] private float height = 0.9f;
    [Tooltip("Margen entre la barra de coccion y los bordes del panel (abajo y costados).")]
    [SerializeField] private float barPadding = 0.14f;
    [Tooltip("El ancho sale del texto mas largo (nombre del corte o punto), entre estos limites.")]
    [SerializeField] private float minWidth = 1.6f;
    [SerializeField] private float maxWidth = 2.8f;

    [Header("Transicion")]
    [Tooltip("Segundos del 'pop' al aparecer. En tiempo sin escalar, igual que la del cliente.")]
    [SerializeField] private float popDuration = 0.1f;

    [Header("Colores (los mismos que CustomerOrderBubble)")]
    [SerializeField] private Color panelColor = new Color(0.97f, 0.94f, 0.86f, 0.97f);
    [SerializeField] private Color borderColor = new Color(0.29f, 0.19f, 0.13f, 1f);
    [SerializeField] private Color titleColor = new Color(0.16f, 0.12f, 0.09f, 1f);
    [SerializeField] private Color detailColor = new Color(0.29f, 0.23f, 0.18f, 1f);
    [SerializeField] private Color pointColor = new Color(0.72f, 0.31f, 0.09f, 1f);

    [Header("Orden de dibujo")]
    [Tooltip("Misma capa que la barra de coccion (MeatBubbleOverlay) y por debajo de ella " +
             "(5000-5010), para que la barra se dibuje encima del panel.")]
    [SerializeField] private string sortingLayerName = "MeatBubbleOverlay";
    [SerializeField] private int sortingOrder = 4980;

    private const float IconSize = 0.6f;

    private Transform contentRoot;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer panelRenderer;
    private SpriteRenderer iconRenderer;
    private TextMeshPro titleText;
    private TextMeshPro detailText;
    private bool built;

    private Meat source;
    private MeatStates lastState;
    private bool lastSideA;
    private bool hasIcon;
    private bool barShown;
    private float headerCenterY;
    private float popT = 1f;

    private static readonly MeatStates[] AllStates =
    {
        MeatStates.Crudo, MeatStates.Jugoso, MeatStates.Hecho,
        MeatStates.Muy_Hecho, MeatStates.Pasado, MeatStates.Quemado
    };

    void Awake()
    {
        Instance = this;
        Build();
        Hide();
    }

    // El idioma se puede cambiar desde la pausa con el mouse sobre una pieza.
    void OnEnable() => Loc.OnTextsChanged += Relayout;
    void OnDisable() => Loc.OnTextsChanged -= Relayout;

    void LateUpdate()
    {
        if (source == null)
        {
            Hide();
            return;
        }

        transform.position = source.transform.position + GetOffset(source);

        // La fila de la barra va solo con la pieza en la parrilla.
        if (source.IsOnGrill != barShown)
            Relayout();

        // Refresco en vivo: punto e icono cambian solo cuando cambia la cara activa o su estado.
        if (source.ActiveSideState != lastState || source.IsSideAActive != lastSideA)
            RefreshContent();

        if (popT < 1f)
        {
            popT = popDuration > 0.0001f ? Mathf.MoveTowards(popT, 1f, Time.unscaledDeltaTime / popDuration) : 1f;
            ApplyPop();
        }
    }

    /// <summary>Muestra la burbuja siguiendo a la pieza y actualizando el estado mientras se cocina.</summary>
    public void Show(Meat meat)
    {
        if (meat == null) return;

        bool wasShowingIt = source == meat && gameObject.activeSelf;

        source = meat;
        gameObject.SetActive(true);
        Relayout();

        if (!wasShowingIt)
        {
            popT = 0f;
            ApplyPop();
        }

        transform.position = meat.transform.position + GetOffset(meat);
    }

    public void Hide()
    {
        source = null;
        barShown = false;

        if (cookBar != null)
            cookBar.Hide();

        gameObject.SetActive(false);
    }

    /// <summary>Oculta solo si la burbuja esta mostrando esta pieza.</summary>
    public void HideIfTarget(Meat meat)
    {
        if (source == meat)
            Hide();
    }

    /// <summary>
    /// Rectangulo del panel en el mundo, si la burbuja esta mostrando la pieza de
    /// <paramref name="target"/>. Los carteles del tutorial que apuntan a esa carne lo suman al
    /// de la pieza para ponerse por fuera: son UI de pantalla y si no, la tapan.
    /// </summary>
    public bool TryGetPanelBounds(Transform target, out Bounds bounds)
    {
        bounds = default;
        if (!built || source == null || target != source.transform || !gameObject.activeInHierarchy)
            return false;

        bounds = borderRenderer.bounds;
        return true;
    }

    private Vector3 GetOffset(Meat meat)
    {
        // Z en 0: la camara es perspectiva y separar la burbuja en Z la desalinea de la pieza.
        return new Vector3(0f, meat.VisualTopOffset + gapAboveMeat, 0f);
    }

    // ── Contenido ────────────────────────────────────────────────────────────

    /// <summary>
    /// Recalcula el ancho para la pieza actual y refresca el contenido. El ancho contempla el
    /// punto mas largo de todos, asi la burbuja no cambia de tamaño mientras la carne se cocina.
    /// </summary>
    private void Relayout()
    {
        if (!built || source == null) return;

        hasIcon = GetIconSprite() != null;
        titleText.text = GetTitle();

        barShown = source.IsOnGrill && cookBar != null;
        Bounds barBounds = barShown ? cookBar.GetLocalBounds() : default;

        float textWidth = titleText.GetPreferredValues(titleText.text).x;
        for (int i = 0; i < AllStates.Length; i++)
            textWidth = Mathf.Max(textWidth, detailText.GetPreferredValues(BuildDetail(AllStates[i])).x);

        float width = Mathf.Clamp(TextLeftFromEdge() + textWidth + 0.18f, minWidth, maxWidth);
        float panelHeight = height;

        if (barShown)
        {
            // La barra entra entera: el panel se ensancha si hace falta y suma una fila al pie.
            width = Mathf.Max(width, barBounds.size.x + barPadding * 2f);
            panelHeight += barBounds.size.y + barPadding;
        }

        ApplyLayout(new Vector2(width, panelHeight));

        if (barShown)
        {
            float barCenterY = -panelHeight * 0.5f + barPadding + barBounds.size.y * 0.5f;
            cookBar.SetLayoutPosition(new Vector3(-barBounds.center.x, barCenterY - barBounds.center.y, 0f));
            cookBar.Show(source);
        }
        else if (cookBar != null)
        {
            cookBar.Hide();
        }

        RefreshContent();
    }

    private void RefreshContent()
    {
        if (!built || source == null) return;

        lastState = source.ActiveSideState;
        lastSideA = source.IsSideAActive;

        Sprite sprite = GetIconSprite();

        // Si el icono aparece o desaparece cambia el lugar del texto: se rearma todo.
        if ((sprite != null) != hasIcon)
        {
            Relayout();
            return;
        }

        detailText.text = BuildDetail(lastState);

        iconRenderer.sprite = sprite;
        iconRenderer.enabled = sprite != null;
        iconRenderer.flipX = !lastSideA;   // igual que la pieza en la parrilla

        float halfW = panelRenderer.size.x * 0.5f;
        WorldBubbleStyle.FitSprite(iconRenderer.transform, sprite, IconSize,
            new Vector3(-halfW + 0.12f + IconSize * 0.5f, headerCenterY, 0f));
    }

    /// <summary>El corte en el punto y la cara que se estan viendo en la parrilla.</summary>
    private Sprite GetIconSprite()
    {
        if (source == null || source.cut == null) return null;
        return source.cut.GetSpriteForState(source.ActiveSideState, source.IsSideAActive);
    }

    /// <summary>
    /// Varios cortes todavia no tienen sprite: sin icono el texto se corre a la izquierda en vez
    /// de dejar el hueco donde iria el dibujo (igual que en la burbuja del cliente).
    /// </summary>
    private float TextLeftFromEdge()
    {
        return hasIcon ? 0.12f + IconSize + 0.1f : 0.16f;
    }

    private string GetTitle()
    {
        return source != null && source.cut != null ? source.cut.cutName : Loc.Get("meat.generic");
    }

    private string BuildDetail(MeatStates state)
    {
        string point = "<color=#" + ColorUtility.ToHtmlStringRGB(pointColor) + "><b>"
                       + MeatHoverText.GetStateDisplayName(state) + "</b></color>";
        return Loc.Format("order.doneness", point);
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void ApplyLayout(Vector2 size)
    {
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // El borde de abajo queda en el origen: el pop y el offset se miden desde ahi.
        contentRoot.localPosition = new Vector3(0f, halfH, 0f);

        borderRenderer.size = size + new Vector2(0.09f, 0.09f);
        borderRenderer.color = borderColor;
        panelRenderer.size = size;
        panelRenderer.color = panelColor;

        // La fila de arriba (icono, nombre y punto) se mide desde el borde de arriba, asi no se
        // mueve cuando se suma la fila de la barra.
        float top = halfH;
        headerCenterY = top - height * 0.5f;

        float textLeft = -halfW + TextLeftFromEdge();
        float textWidth = Mathf.Max(0.2f, halfW - 0.1f - textLeft);

        titleText.rectTransform.sizeDelta = new Vector2(textWidth, 0.42f);
        titleText.transform.localPosition = new Vector3(textLeft + textWidth * 0.5f, top - 0.28f, 0f);
        titleText.color = titleColor;

        detailText.rectTransform.sizeDelta = new Vector2(textWidth, 0.34f);
        detailText.transform.localPosition = new Vector3(textLeft + textWidth * 0.5f, top - height + 0.27f, 0f);
        detailText.color = detailColor;
    }

    private void ApplyPop()
    {
        // Crece desde el borde de abajo, con un leve sobrepaso al final.
        float t = popT;
        float scale = t >= 1f ? 1f : Mathf.LerpUnclamped(0.8f, 1f, 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f));
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    // ── Construccion de la jerarquia ─────────────────────────────────────────

    private void Build()
    {
        if (built) return;

        var rootGo = new GameObject("MeatBubbleContent");
        rootGo.transform.SetParent(transform, false);
        contentRoot = rootGo.transform;

        Vector2 size = new Vector2(minWidth, height);
        int layerId = SortingLayer.NameToID(sortingLayerName);

        // Todo en el mismo Z (camara perspectiva): el orden lo resuelve sortingOrder.
        borderRenderer = WorldBubbleStyle.CreatePanel(contentRoot, "Border", borderColor, size);
        panelRenderer = WorldBubbleStyle.CreatePanel(contentRoot, "Panel", panelColor, size);

        var iconGo = new GameObject("MeatIcon");
        iconGo.transform.SetParent(contentRoot, false);
        iconRenderer = iconGo.AddComponent<SpriteRenderer>();

        titleText = WorldBubbleStyle.CreateText(contentRoot, "Title", 2.6f, FontStyles.Bold, TextAlignmentOptions.Left);
        titleText.enableWordWrapping = false;

        detailText = WorldBubbleStyle.CreateText(contentRoot, "Detail", 1.9f, FontStyles.Normal, TextAlignmentOptions.Left);
        detailText.enableWordWrapping = false;

        SetSorting(borderRenderer, layerId, sortingOrder);
        SetSorting(panelRenderer, layerId, sortingOrder + 1);
        SetSorting(iconRenderer, layerId, sortingOrder + 2);

        titleText.sortingLayerID = layerId;
        titleText.sortingOrder = sortingOrder + 3;
        detailText.sortingLayerID = layerId;
        detailText.sortingOrder = sortingOrder + 3;

        // La barra (hija en la escena) pasa adentro del contenido para seguir al panel y al pop.
        if (cookBar == null)
            cookBar = GetComponentInChildren<MeatCookHoverBar>(true);

        if (cookBar != null)
            cookBar.transform.SetParent(contentRoot, false);

        ApplyLayout(size);
        built = true;
    }

    private static void SetSorting(SpriteRenderer sr, int layerId, int order)
    {
        sr.sortingLayerID = layerId;
        sr.sortingOrder = order;
    }
}
