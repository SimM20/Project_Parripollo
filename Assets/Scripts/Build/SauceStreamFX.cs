using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La salsa en el aire, en pixel art: el chorro, los trocitos que bajan por él y las gotas que
/// saltan al tocar el plato. Se dibuja en una textura que sigue al chorro, sobre la misma grilla
/// de mundo que el charco (<see cref="SauceDecal"/>), así los pixeles de los dos calzan.
/// El chorro se dibuja fila por fila: contorno a los costados, luz a la izquierda y sombra a la
/// derecha. Sale grueso de la boca y se afina al acelerar. Trae los pulsos del apretón de la
/// botella, que bajan a la velocidad de la caída, y una corona de salpicaduras donde toca. Al
/// cortar la vertida la cola se suelta de la boca, termina de caer en el plato y se parte en gotas.
/// </summary>
public class SauceStreamFX : MonoBehaviour
{
    private const int PadCells = 4;
    private const int TextureStep = 32;
    // Cuánto se ensancha y afina el chorro con cada apretón.
    private const float PulseAmount = 0.06f;
    // Segundos que tarda la cola suelta en partirse en gotas, y largo de cada gota (en medios anchos).
    private const float TailBreakSeconds = 0.16f;
    private const float TailBeadLength = 4.5f;
    // Cuadros por segundo de la corona del impacto (más lento que el juego: se lee como dibujo).
    private const float CrownFps = 14f;

    private SaucePalette palette;
    private float px;
    private Func<SauceDecal> decalProvider;
    private Action<Vector3> onTailLanded;

    private SpriteRenderer spriteRenderer;
    private Texture2D texture;
    private Sprite sprite;
    private Color32[] pixels;
    private int texWidth;
    private int texHeight;
    private int gridX;
    private int gridY;

    private readonly List<Vector3> curve = new List<Vector3>();
    private readonly List<float> lengths = new List<float>();
    private bool hasStream;
    private bool attached;
    private bool landed;
    // Distancias desde la boca, a lo largo del chorro: dónde empieza (0 pegado a la boca) y dónde termina.
    private float top;
    private float head;
    private float topSpeed;
    private float headSpeed;
    private float releasedFor;
    private float clock;

    private float mouthHalfWidth;
    private float startSpeed;
    private float acceleration;
    private float pulseFrequency;
    private float chunkRate;
    private float chunkTimer;

    private float tailPourRate;
    private float tailPourRadius;

    private class Chunk
    {
        public float distance;
        public float speed;
        public float side;
        public int color;
    }

    private class Droplet
    {
        public Vector3 from;
        public Vector3 to;
        public float t;
        public float duration;
        public float arc;
        public int size;
    }

    private readonly List<Chunk> chunks = new List<Chunk>();
    private readonly List<Chunk> chunkPool = new List<Chunk>();
    private readonly List<Droplet> droplets = new List<Droplet>();

    /// <summary>True mientras haya algo en el aire (chorro, cola o gotas).</summary>
    public bool IsBusy => hasStream || droplets.Count > 0;

    /// <param name="decalProvider">Charco donde cae la salsa (lo crea si todavía no hay).</param>
    /// <param name="onTailLanded">Avisa cuando el frente de una cola suelta llega al plato.</param>
    public static SauceStreamFX Create(SaucePalette palette, int sortingOrder,
        Func<SauceDecal> decalProvider, Action<Vector3> onTailLanded)
    {
        var go = new GameObject("SauceStream");
        var fx = go.AddComponent<SauceStreamFX>();
        fx.palette = palette;
        fx.px = palette.pixelSize;
        fx.decalProvider = decalProvider;
        fx.onTailLanded = onTailLanded;
        fx.spriteRenderer = go.AddComponent<SpriteRenderer>();
        fx.spriteRenderer.sortingOrder = sortingOrder;
        fx.spriteRenderer.enabled = false;
        fx.enabled = false;
        return fx;
    }

    void OnDestroy()
    {
        if (sprite != null)
            Destroy(sprite);
        if (texture != null)
            Destroy(texture);
    }

