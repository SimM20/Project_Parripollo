using UnityEngine;

/// <summary>
/// Curvas de easing compartidas por las animaciones de presentación (game feel). Todas reciben
/// t en [0, 1] (se clampea) y devuelven 0 en t = 0 y 1 en t = 1; OutBack se pasa de 1 un instante
/// antes de asentarse. Son funciones puras: sin estado, sin allocations.
/// </summary>
public static class FeelEase
{
    /// <summary>Arranca rápido y frena al llegar: la respuesta se ve al toque.</summary>
    public static float OutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>Arranca lento y acelera: algo que "se va" o es absorbido.</summary>
    public static float InCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    /// <summary>Lento en los dos extremos, rápido en el medio: transiciones que tienen que notarse enteras.</summary>
    public static float InOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.5f) return 4f * t * t * t;
        float u = 2f - 2f * t;
        return 1f - u * u * u * 0.5f;
    }

    public static float InQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    /// <summary>
    /// Se pasa un poco del destino y vuelve. <paramref name="overshoot"/> 1.70158 es el clásico
    /// (~10 % de pasada); valores chicos (0.5–1) dan un rebote apenas perceptible.
    /// </summary>
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        t = Mathf.Clamp01(t);
        float c3 = overshoot + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + overshoot * u * u;
    }

    /// <summary>Campana 0 → 1 → 0: sirve para saltitos y golpes que vuelven solos a reposo.</summary>
    public static float Bump(float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Sin(t * Mathf.PI);
    }
}
