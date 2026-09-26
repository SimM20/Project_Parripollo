using UnityEngine;

/// <summary>
/// Barra de paciencia armada en runtime sobre los sprites del prefab (Fondo + Completo): los
/// pasa a una pastilla redondeada, le suma contorno y brillo, y la anima (entrada, vaciado desde
/// la izquierda, golpe al cruzar umbrales, latido y temblor cuando queda poca, salida).
/// Todos los hijos quedan al mismo Z que el fill: el orden lo resuelve el sortingOrder (ver
/// el comentario de <see cref="CustomerView"/> sobre la camara en perspectiva).
/// </summary>
public class PatienceBar
{
    public Color highColor, midColor, lowColor;
    public float urgentThreshold, shakeAmplitude, shakeFrequency;

    const float IntroDuration = 0.45f;
    const float HideDuration = 0.18f;

    static Sprite pill;

    readonly Transform root, fill, shine;
    readonly SpriteRenderer fillSr, shineSr;
    readonly Vector3 rootBasePos, rootBaseScale;
    readonly float fullW, fillH, fillLeft, shineH;

    float display, lastP = -1f;
    float introT = 1f, hideT = -1f;
    float punch, flash;

    public PatienceBar(Transform fillTransform, float fillFullX, Color trackColor, Color outlineColor)
    {
        fill = fillTransform;
        root = fill.parent;
        fillSr = fill.GetComponent<SpriteRenderer>();
        rootBasePos = root.localPosition;
        rootBaseScale = root.localScale;

        Vector3 fillPos = fill.localPosition;
        fullW = fillFullX;
        fillH = fill.localScale.y;
        fillLeft = fillPos.x - fullW * 0.5f;
        MakePill(fillSr, fillH);

        SpriteRenderer track = null;
        foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr != fillSr) { track = sr; break; }

        int baseOrder = fillSr.sortingOrder;
        if (track != null)
        {
            Vector2 trackSize = track.transform.localScale;
            MakePill(track, trackSize.y, trackSize.x);
            track.sortingOrder = baseOrder - 1;

            float pad = trackSize.y * 0.35f;
            var outline = NewPiece("Contorno", baseOrder - 2, track.transform.localPosition);
            MakePill(outline, trackSize.y + pad, trackSize.x + pad);
            outline.color = outlineColor;
            track.color = trackColor;
        }