    /// <summary>Vertida nueva: cómo sale de la boca (medio ancho y velocidad) y cómo cae.</summary>
    public void BeginPour(float halfWidth, float initialSpeed, float fallAcceleration, float squeezeFrequency, float chunksPerSecond)
    {
        mouthHalfWidth = halfWidth;
        startSpeed = Mathf.Max(initialSpeed, 0.01f);
        acceleration = Mathf.Max(fallAcceleration, 0.01f);
        pulseFrequency = squeezeFrequency;
        chunkRate = palette.chunkLight.Length > 0 ? chunksPerSecond : 0f;
        chunkTimer = 0f;

        for (int i = 0; i < chunks.Count; i++)
            chunkPool.Add(chunks[i]);
        chunks.Clear();
    }

    /// <summary>
    /// Forma del chorro este frame, de la boca a la punta (el plato, si ya tocó). Se llama en cada
    /// frame de la vertida.
    /// </summary>
    public void UpdateStream(Vector3[] points, bool streamLanded, float frontSpeed)
    {
        curve.Clear();
        for (int i = 0; i < points.Length; i++)
            curve.Add(points[i]);
        RecomputeLengths();

        hasStream = true;
        attached = true;
        landed = streamLanded;
        top = 0f;
        head = lengths[lengths.Count - 1];
        headSpeed = frontSpeed;
        Wake();
    }

    /// <summary>
    /// Se cortó la vertida: el chorro se suelta de la boca y sigue cayendo. Lo que todavía estaba
    /// en el aire llega al plato (si no había tocado, el frente cae derecho hasta
    /// <paramref name="groundY"/>) y se suma al charco a <paramref name="pourRate"/>.
    /// </summary>
    public void Release(float groundY, float pourRate, float pourRadius)
    {
        if (!hasStream || !attached)
            return;

        attached = false;
        releasedFor = 0f;
        topSpeed = startSpeed;
        tailPourRate = pourRate;
        tailPourRadius = pourRadius;

        if (!landed)
        {
            Vector3 end = curve[curve.Count - 1];
            if (end.y > groundY + px * 0.5f)
            {
                curve.Add(new Vector3(end.x, groundY, end.z));
                RecomputeLengths();
            }
        }
    }

    /// <summary>Gotita que salta de <paramref name="from"/> y cae en <paramref name="to"/>, donde moja el charco.</summary>
    public void SpawnDroplet(Vector3 from, Vector3 to, int size)
    {
        float distance = Vector2.Distance(from, to);
        droplets.Add(new Droplet
        {
            from = from,
            to = to,
            duration = Mathf.Lerp(0.12f, 0.24f, Mathf.Clamp01(distance / 0.2f)),
            arc = Mathf.Max(distance * 0.45f, px * 2f),
            size = size
        });
        Wake();
    }

    private void Wake()
    {
        enabled = true;
        spriteRenderer.enabled = true;
    }

    private void RecomputeLengths()
    {
        lengths.Clear();
        lengths.Add(0f);
        for (int i = 1; i < curve.Count; i++)
            lengths.Add(lengths[i - 1] + Vector2.Distance(curve[i - 1], curve[i]));
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        clock += dt;
        StepTail(dt);
        StepChunks(dt);
        StepDroplets(dt);
        Draw();

        if (!IsBusy)
        {
            spriteRenderer.enabled = false;
            enabled = false;
        }
    }

    private void StepTail(float dt)
    {
        if (!hasStream || attached)
            return;

        releasedFor += dt;
        float total = lengths[lengths.Count - 1];

        topSpeed += acceleration * dt;
        top += topSpeed * dt;

        if (!landed)
        {
            headSpeed += acceleration * dt;
            head = Mathf.Min(head + headSpeed * dt, total);
            if (head >= total)
            {
                landed = true;
                onTailLanded?.Invoke(curve[curve.Count - 1]);
            }
        }
        else if (top < total)
        {
            SauceDecal decal = decalProvider?.Invoke();
            if (decal != null)
                decal.Pour(curve[curve.Count - 1], tailPourRate * dt, tailPourRadius);
        }

        if (top >= head)
            hasStream = false;
    }

