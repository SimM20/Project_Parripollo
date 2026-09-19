using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estilo del riel de tickets. Serializado en CustomerSystem (escena) para ajustarlo sin código.
/// </summary>
[System.Serializable]
public class OrderTicketStyle
{
    [Header("Riel")]
    [Tooltip("Anclaje del riel en pantalla (0,0 abajo-izquierda · 1,1 arriba-derecha).")]
    public Vector2 railAnchor = new Vector2(0.5f, 1f);
    [Tooltip("Desplazamiento en píxeles (resolución de referencia 1920x1080) desde el anclaje.")]
    public Vector2 railOffset = new Vector2(0f, -14f);
    public float ticketSpacing = 12f;
    public int sortingOrder = 0;

    [Header("Ticket")]
    public Vector2 ticketSize = new Vector2(170f, 96f);
    public Color paperColor = new Color(0.98f, 0.95f, 0.85f);
    public Color inkColor = new Color(0.16f, 0.12f, 0.08f);
    public Color accentColor = new Color(0.72f, 0.16f, 0.12f);
    [Tooltip("Rotación máxima (grados) de cada ticket, alternando signo, para que no queden perfectos.")]
    public float maxTilt = 2.5f;
    public TMP_FontAsset font;                 // null → TMP_Settings.defaultFontAsset
    public float titleFontSize = 22f;
    public float bodyFontSize = 16f;

    [Header("Pin")]
    public float pinDiameter = 14f;
    public Color pinColor = new Color(0.85f, 0.2f, 0.15f);
    [Tooltip("Con la paciencia por debajo de este valor el pin parpadea.")]
    [Range(0f, 1f)] public float pinUrgentPatience = 0.2f;
}

/// <summary>
/// Riel de tickets de cocina: un ticket por cliente activo (no en feedback), ordenados por slot
/// de izquierda a derecha para que mapeen con los clientes en escena. Cada ticket muestra corte,
/// punto, sándwich/plato y acompañamientos. Se crea en runtime (Create) sobre un Canvas propio
/// screen-space; sin setup de escena. Reconstruye cuando cambia la lista de clientes o algún
/// pedido (TriggerMissingCutChange cambia el corte del pedido en caliente).
/// </summary>
public class OrderTicketBoard : MonoBehaviour
{
    private CustomerSystem customers;
    private OrderTicketStyle style;
    private Canvas canvas;
    private RectTransform rail;
    private Sprite circleSprite;

    private readonly List<Ticket> tickets = new List<Ticket>();
    private readonly List<Customer> lastCustomers = new List<Customer>();
    private readonly List<string> lastSignatures = new List<string>();

    private class Ticket
    {
        public Customer customer;
        public RectTransform root;
        public Image pin;
    }

    public static OrderTicketBoard Create(CustomerSystem customers, OrderTicketStyle style)
    {
        if (customers == null) return null;

        var go = new GameObject("OrderTicketBoard");
        var board = go.AddComponent<OrderTicketBoard>();
        board.customers = customers;
        board.style = style ?? new OrderTicketStyle();
        board.Build();
        return board;
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = style.sortingOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Sin GraphicRaycaster: los tickets son informativos y no deben robar clicks.

        var railGo = new GameObject("Rail", typeof(RectTransform));
        railGo.transform.SetParent(transform, false);
        rail = railGo.GetComponent<RectTransform>();
        rail.anchorMin = style.railAnchor;
        rail.anchorMax = style.railAnchor;
        rail.pivot = style.railAnchor;
        rail.anchoredPosition = style.railOffset;
        rail.sizeDelta = Vector2.zero;

        circleSprite = MakeCircleSprite(32);
    }

    void Update()
    {
        // Ocultos en pausa: el menú de pausa tiene su propio canvas y no queremos tapar nada.
        bool visible = !GamePause.IsPaused;
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;

        if (NeedsRebuild())
            Rebuild();

        RefreshPins();
    }

    private bool NeedsRebuild()
    {
        var active = customers.ActiveCustomers;
        int n = 0;

        for (int i = 0; i < active.Count; i++)
        {
            Customer c = active[i];
            if (c == null || c.IsInFeedback) continue;

            if (n >= lastCustomers.Count || lastCustomers[n] != c || lastSignatures[n] != Signature(c))
                return true;
            n++;
        }

        return n != lastCustomers.Count;
    }