        shineH = fillH * 0.35f;
        shineSr = NewPiece("Brillo", baseOrder + 1, fillPos + new Vector3(0f, fillH * 0.18f, 0f));
        shineSr.transform.localScale = new Vector3(shineH, shineH, 1f);
        shineSr.color = new Color(1f, 1f, 1f, 0.35f);
        shine = shineSr.transform;
    }

    /// <summary>Aparece con rebote y el fill se carga de 0 a la paciencia actual.</summary>
    public void Show()
    {
        root.gameObject.SetActive(true);
        introT = 0f;
        hideT = -1f;
        display = 0f;
        lastP = -1f;
        punch = flash = 0f;
    }

    /// <summary>Se encoge y se apaga. Llamar al entrar en feedback.</summary>
    public void Hide()
    {
        if (root.gameObject.activeSelf && hideT < 0f)
            hideT = 0f;
    }

    /// <summary>Avanza la animacion. Devuelve true el frame en que la paciencia entra en zona urgente.</summary>
    public bool Tick(float p)
    {
        if (!root.gameObject.activeSelf) return false;

        float dt = Time.deltaTime;
        float scale = 1f;
        bool enteredUrgent = false;

        if (hideT >= 0f)
        {
            hideT += dt / HideDuration;
            if (hideT >= 1f)
            {
                root.localScale = rootBaseScale;
                root.localPosition = rootBasePos;
                root.gameObject.SetActive(false);
                return false;
            }
            scale *= 1f - hideT * hideT;
        }

        if (introT < 1f)
        {
            introT = Mathf.Min(1f, introT + dt / IntroDuration);
            scale *= BackOut(introT);
            display = p * (1f - Mathf.Pow(1f - introT, 3f));
        }
        else
        {
            display = Mathf.Lerp(display, p, 1f - Mathf.Exp(-12f * dt));

            // Golpe al cruzar la mitad y el umbral urgente hacia abajo.
            if (lastP >= 0f && hideT < 0f)
            {
                if (Crossed(lastP, p, 0.5f)) punch = flash = 1f;
                if (Crossed(lastP, p, urgentThreshold)) { punch = flash = 1f; enteredUrgent = true; }
            }
        }
        lastP = p;

        punch = Mathf.MoveTowards(punch, 0f, dt * 4f);
        flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
        scale *= 1f + 0.3f * punch * punch;

        // Urgencia: 0 fuera de zona, sube de 0.35 a 1 mientras se vacia.
        float urgency = p > 0f && p < urgentThreshold
            ? Mathf.Lerp(1f, 0.35f, p / Mathf.Max(0.0001f, urgentThreshold))
            : 0f;
        float beatSpeed = Mathf.Lerp(6f, 16f, urgency);
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * beatSpeed);
        scale *= 1f + 0.08f * urgency * beat;

        root.localScale = rootBaseScale * scale;

        // Temblor del contenedor (no del cliente: asi el collider no se corre bajo el mouse).
        Vector3 offset = Vector3.zero;
        if (urgency > 0f)
        {
            float t = Time.time * shakeFrequency;
            float amp = shakeAmplitude * (0.5f + urgency);
            offset = new Vector3(Mathf.Sin(t) * amp, Mathf.Cos(t * 1.7f) * amp * 0.5f, 0f);
        }
        root.localPosition = rootBasePos + offset;

        // Fill anclado a la izquierda.
        float w = fullW * display;
        bool visible = w > 0.001f;
        fillSr.enabled = visible;
        if (visible)
        {
            fillSr.size = new Vector2(w / fillH, 1f);
            var fp = fill.localPosition;
            fp.x = fillLeft + w * 0.5f;
            fill.localPosition = fp;
        }

        float shineW = w - fillH * 0.5f;
        shineSr.enabled = shineW > shineH;
        if (shineSr.enabled)
        {
            shineSr.size = new Vector2(shineW / shineH, 1f);
            var sp = shine.localPosition;
            sp.x = fillLeft + fillH * 0.25f + shineW * 0.5f;
            shine.localPosition = sp;
        }

        // Verde → amarillo en la mitad superior, amarillo → rojo en la inferior; destello blanco
        // al cruzar umbrales y parpadeo en zona urgente.
        Color c = display >= 0.5f
            ? Color.Lerp(midColor, highColor, (display - 0.5f) * 2f)
            : Color.Lerp(lowColor, midColor, display * 2f);
        c = Color.Lerp(c, Color.white, Mathf.Max(flash * 0.8f, urgency * 0.45f * beat));
        fillSr.color = c;

        return enteredUrgent;
    }

    static bool Crossed(float from, float to, float threshold) => from >= threshold && to < threshold;

    static float BackOut(float t)
    {
        const float s = 1.70158f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    SpriteRenderer NewPiece(string name, int order, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Pill;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingLayerID = fillSr.sortingLayerID;
        sr.sortingOrder = order;
        return sr;
    }

    /// <summary>
    /// Pasa el renderer a pastilla sliced: escala uniforme = alto (asi las puntas son siempre
    /// medio circulo) y el ancho va por size en unidades de alto.
    /// </summary>
    static void MakePill(SpriteRenderer sr, float height, float width = -1f)
    {
        sr.sprite = Pill;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.transform.localScale = new Vector3(height, height, 1f);
        if (width > 0f)
            sr.size = new Vector2(width / height, 1f);
    }

    /// <summary>Rectangulo redondeado blanco de 64px con bordes de 30px para 9-slice, 1 unidad de alto.</summary>
    static Sprite Pill
    {
        get
        {
            if (pill != null) return pill;

            const int size = 64;
            const float radius = 30f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PatiencePill"
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // SDF de rectangulo redondeado, con 1px de antialias.
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            pill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            pill.name = "PatiencePill";
            return pill;
        }
    }
}