    private void StepChunks(float dt)
    {
        if (attached && chunkRate > 0f)
        {
            chunkTimer += chunkRate * dt;
            while (chunkTimer >= 1f)
            {
                chunkTimer -= 1f;
                Chunk chunk = chunkPool.Count > 0 ? chunkPool[chunkPool.Count - 1] : new Chunk();
                if (chunkPool.Count > 0)
                    chunkPool.RemoveAt(chunkPool.Count - 1);

                chunk.distance = 0f;
                chunk.speed = startSpeed;
                chunk.side = UnityEngine.Random.Range(-0.6f, 0.6f);
                chunk.color = UnityEngine.Random.Range(0, palette.chunkLight.Length);
                chunks.Add(chunk);
            }
        }

        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk chunk = chunks[i];
            chunk.speed += acceleration * dt;
            chunk.distance += chunk.speed * dt;

            // Al llegar a la punta se pierde: en el charco ya están los trocitos.
            if (!hasStream || chunk.distance >= head)
            {
                chunks.RemoveAt(i);
                chunkPool.Add(chunk);
            }
        }
    }

    private void StepDroplets(float dt)
    {
        for (int i = droplets.Count - 1; i >= 0; i--)
        {
            Droplet d = droplets[i];
            d.t += dt;
            if (d.t < d.duration)
                continue;

            SauceDecal decal = decalProvider?.Invoke();
            if (decal != null)
                decal.Splash(d.to, d.size);
            droplets.RemoveAt(i);
        }
    }

    private Vector3 DropletPosition(Droplet d)
    {
        float k = Mathf.Clamp01(d.t / d.duration);
        return Vector3.Lerp(d.from, d.to, k) + Vector3.up * (d.arc * 4f * k * (1f - k));
    }

    // ---------------------------------------------------------------- dibujo

    private void Draw()
    {
        if (!TryGetBounds(out Vector2 min, out Vector2 max))
            return;

        gridX = Mathf.FloorToInt(min.x / px) - PadCells;
        gridY = Mathf.FloorToInt(min.y / px) - PadCells;
        int w = Mathf.CeilToInt(max.x / px) + PadCells - gridX;
        int h = Mathf.CeilToInt(max.y / px) + PadCells - gridY;
        EnsureTexture(w, h);

        Array.Clear(pixels, 0, pixels.Length);

        if (hasStream)
        {
            DrawStream();
            DrawChunks();
            DrawCrown();
        }

        DrawDroplets();

        texture.SetPixels32(pixels);
        texture.Apply(false);

        float z = curve.Count > 0 ? curve[0].z : transform.position.z;
        transform.position = new Vector3(gridX * px, gridY * px, z);
    }

    private bool TryGetBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        if (hasStream)
        {
            float pad = mouthHalfWidth * 2f + px * 3f;
            for (int i = 0; i < curve.Count; i++)
            {
                if (i > 0 && lengths[i - 1] > head) break;
                if (i < curve.Count - 1 && lengths[i + 1] < top) continue;

                Vector2 p = curve[i];
                min = Vector2.Min(min, p - Vector2.one * pad);
                max = Vector2.Max(max, p + Vector2.one * pad);
                any = true;
            }
        }

        for (int i = 0; i < droplets.Count; i++)
        {
            Vector2 p = DropletPosition(droplets[i]);
            min = Vector2.Min(min, p - Vector2.one * px * 2f);
            max = Vector2.Max(max, p + Vector2.one * px * 2f);
            any = true;
        }

        return any;
    }

    private void EnsureTexture(int w, int h)
    {
        if (texture != null && w <= texWidth && h <= texHeight)
            return;

        texWidth = Mathf.Max(texWidth, Mathf.CeilToInt(w / (float)TextureStep) * TextureStep);
        texHeight = Mathf.Max(texHeight, Mathf.CeilToInt(h / (float)TextureStep) * TextureStep);

        if (sprite != null)
            Destroy(sprite);
        if (texture != null)
            Destroy(texture);

        texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SauceStream"
        };
        pixels = new Color32[texWidth * texHeight];
        sprite = Sprite.Create(texture, new Rect(0, 0, texWidth, texHeight), Vector2.zero, 1f / px, 0, SpriteMeshType.FullRect);
        sprite.name = "SauceStream";
        spriteRenderer.sprite = sprite;
    }

    private void Put(int x, int y, Color32 color)
    {
        if (x < 0 || y < 0 || x >= texWidth || y >= texHeight)
            return;
        pixels[y * texWidth + x] = color;
    }

    /// <summary>Fase (en vueltas) del apretón que pasa por esa altura del chorro, con lo que tardó en bajar.</summary>
    private float PulsePhase(float distance)
    {
        float v2 = startSpeed * startSpeed;
        float travel = (Mathf.Sqrt(v2 + 2f * acceleration * distance) - startSpeed) / acceleration;
        return (clock - travel) * pulseFrequency;
    }

    /// <summary>
    /// Medio ancho del chorro a esa distancia de la boca. Al acelerar se afina (el mismo caudal pasa
    /// más rápido), late con los apretones, y las puntas son redondas como una gota.
    /// </summary>
    private float HalfWidthAt(float distance)
    {
        float v2 = startSpeed * startSpeed;
        float hw = mouthHalfWidth * Mathf.Pow(v2 / (v2 + 2f * acceleration * distance), 0.25f);

        hw *= 1f + PulseAmount * Mathf.Sin(PulsePhase(distance) * 2f * Mathf.PI);

        // Suelta, la cola se afina y se estrangula en cuentas cada pocos pixeles hasta cortarse en
        // gotas (las cuentas van con la cola, no con los apretones: esos abajo son muy largos).
        if (!attached)
        {
            float breakup = Mathf.Clamp01(releasedFor / TailBreakSeconds);
            float beads = Mathf.Sin((distance - top) / (mouthHalfWidth * TailBeadLength) * 2f * Mathf.PI);
            hw *= (1f + breakup * 1.15f * beads) * (1f - breakup * 0.25f);
        }

        float total = lengths[lengths.Count - 1];
        float capRadius = mouthHalfWidth * 1.3f;

        // El frente que todavía no tocó: gota redonda, un poco más gorda.
        float toHead = head - distance;
        if (!landed && toHead < capRadius)
            hw = Mathf.Min(hw * 1.3f, CapHalfWidth(capRadius, toHead));

        // El corte de arriba de la cola suelta.
        float fromTop = distance - top;
        if (!attached && fromTop < capRadius)
            hw = Mathf.Min(hw, CapHalfWidth(capRadius, fromTop));

        // Donde toca el plato el chorro se aplasta y se abre.
        float toImpact = total - distance;
        float footLength = px * 3f;
        if (landed && toImpact < footLength)
            hw += (1f - toImpact / footLength) * px * 1.3f;

        return Mathf.Max(hw, 0f);
    }

    private static float CapHalfWidth(float radius, float fromEnd)
    {
        float e = Mathf.Clamp(fromEnd, 0f, radius);
        return Mathf.Sqrt(Mathf.Max(radius * radius - (radius - e) * (radius - e), 0f));
    }

    /// <summary>
    /// Recorre el chorro de arriba hacia abajo, una fila de pixeles por vez (el chorro siempre baja,
    /// así cada fila cae en un solo tramo de la curva).
    /// </summary>
    private void DrawStream()
    {
        for (int seg = 0; seg < curve.Count - 1; seg++)
        {
            float s0 = lengths[seg], s1 = lengths[seg + 1];
            if (s1 < top || s0 > head)
                continue;

            Vector3 p0 = curve[seg], p1 = curve[seg + 1];
            float yHigh = Mathf.Max(p0.y, p1.y), yLow = Mathf.Min(p0.y, p1.y);
            if (yHigh - yLow < 0.00001f)
                continue;

            Vector2 dir = ((Vector2)(p1 - p0)).normalized;
            float stretch = 1f / Mathf.Max(Mathf.Abs(dir.y), 0.35f);

            int rowHigh = Mathf.FloorToInt(yHigh / px - 0.5f) - gridY;
            int rowLow = Mathf.CeilToInt(yLow / px - 0.5f) - gridY;

            for (int row = rowHigh; row >= rowLow; row--)
            {
                float yc = (gridY + row + 0.5f) * px;
                float k = (p0.y - yc) / (p0.y - p1.y);
                float s = Mathf.Lerp(s0, s1, k);
                if (s < top || s > head)
                    continue;

                float xc = Mathf.Lerp(p0.x, p1.x, k);
                DrawRow(row, xc, HalfWidthAt(s) * stretch, s);
            }
        }
    }

    private void DrawRow(int row, float worldX, float halfWidth, float distance)
    {
        float center = worldX / px - gridX;
        float half = halfWidth / px;

        int c0 = Mathf.CeilToInt(center - half - 0.5f);
        int c1 = Mathf.FloorToInt(center + half - 0.5f);
        if (c1 < c0)
        {
            // Más fino que un pixel: un pixel si alcanza, si no un hueco (así se corta en gotas).
            if (half < 0.25f)
                return;
            c0 = c1 = Mathf.FloorToInt(center);
        }

        int n = c1 - c0 + 1;
        // Un brillo corto por apretón, que baja con la salsa.
        bool glint = Mathf.Sin(PulsePhase(distance) * 2f * Mathf.PI) > 0.8f;

        for (int c = c0; c <= c1; c++)
        {
            int j = c - c0;
            Color32 tone;
            if (n == 1)
                tone = palette.body;
            else if (n == 2)
                tone = j == 0 ? palette.light : palette.shadow;
            else if (n == 3)
                tone = j == 0 ? palette.light : j == 1 ? palette.body : palette.shadow;
            else if (j == 0 || j == n - 1)
                tone = palette.outline;
            else if (j == 1)
                tone = glint ? palette.highlight : palette.light;
            else if (j == n - 2 && n >= 5)
                tone = palette.shadow;
            else
                tone = palette.body;

            Put(c, row, tone);
        }
    }

    private void DrawChunks()
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            Chunk chunk = chunks[i];
            if (chunk.distance < top || chunk.distance > head)
                continue;

            if (!TrySample(chunk.distance, out Vector3 p))
                continue;

            // Un pixel por trocito y nunca sobre el contorno: el chorro tiene que leerse como salsa.
            float half = HalfWidthAt(chunk.distance) / px;
            if (half < 1.5f)
                continue;

            int x = Mathf.FloorToInt(p.x / px + chunk.side * (half - 1.5f)) - gridX;
            int y = Mathf.FloorToInt(p.y / px) - gridY;
            Put(x, y, palette.chunkLight[chunk.color]);
        }
    }

    private bool TrySample(float distance, out Vector3 point)
    {
        point = Vector3.zero;
        for (int i = 1; i < curve.Count; i++)
        {
            if (lengths[i] < distance)
                continue;

            float segment = lengths[i] - lengths[i - 1];
            float k = segment > 0.0001f ? (distance - lengths[i - 1]) / segment : 0f;
            point = Vector3.Lerp(curve[i - 1], curve[i], k);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Salpicaduras de la corona donde el chorro toca el plato: pixeles que saltan a los costados
    /// del pie del chorro y cambian a pocos cuadros por segundo, como un dibujo animado.
    /// </summary>
    private void DrawCrown()
    {
        float total = lengths[lengths.Count - 1];
        if (!landed || head < total - px || top >= total)
            return;

        Vector3 end = curve[curve.Count - 1];
        int ex = Mathf.FloorToInt(end.x / px) - gridX;
        int ey = Mathf.FloorToInt(end.y / px) - gridY;
        float footHalf = HalfWidthAt(total) / px;
        int frame = Mathf.FloorToInt(clock * CrownFps);

        for (int k = 0; k < 4; k++)
        {
            float a = SaucePalette.Hash01(frame, k, palette.seed + 3);
            float b = SaucePalette.Hash01(frame, k + 17, palette.seed + 3);
            int side = (k & 1) == 0 ? -1 : 1;
            int x = ex + side * Mathf.RoundToInt(footHalf + 0.5f + a * 1.8f);
            int y = ey + Mathf.FloorToInt(b * 3f);

            Put(x, y, a < 0.5f ? palette.light : palette.body);
            if (b > 0.6f)
                Put(x, y - 1, palette.shadow);
        }
    }

    private void DrawDroplets()
    {
        for (int i = 0; i < droplets.Count; i++)
        {
            Droplet d = droplets[i];
            Vector3 p = DropletPosition(d);
            int x = Mathf.FloorToInt(p.x / px) - gridX;
            int y = Mathf.FloorToInt(p.y / px) - gridY;

            if (d.size <= 1)
            {
                Put(x, y, palette.body);
            }
            else
            {
                Put(x, y, palette.light);
                Put(x + 1, y, palette.body);
                Put(x, y - 1, palette.shadow);
                Put(x + 1, y - 1, palette.outline);
            }
        }
    }
}
