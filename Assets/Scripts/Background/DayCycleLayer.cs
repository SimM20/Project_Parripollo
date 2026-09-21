using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una capa del fondo que cambia con la hora: el color (tinte × alfa) de su SpriteRenderer se
/// mezcla entre claves horarias y, opcionalmente, también el sprite.
///
/// Claves: pares hora → color, en cualquier orden. Entre dos claves el color se interpola lineal;
/// después de la última se vuelve a la primera pasando por la medianoche, así que la lista describe
/// un día completo aunque la jornada solo recorra 06:30 → 21:00. El color multiplica al sprite:
/// los sprites dibujados en blanco (cielo, sol, luna, estrellas) toman el color de la clave tal
/// cual; en los que tienen arte (paisaje, nubes) blanco = el dibujo original.
///
/// Sprite por clave (opcional, vacío = el que ya tiene el renderer): si las dos claves que rodean a
/// la hora tienen sprites distintos, se funden. El renderer propio dibuja el de la clave anterior y
/// un renderer hijo ("Fundido", creado en runtime con sortingOrder + 1) dibuja el de la siguiente
/// encima, con alfa = avance entre las dos. Es para cuando Arte entregue la capa pintada por momento
/// del día en lugar de teñida. ⚠️ Dejar libre el sortingOrder siguiente al de la capa.
///
/// Tiene que estar debajo de un <see cref="DayCycleBackground"/>: es quien le pasa la hora.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class DayCycleLayer : MonoBehaviour, IDayCycleVisual
{
    [Serializable]
    public struct Key
    {
        [Tooltip("Solo para leer la lista en el inspector (Amanecer, Mediodía...).")]
        public string label;

        [Tooltip("Hora de la clave en horas decimales: 6.5 = 06:30.")]
        [Range(0f, 24f)]
        public float hour;

        [Tooltip("Tinte que multiplica al sprite. El alfa hace aparecer o desaparecer la capa (estrellas, luna).")]
        public Color color;

        [Tooltip("Opcional. Vacío = el sprite que el renderer ya tiene.")]
        public Sprite sprite;
    }

    [Tooltip("Claves horarias de la capa. El orden no importa.")]
    [SerializeField] private List<Key> keys = new List<Key>();

    private SpriteRenderer spriteRenderer;
    private SpriteRenderer blendRenderer;
    private Sprite baseSprite;
    private float lastHour = float.NaN;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseSprite = spriteRenderer.sprite;
    }

    private void Start()
    {
        if (GetComponentInParent<DayCycleBackground>() == null)
            Debug.LogWarning("[DayCycleLayer] '" + name + "' no está debajo de un DayCycleBackground: nadie le pasa la hora.", this);
    }

    public void ApplyHour(float hour)
    {
        lastHour = hour;

        if (keys.Count == 0)
            return;

        FindKeys(hour, out Key from, out Key to, out float t);

        Color color = Color.Lerp(from.color, to.color, t);
        Sprite fromSprite = from.sprite != null ? from.sprite : baseSprite;
        Sprite toSprite = to.sprite != null ? to.sprite : baseSprite;

        if (spriteRenderer.sprite != fromSprite)
            spriteRenderer.sprite = fromSprite;

        spriteRenderer.color = color;

        if (fromSprite != toSprite && t > 0f)
        {
            EnsureBlendRenderer();

            if (blendRenderer.sprite != toSprite)
                blendRenderer.sprite = toSprite;

            color.a *= t;
            blendRenderer.color = color;
            blendRenderer.enabled = true;
        }
        else if (blendRenderer != null)
        {
            blendRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Clave anterior y siguiente a <paramref name="hour"/> (dando la vuelta por la medianoche) y el
    /// avance entre ellas. Recorre la lista entera: no depende del orden en que estén cargadas.
    /// </summary>
    private void FindKeys(float hour, out Key from, out Key to, out float t)
    {
        int fromIndex = 0;
        int toIndex = 0;
        float sinceFrom = float.MaxValue;
        float untilTo = float.MaxValue;

        for (int i = 0; i < keys.Count; i++)
        {
            float since = Mathf.Repeat(hour - keys[i].hour, 24f);
            float until = Mathf.Repeat(keys[i].hour - hour, 24f);

            // Una clave justo en esta hora es la anterior: la siguiente vez que toca es mañana.
            if (until <= 0f)
                until = 24f;

            if (since < sinceFrom)
            {
                sinceFrom = since;
                fromIndex = i;
            }

            if (until < untilTo)
            {
                untilTo = until;
                toIndex = i;
            }
        }

        from = keys[fromIndex];
        to = keys[toIndex];

        float span = sinceFrom + untilTo;
        t = span > 0f ? sinceFrom / span : 0f;
    }

    private void EnsureBlendRenderer()
    {
        if (blendRenderer != null)
            return;

        var child = new GameObject("Fundido");
        child.transform.SetParent(transform, false);

        blendRenderer = child.AddComponent<SpriteRenderer>();
        blendRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        blendRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        blendRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        blendRenderer.flipX = spriteRenderer.flipX;
        blendRenderer.flipY = spriteRenderer.flipY;
        blendRenderer.drawMode = spriteRenderer.drawMode;

        if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
        {
            blendRenderer.tileMode = spriteRenderer.tileMode;
            blendRenderer.size = spriteRenderer.size;
        }
    }

    private void OnValidate()
    {
        // Editando claves en Play el cambio se ve al instante, sin esperar a que se mueva la hora.
        if (Application.isPlaying && spriteRenderer != null && !float.IsNaN(lastHour))
            ApplyHour(lastHour);
    }
}