    private void Rebuild()
    {
        for (int i = 0; i < tickets.Count; i++)
        {
            if (tickets[i].root != null)
                Destroy(tickets[i].root.gameObject);
        }
        tickets.Clear();
        lastCustomers.Clear();
        lastSignatures.Clear();

        var ordered = new List<Customer>();
        var active = customers.ActiveCustomers;
        for (int i = 0; i < active.Count; i++)
        {
            Customer c = active[i];
            if (c != null && !c.IsInFeedback) ordered.Add(c);
        }
        ordered.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));

        float totalWidth = ordered.Count * style.ticketSize.x + Mathf.Max(0, ordered.Count - 1) * style.ticketSpacing;
        float x = -totalWidth * 0.5f + style.ticketSize.x * 0.5f;

        for (int i = 0; i < ordered.Count; i++)
        {
            Customer c = ordered[i];
            Ticket t = MakeTicket(c, i);
            t.root.anchoredPosition = new Vector2(x, 0f);
            x += style.ticketSize.x + style.ticketSpacing;

            tickets.Add(t);
            lastCustomers.Add(c);
            lastSignatures.Add(Signature(c));
        }
    }

    private Ticket MakeTicket(Customer c, int index)
    {
        var go = new GameObject("Ticket", typeof(RectTransform));
        go.transform.SetParent(rail, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = style.ticketSize;
        float tilt = (index % 2 == 0 ? 1f : -1f) * style.maxTilt * (0.5f + 0.5f * ((index * 7) % 3) / 2f);
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

        var paper = go.AddComponent<Image>();
        paper.color = style.paperColor;
        paper.raycastTarget = false;

        // Franja de color arriba (como el borde perforado de un ticket).
        var stripe = MakeImage("Stripe", rt, null, style.accentColor);
        var srt = stripe.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.sizeDelta = new Vector2(0f, 5f);
        srt.anchoredPosition = Vector2.zero;

        // Pin
        var pin = MakeImage("Pin", rt, circleSprite, style.pinColor);
        var prt = pin.rectTransform;
        prt.anchorMin = new Vector2(0.5f, 1f);
        prt.anchorMax = new Vector2(0.5f, 1f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(style.pinDiameter, style.pinDiameter);
        prt.anchoredPosition = new Vector2(0f, 1f);

        // Texto
        TMP_FontAsset font = style.font != null ? style.font : TMP_Settings.defaultFontAsset;

        var title = MakeText("Title", rt, font, style.titleFontSize, style.inkColor, FontStyles.Bold);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(8f, 0f);
        trt.offsetMax = new Vector2(-8f, -10f);
        trt.sizeDelta = new Vector2(trt.sizeDelta.x, style.titleFontSize + 8f);
        title.text = c.order?.PrimaryCut != null ? c.order.PrimaryCut.cutName : "Sin corte";

        var body = MakeText("Body", rt, font, style.bodyFontSize, style.inkColor, FontStyles.Normal);
        var brt = body.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.offsetMin = new Vector2(8f, 6f);
        brt.offsetMax = new Vector2(-8f, -(10f + style.titleFontSize + 8f));
        body.text = BuildBody(c.order);

        return new Ticket { customer = c, root = rt, pin = pin };
    }

    private static string BuildBody(Order order)
    {
        if (order == null) return "";

        var sb = new StringBuilder();

        if (order.PrimaryCut != null)
            sb.Append(MeatHoverText.GetStateDisplayName(order.GetRequestedState(0)));

        if (order.IsSandwich)
            sb.Append(sb.Length > 0 ? " - " : "").Append(order.bread != null ? order.bread.breadName : "Pan");
        else
            sb.Append(sb.Length > 0 ? " - " : "").Append("Al plato");

        if (order.sides != null && order.sides.Count > 0)
        {
            sb.Append('\n');
            for (int i = 0; i < order.sides.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(order.sides[i] != null ? order.sides[i].sideName : "?");
            }
        }

        return sb.ToString();
    }

    /// <summary>Lo que, si cambia, obliga a redibujar el ticket de ese cliente.</summary>
    private static string Signature(Customer c)
    {
        Order o = c.order;
        if (o == null) return "";
        return (o.PrimaryCut != null ? o.PrimaryCut.name : "-") + "|" + o.GetRequestedState(0) + "|" + o.IsSandwich;
    }

    private void RefreshPins()
    {
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);

        for (int i = 0; i < tickets.Count; i++)
        {
            Ticket t = tickets[i];
            if (t.pin == null || t.customer == null) continue;

            bool urgent = t.customer.Patience01 < style.pinUrgentPatience;
            Color c = style.pinColor;
            if (urgent) c.a = Mathf.Lerp(0.35f, 1f, blink);
            t.pin.color = c;
        }
    }

    // ── helpers de construcción ──────────────────────────────────────────

    private static Image MakeImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI MakeText(string name, RectTransform parent, TMP_FontAsset font, float size, Color color, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = fontStyle;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = size * 0.5f;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                byte a = (byte)(255 * Mathf.Clamp01(r - d));   // borde suave de 1px
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
