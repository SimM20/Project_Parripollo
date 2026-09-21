using UnityEngine;

/// <summary>
/// Mueve un cuerpo del cielo (sol, luna) por un arco según la hora: a la <see cref="riseHour"/>
/// está en <see cref="risePoint"/>, a mitad de camino pasa por lo más alto y a la
/// <see cref="setHour"/> llega a <see cref="setPoint"/>. Los puntos son locales al padre y conviene
/// dejarlos apenas debajo del horizonte: la capa del paisaje tapa al cuerpo al salir y al ponerse.
///
/// Si <see cref="setHour"/> es menor que <see cref="riseHour"/>, el arco cruza la medianoche (la
/// luna). Fuera del arco el cuerpo espera escondido en la punta más cercana (recién puesto o por
/// salir); hacerlo invisible es trabajo del alfa de su <see cref="DayCycleLayer"/>.
///
/// Solo toca la posición. Tiene que estar debajo de un <see cref="DayCycleBackground"/>.
/// </summary>
[DisallowMultipleComponent]
public class DayCycleArc : MonoBehaviour, IDayCycleVisual
{
    [Header("Horario")]
    [Tooltip("Hora en que el cuerpo está en el punto de salida. 6.25 = 06:15.")]
    [Range(0f, 24f)]
    [SerializeField] private float riseHour = 6.25f;

    [Tooltip("Hora en que llega al punto de puesta. Menor que la salida = el arco cruza la medianoche.")]
    [Range(0f, 24f)]
    [SerializeField] private float setHour = 19.75f;

    [Header("Recorrido (local al padre)")]
    [SerializeField] private Vector2 risePoint = new Vector2(-7.5f, 1.9f);
    [SerializeField] private Vector2 setPoint = new Vector2(7.5f, 1.9f);

    [Tooltip("Cuánto sube el arco por encima de la recta salida → puesta, en unidades de mundo.")]
    [SerializeField] private float arcHeight = 2f;

    private void Start()
    {
        if (GetComponentInParent<DayCycleBackground>() == null)
            Debug.LogWarning("[DayCycleArc] '" + name + "' no está debajo de un DayCycleBackground: nadie le pasa la hora.", this);
    }

    public void ApplyHour(float hour)
    {
        Vector2 point = ArcPoint(Progress(hour));
        transform.localPosition = new Vector3(point.x, point.y, transform.localPosition.z);
    }

    /// <summary>0 en la salida, 1 en la puesta. De noche, la punta más cercana.</summary>
    private float Progress(float hour)
    {
        float span = Mathf.Repeat(setHour - riseHour, 24f);

        if (span <= 0f)
            return 0f;

        float elapsed = Mathf.Repeat(hour - riseHour, 24f);

        if (elapsed <= span)
            return elapsed / span;

        // Bajo el horizonte: la primera mitad de la noche sigue en la puesta, la segunda ya espera en la salida.
        float night = 24f - span;
        return elapsed - span < night * 0.5f ? 1f : 0f;
    }

    private Vector2 ArcPoint(float progress)
    {
        Vector2 point = Vector2.Lerp(risePoint, setPoint, progress);
        point.y += arcHeight * Mathf.Sin(progress * Mathf.PI);
        return point;
    }

    private void OnDrawGizmosSelected()
    {
        // El recorrido a la vista, para acomodarlo contra el horizonte sin entrar en Play.
        const int segments = 32;
        Transform parent = transform.parent;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Vector3 previous = ToWorld(parent, ArcPoint(0f));

        for (int i = 1; i <= segments; i++)
        {
            Vector3 next = ToWorld(parent, ArcPoint(i / (float)segments));
            Gizmos.DrawLine(previous, next);
            previous = next;
        }

        Gizmos.DrawWireSphere(ToWorld(parent, risePoint), 0.15f);
        Gizmos.DrawWireSphere(ToWorld(parent, setPoint), 0.15f);
    }

    private static Vector3 ToWorld(Transform parent, Vector2 local) =>
        parent != null ? parent.TransformPoint(local) : (Vector3)local;
}
