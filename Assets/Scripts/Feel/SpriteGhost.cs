using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copia visual de un sprite que se anima sola y desaparece: el eco de algo que se apoyó, el
/// carbón hecho ceniza que se vuela, el corte que cae al tacho, el cliente que se va. El objeto
/// real ya hizo lo suyo (se destruyó, se movió, sigue en su lugar): el fantasma no tiene collider
/// ni lógica, así que nunca demora ni cambia el resultado.
///
/// Pooleado: los fantasmas apagados se reusan. Son objetos de escena, así que al cambiar de
/// escena el pool se vacía solo (las referencias destruidas se descartan al pedir uno).
/// Corre con Time.deltaTime salvo que se pida lo contrario: la pausa los congela como al resto
/// del mundo.
/// </summary>
public sealed class SpriteGhost : MonoBehaviour
{
    public struct Motion
    {
        public float duration;
        public float scaleFrom;
        public float scaleTo;
        public float alphaFrom;
        public float alphaTo;
        /// <summary>Corrimiento en mundo al final de la animación (si no hay destino).</summary>
        public Vector3 moveBy;
        /// <summary>Si es true, viaja hasta <see cref="target"/> en vez de usar moveBy.</summary>
        public bool hasTarget;
        public Vector3 target;
        /// <summary>El movimiento acelera (absorbido) en vez de frenar (soltado).</summary>
        public bool easeIn;
        /// <summary>El alfa se mantiene y se apaga al final, en vez de bajar parejo.</summary>
        public bool fadeLate;
        public int sortingOffset;
        public bool unscaledTime;

        /// <summary>Onda que se abre detrás de algo recién apoyado: "aterrizó acá".</summary>
        public static Motion Echo(float scaleTo = 1.3f, float alpha = 0.45f, float duration = 0.24f) => new Motion
        {
            duration = duration,
            scaleFrom = 1f,
            scaleTo = scaleTo,
            alphaFrom = alpha,
            alphaTo = 0f,
            sortingOffset = -1,
        };

        /// <summary>Se achica, sube un poco y se desvanece: algo que se limpia o se consume.</summary>
        public static Motion Vanish(float rise = 0.18f, float duration = 0.3f) => new Motion
        {
            duration = duration,
            scaleFrom = 1f,
            scaleTo = 0.55f,
            alphaFrom = 1f,
            alphaTo = 0f,
            moveBy = new Vector3(0f, rise, 0f),
            fadeLate = true,
        };

        /// <summary>Viaja a un punto, se achica y se apaga: absorbido (el tacho) o de vuelta a su origen.</summary>
        public static Motion Into(Vector3 worldTarget, float duration = 0.2f, float scaleTo = 0.25f, bool accelerate = true) => new Motion
        {
            duration = duration,
            scaleFrom = 1f,
            scaleTo = scaleTo,
            alphaFrom = 1f,
            alphaTo = 0f,
            hasTarget = true,
            target = worldTarget,
            easeIn = accelerate,
            fadeLate = true,
        };
    }

    private static readonly Stack<SpriteGhost> Pool = new Stack<SpriteGhost>();

    private SpriteRenderer spriteRenderer;
    private Motion motion;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 startScale;
    private Color baseColor;
    private float elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Pool.Clear();

    /// <summary>Fantasma de lo que muestra <paramref name="source"/> ahora mismo (sprite, color, orden, tamaño en mundo).</summary>
    public static SpriteGhost Spawn(SpriteRenderer source, Motion motion)
    {
        if (source == null || source.sprite == null || !source.enabled || !source.gameObject.activeInHierarchy)
            return null;

        SpriteGhost ghost = Spawn(source.sprite, source.transform.position, source.transform.rotation,
            source.transform.lossyScale, source.color, source.sortingLayerID, source.sortingOrder, motion);

        if (ghost != null)
        {
            SpriteRenderer sr = ghost.spriteRenderer;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.drawMode = source.drawMode;
            if (source.drawMode != SpriteDrawMode.Simple)
                sr.size = source.size;
            sr.sharedMaterial = source.sharedMaterial;
        }

        return ghost;
    }

    public static SpriteGhost Spawn(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 worldScale,
        Color color, int sortingLayerId, int sortingOrder, Motion motion)
    {
        if (sprite == null || !Application.isPlaying)
            return null;

        SpriteGhost ghost = Acquire();
        SpriteRenderer sr = ghost.spriteRenderer;

        sr.sprite = sprite;
        sr.flipX = false;
        sr.flipY = false;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.color = color;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder + motion.sortingOffset;

        Transform t = ghost.transform;
        t.SetPositionAndRotation(position, rotation);
        t.localScale = worldScale;

        ghost.motion = motion;
        ghost.motion.duration = Mathf.Max(0.01f, motion.duration);
        ghost.startPosition = position;
        ghost.endPosition = motion.hasTarget ? new Vector3(motion.target.x, motion.target.y, position.z) : position + motion.moveBy;
        ghost.startScale = worldScale;
        ghost.baseColor = color;
        ghost.elapsed = 0f;

        ghost.gameObject.SetActive(true);
        ghost.Apply(0f);
        return ghost;
    }

    private static SpriteGhost Acquire()
    {
        while (Pool.Count > 0)
        {
            SpriteGhost pooled = Pool.Pop();
            if (pooled != null)
                return pooled;
        }

        var go = new GameObject("SpriteGhost");
        go.SetActive(false);
        SpriteGhost ghost = go.AddComponent<SpriteGhost>();
        ghost.spriteRenderer = go.AddComponent<SpriteRenderer>();
        return ghost;
    }

    private void Update()
    {
        elapsed += motion.unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = elapsed / motion.duration;

        if (t >= 1f)
        {
            Release();
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        float move = motion.easeIn ? FeelEase.InCubic(t) : FeelEase.OutCubic(t);
        transform.position = Vector3.LerpUnclamped(startPosition, endPosition, move);
        transform.localScale = startScale * Mathf.Lerp(motion.scaleFrom, motion.scaleTo, FeelEase.OutCubic(t));

        float fade = motion.fadeLate ? FeelEase.InQuad(t) : t;
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Lerp(motion.alphaFrom, motion.alphaTo, fade);
        spriteRenderer.color = c;
    }

    private void Release()
    {
        spriteRenderer.sprite = null;
        gameObject.SetActive(false);
        Pool.Push(this);
    }
}
