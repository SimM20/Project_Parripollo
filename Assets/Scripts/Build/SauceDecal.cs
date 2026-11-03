using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La salsa que cayó en el plato, pintada en pixel art sobre una grilla que cubre la loza.
/// Es un campo de alturas: el chorro deposita salsa y la salsa se escurre mientras la pendiente
/// supere su espesor, como una salsa espesa. Vertiendo quieto se junta un charco que crece, y
/// moviendo el frasco queda un cordón más fino cuanto más rápido. La salsa no pasa el borde de
/// la loza. Lo mojado no se seca (los bordes no titilan).
/// Se dibuja con luz de arriba a la izquierda, como los sprites de salpicaduras: borde de arriba
/// claro, panza y contorno oscuros abajo y a la derecha, una sombrita sobre el plato, brillos
/// cortos y trocitos fijos por celda. Cuando la salsa se asienta se duerme (sin Update).
/// </summary>
public class SauceDecal : MonoBehaviour
{
    // Desde esta altura la celda cuenta como salsa; abajo es salsa que se está escurriendo.
    private const float CoverHeight = 1f;
    // Cuánto varía esa altura de una zona a otra: el borde sale ondulado y no un óvalo perfecto.
    private const float EdgeNoise = 0.6f;
    private const int EdgeNoiseCells = 5;
    // Parte del desnivel sobrante que pasa a la celda vecina (0.5 = la deja justo en el límite).
    private const float FlowRate = 0.45f;
    private const int RelaxIterations = 10;
    // Después del último aporte la salsa se acomoda un rato más; después el charco se congela.
    private const float SettleSeconds = 1f;
    // El caudal está pensado para este pixel: con otro tamaño, el charco mide lo mismo en mundo.
    private const float ReferencePixelSize = 0.02f;
    private const int BorderCells = 3;
    // Más alto que esto el charco es plano: el relieve queda en los bordes y los cordones encimados.
    private const float ReliefCells = 3f;
    private const float RippleSpeed = 24f;
    private const float RippleLife = 0.45f;

    private SaucePalette palette;
    private float px;
    private float squash;
    private float yield;

    private int width;
    private int height;
    // Celda (0,0) en la grilla de mundo: el ruido de los trocitos no depende de dónde nace el charco.
    private int gridX;
    private int gridY;
    // Esquina de abajo a la izquierda de la celda (0,0), en el espacio local de la raíz.
    private Vector2 originLocal;

    private float[] mass;
    private bool[] wet;
    private bool[] inside;

    private Texture2D texture;
    private Sprite sprite;
    private Color32[] pixels;
    private SpriteRenderer spriteRenderer;

    private RectInt active;
    private bool hasActive;
    private float awakeUntil;

    private struct Ripple
    {
        public Vector2 center;
        public float age;
    }

    private readonly List<Ripple> ripples = new List<Ripple>();

    /// <summary>
    /// Charco nuevo centrado en <paramref name="center"/> que cubre el óvalo de semiejes
    /// <paramref name="extents"/>. Con <paramref name="clipToEllipse"/> la salsa no sale del óvalo
    /// (la loza del plato). La raíz queda en el centro: el plato la arrastra con él.
    /// </summary>
    public static SauceDecal Create(Vector3 center, Vector2 extents, bool clipToEllipse, SaucePalette palette,
        float perspectiveSquash, float thickness, int sortingOrder, Transform parent)
    {
        var go = new GameObject("SauceDecal");
        go.transform.position = center;

        var decal = go.AddComponent<SauceDecal>();
        decal.Init(center, extents, clipToEllipse, palette, perspectiveSquash, thickness, sortingOrder);

        if (parent != null)
            go.transform.SetParent(parent, true);

        return decal;
    }

