using UnityEngine;

/// <summary>
/// Corrimiento visual que se apaga solo, para piezas que se sueltan sobre la parrilla. La pieza ya
/// quedó en su lugar lógico (slots, posición, collider): solo el dibujo arranca donde se soltó y
/// llega en un instante, con un saltito opcional al apoyarse. El dueño suma el valor a la posición
/// del transform y compensa el offset de su collider, así el área de interacción nunca se mueve.
///
/// POCO sin allocations: cada pieza tiene el suyo. Se puede reiniciar o cortar en cualquier momento.
/// </summary>
public sealed class SettleOffset
{
    private Vector3 from;
    private float duration;
    private float hopHeight;
    private float t = 1f;

    public bool Active => t < 1f;

    /// <summary>
    /// Arranca el asentado. <paramref name="startOffset"/> = dónde se ve la pieza menos dónde quedó.
    /// Con un recorrido despreciable no hace nada (el dibujo ya está en su lugar).
    /// </summary>
    public bool Begin(Vector3 startOffset, float seconds, float hop)
    {
        startOffset.z = 0f;
        if (startOffset.sqrMagnitude < 0.0004f && hop <= 0f)
        {
            t = 1f;
            return false;
        }

        from = startOffset;
        duration = Mathf.Max(0.01f, seconds);
        hopHeight = Mathf.Max(0f, hop);
        t = 0f;
        return true;
    }

    /// <summary>Avanza y devuelve el corrimiento de este frame. En el último frame devuelve cero.</summary>
    public Vector3 Tick(float deltaTime)
    {
        if (t >= 1f) return Vector3.zero;

        t = Mathf.Min(1f, t + deltaTime / duration);
        return Evaluate();
    }

    /// <summary>Corrimiento actual sin avanzar (para aplicar el primer frame al arrancar).</summary>
    public Vector3 Evaluate()
    {
        if (t >= 1f) return Vector3.zero;

        Vector3 slide = from * (1f - FeelEase.OutCubic(t));
        float hop = hopHeight * FeelEase.Bump(t);
        return new Vector3(slide.x, slide.y + hop, 0f);
    }

    /// <summary>Lo deja en reposo de inmediato.</summary>
    public void Stop() => t = 1f;
}