    private void Init(Vector3 center, Vector2 extents, bool clipToEllipse, SaucePalette sauce,
        float perspectiveSquash, float thickness, int sortingOrder)
    {
        palette = sauce;
        px = sauce.pixelSize;
        squash = Mathf.Max(perspectiveSquash, 0.05f);
        yield = thickness;

        gridX = Mathf.FloorToInt((center.x - extents.x) / px) - BorderCells;
        gridY = Mathf.FloorToInt((center.y - extents.y) / px) - BorderCells;
        width = Mathf.CeilToInt((center.x + extents.x) / px) + BorderCells - gridX;
        height = Mathf.CeilToInt((center.y + extents.y) / px) + BorderCells - gridY;
        originLocal = new Vector2(gridX * px - center.x, gridY * px - center.y);

        int n = width * height;
        mass = new float[n];
        wet = new bool[n];
        inside = new bool[n];
        pixels = new Color32[n];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool isInside = true;
            if (clipToEllipse)
            {
                float nx = ((gridX + x + 0.5f) * px - center.x) / extents.x;
                float ny = ((gridY + y + 0.5f) * px - center.y) / extents.y;
                isInside = nx * nx + ny * ny <= 1f;
            }
            inside[y * width + x] = isInside;
        }

        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SauceDecal"
        };
        texture.SetPixels32(pixels);
        texture.Apply(false);

        Vector2 pivot = new Vector2(-originLocal.x / (width * px), -originLocal.y / (height * px));
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 1f / px, 0, SpriteMeshType.FullRect);
        sprite.name = "SauceDecal";

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = sortingOrder;

        enabled = false;
    }

    void OnDestroy()
    {
        if (sprite != null)
            Destroy(sprite);
        if (texture != null)
            Destroy(texture);
    }

    /// <summary>
    /// Arranca con la salsa de un charco anterior del mismo frasco sobre el mismo plato (se copia
    /// entero): la salsa nueva se junta con la vieja en vez de dibujar dos contornos encimados.
    /// False si las grillas no coinciden.
    /// </summary>
    public bool TryInherit(SauceDecal previous)
    {
        if (previous == null || previous.width != width || previous.height != height ||
            previous.gridX != gridX || previous.gridY != gridY)
            return false;

        System.Array.Copy(previous.mass, mass, mass.Length);
        System.Array.Copy(previous.wet, wet, wet.Length);
        Render();
        return true;
    }

    public void SetVisible(bool visible)
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = visible;
    }

    /// <summary>Deja salsa a lo largo del tramo, repartida en pasos de menos de un pixel (sin huecos al ir rápido).</summary>
    public void PourLine(Vector3 from, Vector3 to, float volume, float radius)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / px));
        for (int i = 1; i <= steps; i++)
            Pour(Vector3.Lerp(from, to, (float)i / steps), volume / steps, radius);
    }

    /// <summary>
    /// Deja <paramref name="volume"/> de salsa alrededor del punto, en una campana de
    /// <paramref name="radius"/> (mundo) aplastada por la perspectiva.
    /// </summary>
    public void Pour(Vector3 worldPoint, float volume, float radius)
    {
        Vector2 g = ToGrid(worldPoint);
        float sigma = Mathf.Max(radius / px, 0.6f);
        int rx = Mathf.CeilToInt(sigma * 2.5f);
        int ry = Mathf.CeilToInt(sigma * 2.5f * squash);
        int cx = Mathf.FloorToInt(g.x);
        int cy = Mathf.FloorToInt(g.y);

        int x0 = Mathf.Max(cx - rx, 0), x1 = Mathf.Min(cx + rx, width - 1);
        int y0 = Mathf.Max(cy - ry, 0), y1 = Mathf.Min(cy + ry, height - 1);
        if (x0 > x1 || y0 > y1)
            return;

        float invTwoSigma2 = 1f / (2f * sigma * sigma);
        float total = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            float scale = pass == 0 ? 0f : volume * VolumeScale / total;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * width + x;
                if (!inside[i])
                    continue;

                float dx = x + 0.5f - g.x;
                float dy = (y + 0.5f - g.y) / squash;
                float weight = Mathf.Exp(-(dx * dx + dy * dy) * invTwoSigma2);
                if (pass == 0)
                    total += weight;
                else
                    mass[i] += weight * scale;
            }

            if (total <= 0f)
                return;
        }

        Wake(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
    }

    /// <summary>Gotita que cae del aire: moja unas celdas sin sumar salsa que se escurra.</summary>
    public void Splash(Vector3 worldPoint, int size)
    {
        Vector2 g = ToGrid(worldPoint);
        int cx = Mathf.FloorToInt(g.x);
        int cy = Mathf.FloorToInt(g.y);
        bool any = false;

        for (int dx = 0; dx < Mathf.Max(size, 1); dx++)
        {
            int x = cx + dx;
            if (x < 0 || x >= width || cy < 0 || cy >= height)
                continue;

            int i = cy * width + x;
            if (!inside[i])
                continue;

            wet[i] = true;
            any = true;
        }

        if (any)
            Wake(new RectInt(cx, cy, 1, 1));
    }

    /// <summary>Onda que se abre desde el impacto sobre la salsa.</summary>
    public void AddRipple(Vector3 worldPoint)
    {
        ripples.Add(new Ripple { center = ToGrid(worldPoint) });
        Wake(new RectInt(0, 0, 0, 0));
    }

    /// <summary>Radio horizontal (mundo) de la salsa alrededor del punto, o 0 si ahí no hay salsa.</summary>
    public float MeasureRadius(Vector3 worldPoint)
    {
        Vector2 g = ToGrid(worldPoint);
        int cx = Mathf.FloorToInt(g.x);
        int cy = Mathf.FloorToInt(g.y);
        if (!IsWet(cx, cy))
            return 0f;

        int left = 0, right = 0;
        while (IsWet(cx - left - 1, cy)) left++;
        while (IsWet(cx + right + 1, cy)) right++;
        return (Mathf.Max(left, right) + 0.5f) * px;
    }

    // Volumen en celdas: un pixel más chico necesita más celdas para el mismo charco en mundo.
    private float VolumeScale
    {
        get
        {
            float k = ReferencePixelSize / px;
            return k * k * k;
        }
    }

    private Vector2 ToGrid(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        return new Vector2((local.x - originLocal.x) / px, (local.y - originLocal.y) / px);
    }

    private void Wake(RectInt area)
    {
        if (area.width > 0 && area.height > 0)
        {
            if (!hasActive)
            {
                active = area;
                hasActive = true;
            }
            else
            {
                int xMin = Mathf.Min(active.xMin, area.xMin), yMin = Mathf.Min(active.yMin, area.yMin);
                int xMax = Mathf.Max(active.xMax, area.xMax), yMax = Mathf.Max(active.yMax, area.yMax);
                active = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            }
        }

        awakeUntil = Time.time + SettleSeconds;
        enabled = true;
    }

    void Update()
    {
        if (hasActive)
            Relax();

        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = ripples[i];
            r.age += Time.deltaTime;
            if (r.age >= RippleLife)
                ripples.RemoveAt(i);
            else
                ripples[i] = r;
        }

        Render();

        if (!hasActive && Time.time >= awakeUntil && ripples.Count == 0)
            enabled = false;
    }

    /// <summary>
    /// La salsa baja hacia las celdas vecinas (también en diagonal, si no el charco sale en rombo)
    /// mientras el desnivel supere su espesor. Vertical, una celda es más lejos en la mesa
    /// (perspectiva): ahí aguanta más desnivel y se escurre menos, así el charco sale ovalado.
    /// Se recorre en el lugar, alternando el sentido, y solo donde la salsa todavía se mueve: lo
    /// asentado (el cordón que ya quedó atrás) no cuesta nada.
    /// </summary>
    private void Relax()
    {
        int x0 = Mathf.Max(active.xMin - 1, 0), x1 = Mathf.Min(active.xMax, width - 1);
        int y0 = Mathf.Max(active.yMin - 1, 0), y1 = Mathf.Min(active.yMax, height - 1);

        // Umbral de cada vecino = espesor por distancia en la mesa. Con los saltos de caballo el
        // charco sale redondo; solo con vecinos y diagonales salía octogonal.
        float sy = 1f / squash;
        float yieldH = yield;
        float yieldV = yield * sy;
        float yieldD = yield * Mathf.Sqrt(1f + sy * sy);
        float yieldKnightWide = yield * Mathf.Sqrt(4f + sy * sy);
        float yieldKnightTall = yield * Mathf.Sqrt(1f + 4f * sy * sy);

        movedMinX = int.MaxValue;
        movedMinY = int.MaxValue;
        movedMaxX = int.MinValue;
        movedMaxY = int.MinValue;

        for (int it = 0; it < RelaxIterations; it++)
        {
            bool forward = (it & 1) == 0;
            for (int row = 0; row <= y1 - y0; row++)
            {
                int y = forward ? y0 + row : y1 - row;
                for (int col = 0; col <= x1 - x0; col++)
                {
                    int x = forward ? x0 + col : x1 - col;
                    int i = y * width + x;
                    if (!inside[i])
                        continue;

                    if (x < width - 1)
                        Exchange(i, x, y, x + 1, y, yieldH);
                    if (y < height - 1)
                    {
                        Exchange(i, x, y, x, y + 1, yieldV);
                        if (x < width - 1)
                            Exchange(i, x, y, x + 1, y + 1, yieldD);
                        if (x > 0)
                            Exchange(i, x, y, x - 1, y + 1, yieldD);
                        if (x < width - 2)
                            Exchange(i, x, y, x + 2, y + 1, yieldKnightWide);
                        if (x > 1)
                            Exchange(i, x, y, x - 2, y + 1, yieldKnightWide);
                    }
                    if (y < height - 2)
                    {
                        if (x < width - 1)
                            Exchange(i, x, y, x + 1, y + 2, yieldKnightTall);
                        if (x > 0)
                            Exchange(i, x, y, x - 1, y + 2, yieldKnightTall);
                    }
                }
            }
        }

        UpdateCoverage(x0 - 1, y0 - 1, x1 + 1, y1 + 1);

        // La zona viva es donde la salsa se movió, con margen para que se pueda seguir abriendo.
        if (movedMinX <= movedMaxX)
            active = new RectInt(movedMinX - 1, movedMinY - 1, movedMaxX - movedMinX + 3, movedMaxY - movedMinY + 3);
        else
            hasActive = false;
    }

    private int movedMinX, movedMinY, movedMaxX, movedMaxY;

    private void Exchange(int i, int x, int y, int nx, int ny, float threshold)
    {
        int j = ny * width + nx;
        if (!inside[j])
            return;

        float d = mass[i] - mass[j];
        // Con un mínimo: sin él, dos celdas justo en el límite se pasarían migajas para siempre.
        float excess = (d > 0f ? d : -d) - threshold;
        if (excess <= 0.002f)
            return;

        float t = FlowRate * excess * (d > 0f ? 1f : -1f);
        mass[i] -= t;
        mass[j] += t;

        movedMinX = Mathf.Min(movedMinX, Mathf.Min(x, nx));
        movedMaxX = Mathf.Max(movedMaxX, Mathf.Max(x, nx));
        movedMinY = Mathf.Min(movedMinY, y);
        movedMaxY = Mathf.Max(movedMaxY, ny);
    }

    /// <summary>
    /// Moja las celdas donde la salsa (suavizada con sus vecinas, así el borde no sale en
    /// escalones) pasa la altura de cubrir. Esa altura varía apenas por zonas: borde ondulado.
    /// </summary>
    private void UpdateCoverage(int x0, int y0, int x1, int y1)
    {
        x0 = Mathf.Max(x0, 1);
        y0 = Mathf.Max(y0, 1);
        x1 = Mathf.Min(x1, width - 2);
        y1 = Mathf.Min(y1, height - 2);

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            int i = y * width + x;
            if (wet[i] || !inside[i])
                continue;

            float smooth = (mass[i] * 4f
                            + (mass[i - 1] + mass[i + 1] + mass[i - width] + mass[i + width]) * 2f
                            + mass[i - width - 1] + mass[i - width + 1] + mass[i + width - 1] + mass[i + width + 1]) / 16f;

            float threshold = CoverHeight * (1f + EdgeNoise * (EdgeNoiseAt(gridX + x, gridY + y) - 0.5f));
            if (smooth >= threshold)
                wet[i] = true;
        }
    }

    /// <summary>Ruido suave (interpolado entre puntos cada <see cref="EdgeNoiseCells"/> celdas), de 0 a 1.</summary>
    private float EdgeNoiseAt(int wx, int wy)
    {
        float fx = wx / (float)EdgeNoiseCells;
        float fy = wy / (float)EdgeNoiseCells;
        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
        float tx = fx - ix, ty = fy - iy;
        int seed = palette.seed + 31;

        float a = SaucePalette.Hash01(ix, iy, seed);
        float b = SaucePalette.Hash01(ix + 1, iy, seed);
        float c = SaucePalette.Hash01(ix, iy + 1, seed);
        float d = SaucePalette.Hash01(ix + 1, iy + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private bool IsWet(int x, int y) =>
        x >= 0 && y >= 0 && x < width && y < height && wet[y * width + x];

    private float Relief(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return 0f;
        return Mathf.Min(mass[y * width + x], CoverHeight + ReliefCells * yield);
    }

    /// <summary>Celdas mojadas seguidas hacia arriba (dy 1) o hacia abajo (dy -1), contando esta. Tope 4.</summary>
    private int RunLength(int x, int y, int dy)
    {
        int n = 1;
        while (n < 4 && IsWet(x, y + dy * n))
            n++;
        return n;
    }

    /// <summary>Celdas mojadas seguidas hacia la izquierda, contando esta. Tope 8.</summary>
    private int LeftRun(int x, int y)
    {
        int n = 1;
        while (n < 8 && IsWet(x - n, y))
            n++;
        return n;
    }

    private void Render()
    {
        Color32 clear = new Color32(0, 0, 0, 0);
        bool hasHalo = palette.halo.a > 0;
        float reliefStep = yield;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;

            if (!wet[i])
            {
                if (IsWet(x, y + 1))
                    pixels[i] = palette.castShadow;
                else if (hasHalo && inside[i] && NearWet(x, y))
                    pixels[i] = palette.halo;
                else
                    pixels[i] = clear;
                continue;
            }

            int up = RunLength(x, y, 1);
            int down = RunLength(x, y, -1);
            bool left = IsWet(x - 1, y);
            bool right = IsWet(x + 1, y);

            Color32 tone;
            if (down == 1)
                tone = palette.outline;
            else if (!right)
                tone = up == 1 ? palette.shadow : palette.outline;
            else if (down == 2)
                tone = palette.shadow;
            else if (up == 1)
                tone = palette.light;
            else if (!left)
                tone = up < down ? palette.light : palette.shadow;
            else
            {
                // Relieve: luz de arriba a la izquierda sobre los desniveles (cordones encimados, bordes).
                float gx = Relief(x + 1, y) - Relief(x - 1, y);
                float gy = Relief(x, y + 1) - Relief(x, y - 1);
                float lit = 0.6f * gx - 0.8f * gy;

                if (lit > reliefStep * 2.2f)
                    tone = palette.highlight;
                else if (lit > reliefStep)
                    tone = palette.light;
                else if (lit < -reliefStep)
                    tone = palette.shadow;
                else
                    tone = palette.body;

                // Reflejo de la luz justo debajo del borde de arriba: un arco corto arriba a la
                // izquierda de cada mancha y brillos sueltos (de a dos pixeles) en el resto.
                if (up == 2 && down >= 3)
                {
                    int fromLeft = LeftRun(x, y);
                    if ((fromLeft >= 2 && fromLeft <= 5) ||
                        SaucePalette.Hash01((gridX + x) >> 1, gridY + y, palette.seed + 7) < 0.2f)
                        tone = palette.highlight;
                }
            }

            pixels[i] = tone;
        }

        DrawRipples();
        DrawChunks();

        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    private bool NearWet(int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if ((dx != 0 || dy != 0) && IsWet(x + dx, y + dy))
                return true;
        }
        return false;
    }

    private bool IsInterior(int x, int y) =>
        IsWet(x - 1, y) && IsWet(x + 1, y) && RunLength(x, y, 1) >= 2 && RunLength(x, y, -1) >= 3;

    /// <summary>Anillos claros que se abren desde el impacto; al final, a pixel salteado.</summary>
    private void DrawRipples()
    {
        for (int r = 0; r < ripples.Count; r++)
        {
            Ripple ripple = ripples[r];
            float radius = ripple.age * RippleSpeed;
            bool fading = ripple.age > RippleLife * 0.55f;

            int x0 = Mathf.Max(Mathf.FloorToInt(ripple.center.x - radius - 1f), 0);
            int x1 = Mathf.Min(Mathf.CeilToInt(ripple.center.x + radius + 1f), width - 1);
            int y0 = Mathf.Max(Mathf.FloorToInt(ripple.center.y - (radius + 1f) * squash), 0);
            int y1 = Mathf.Min(Mathf.CeilToInt(ripple.center.y + (radius + 1f) * squash), height - 1);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - ripple.center.x;
                float dy = (y + 0.5f - ripple.center.y) / squash;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(d - radius) >= 0.55f || !IsInterior(x, y))
                    continue;
                if (fading && ((x + y) & 1) == 1)
                    continue;

                pixels[y * width + x] = palette.light;
            }
        }
    }

    /// <summary>
    /// Trocitos fijos por celda (no se mueven cuando el charco crece): un pixel suelto, dos de
    /// costado o uno con su sombra abajo. Solo adentro de la salsa, nunca en los bordes.
    /// </summary>
    private void DrawChunks()
    {
        int colors = palette.chunkLight.Length;
        if (colors == 0 || palette.chunkDensity <= 0f)
            return;

        for (int y = 1; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            if (!wet[i])
                continue;

            int wx = gridX + x, wy = gridY + y;
            if (SaucePalette.Hash01(wx, wy, palette.seed) >= palette.chunkDensity || !IsInterior(x, y))
                continue;

            int k = Mathf.Min((int)(SaucePalette.Hash01(wx, wy, palette.seed + 1) * colors), colors - 1);
            float shape = SaucePalette.Hash01(wx, wy, palette.seed + 2);
            pixels[i] = palette.chunkLight[k];

            if (shape > 0.75f && RunLength(x, y, -1) >= 4)
                pixels[i - width] = palette.chunkShade[k];
            else if (shape > 0.45f && IsInterior(x + 1, y))
                pixels[i + 1] = palette.chunkLight[k];
        }
    }
}
