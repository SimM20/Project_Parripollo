using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tránsito de la ruta del fondo: autos, camionetas, camiones y micros que pasan cada tanto por
/// detrás de los clientes. Solo ambiente: no toca el gameplay.
///
/// Cada carril (<see cref="lanes"/>) larga un vehículo cada <see cref="Lane.spawnInterval"/>
/// segundos (al azar), más o menos seguido según <see cref="densityByHour"/>. El carril de atrás es
/// el parallax: vehículos más chicos, más lentos y apenas más apagados, que pasan detrás de los del
/// carril de adelante. Se maneja por la derecha: mirando al norte (el sol sale por la derecha), el
/// carril cercano va hacia la derecha y el lejano hacia la izquierda.
///
/// En un carril no hay sobrepasos: el que entra nunca va más rápido que el que tiene adelante, y
/// espera a tener <see cref="minGap"/> de espacio.
///
/// Color: los vehículos toman el tinte de <see cref="tintSource"/> (la capa donde está dibujada la
/// ruta), así de noche quedan tan oscuros como el asfalto. Las luces (faros con su haz y luces
/// traseras) no se tiñen: se prenden según <see cref="lightsByHour"/>.
///
/// <see cref="occluders"/>: recortes del paisaje que están delante de la ruta (el poste del cartel
/// RUTA 40). Se dibujan encima de los vehículos con el mismo tinte que el paisaje: sin un vehículo
/// detrás no se distinguen del dibujo de abajo.
///
/// Todo se dibuja dentro del <see cref="SortingGroup"/> de este objeto: los
/// <see cref="Lane.sortingOrder"/> y el orden de los recortes son relativos al grupo, que se ordena
/// como una sola capa contra el resto de la escena.
///
/// Corre con Time.deltaTime: la pausa frena el tránsito. Tiene que estar debajo de un
/// <see cref="DayCycleBackground"/>: es quien le pasa la hora.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SortingGroup))]
public class RoadTraffic : MonoBehaviour, IDayCycleVisual
{
    [Serializable]
    public class VehicleType
    {
        [Tooltip("Solo para leer la lista en el inspector.")]
        public string label;

        [Tooltip("Carrocerías mirando a la DERECHA, una por color: en cada pasada sale una al azar. " +
                 "Pivote al pie del centro de la carrocería, así al espejarla queda en el lugar.")]
        public Sprite[] bodies;

        [Tooltip("Opcional. Faros con su haz y luces traseras, prendidos. Mismo lienzo y pivote que las carrocerías.")]
        public Sprite lights;

        [Tooltip("Peso al sortear el próximo vehículo. 0 = no sale.")]
        [Min(0f)]
        public float weight = 1f;

        [Tooltip("Velocidad en unidades por segundo (mín, máx), antes del multiplicador del carril.")]
        public Vector2 speed = new Vector2(2.6f, 3.4f);
    }

    [Serializable]
    public class Lane
    {
        [Tooltip("Solo para leer la lista en el inspector.")]
        public string label;

        [Tooltip("Altura (local) de la línea donde apoyan las ruedas.")]
        public float y;

        [Tooltip("Sentido de circulación. Tildado = de izquierda a derecha.")]
        public bool towardsRight = true;

        [Tooltip("Escala de los vehículos. Menos de 1 = más lejos.")]
        [Min(0.1f)]
        public float scale = 1f;

        [Tooltip("Multiplica la velocidad del tipo. Lo lejano se ve pasar más lento: es el parallax.")]
        [Min(0f)]
        public float speedMultiplier = 1f;

        [Tooltip("Multiplica al tinte del paisaje: la bruma de la distancia. Blanco = igual que el paisaje.")]
        public Color haze = Color.white;

        [Tooltip("Orden de dibujo dentro del grupo. Las luces dibujan en el siguiente: dejarlo libre.")]
        public int sortingOrder;

        [Tooltip("Segundos entre un vehículo y el siguiente (mín, máx), con densidad 1.")]
        public Vector2 spawnInterval = new Vector2(5f, 14f);
    }

    /// <summary>Un vehículo en la ruta, o guardado (inactivo) para reusar.</summary>
    private class Vehicle
    {
        public GameObject gameObject;
        public SpriteRenderer body;
        public SpriteRenderer lights;
        public int laneIndex;
        public float speed;

        // Todo en el sentido de circulación, en unidades locales: x = ±distance.
        public float distance;
        public float back;   // cola respecto del pivote (negativo)
        public float front;  // punta del haz de luz respecto del pivote

        public float BackEdge => distance + back;
    }

    private class LaneState
    {
        public float timer;
        public Vehicle last;
    }

    /// <summary>Si el carril estaba ocupado al tocar el turno, cuánto esperar para volver a probar.</summary>
    private const float RetrySeconds = 0.5f;

    /// <summary>Paso de la simulación de arranque (<see cref="prewarmSeconds"/>).</summary>
    private const float PrewarmStep = 0.1f;

    [Header("Vehículos")]
    [SerializeField] private List<VehicleType> vehicleTypes = new List<VehicleType>();

    [Header("Ruta")]
    [SerializeField] private List<Lane> lanes = new List<Lane>();

    [Tooltip("Media longitud de la ruta, desde el centro de este objeto. Los vehículos aparecen y " +
             "desaparecen justo afuera. El Paisaje mide 19,2 u: 9,6.")]
    [Min(0f)]
    [SerializeField] private float roadHalfLength = 9.6f;

    [Tooltip("Espacio mínimo entre dos vehículos del mismo carril al entrar, en unidades.")]
    [Min(0f)]
    [SerializeField] private float minGap = 1.5f;

    [Tooltip("Cuánto tránsito hay según la hora (eje X: 0–24). 1 = el intervalo del carril tal cual; " +
             "0,5 = la mitad de seguido; 0 = no pasa nadie.")]
    [SerializeField] private AnimationCurve densityByHour = new AnimationCurve(
        new Keyframe(0f, 0.4f), new Keyframe(6f, 0.5f), new Keyframe(8f, 1f),
        new Keyframe(19f, 1f), new Keyframe(22f, 0.5f), new Keyframe(24f, 0.4f));

    [Header("Color y luces")]
    [Tooltip("La capa donde está dibujada la ruta (Paisaje). Vehículos y recortes toman su tinte a cada hora.")]
    [SerializeField] private DayCycleLayer tintSource;

    [Tooltip("Alfa de las luces según la hora (eje X: 0–24). 1 = prendidas.")]
    [SerializeField] private AnimationCurve lightsByHour = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(5.75f, 1f), new Keyframe(7.25f, 0f),
        new Keyframe(19f, 0f), new Keyframe(20f, 1f), new Keyframe(24f, 1f));

    [Tooltip("Recortes del paisaje que están delante de la ruta (el poste del cartel). Van dentro de " +
             "este objeto, con un orden mayor que el de los carriles. Se tiñen como el paisaje.")]
    [SerializeField] private SpriteRenderer[] occluders = new SpriteRenderer[0];

    [Header("Arranque")]
    [Tooltip("Segundos de tránsito que se simulan al cargar la escena, para que la ruta no arranque vacía. 0 = vacía.")]
    [Min(0f)]
    [SerializeField] private float prewarmSeconds = 12f;

    private readonly List<Vehicle> pool = new List<Vehicle>();
    private LaneState[] laneStates;
    private Material material;
    private bool prewarmed;

    private float hour = 12f;
    private Color tint = Color.white;
    private float lightsAlpha;

    private void Awake()
    {
        laneStates = new LaneState[lanes.Count];

        // Cada carril arranca en otro punto de su ciclo: que no entren todos a la vez.
        for (int i = 0; i < lanes.Count; i++)
            laneStates[i] = new LaneState { timer = UnityEngine.Random.Range(0f, lanes[i].spawnInterval.y) };

        if (tintSource != null)
            material = tintSource.GetComponent<SpriteRenderer>().sharedMaterial;
    }

    private void Start()
    {
        if (GetComponentInParent<DayCycleBackground>() == null)
            Debug.LogWarning("[RoadTraffic] '" + name + "' no está debajo de un DayCycleBackground: nadie le pasa la hora.", this);

        if (tintSource == null)
            Debug.LogWarning("[RoadTraffic] '" + name + "' no tiene tintSource: los vehículos no se oscurecen de noche.", this);
    }

    private void Update()
    {
        // El arranque va acá y no en Start: para el primer Update el DayCycleBackground ya pasó la
        // hora (lo hace en su Start), así los primeros vehículos salen con el color que corresponde.
        if (!prewarmed)
        {
            prewarmed = true;

            for (float t = 0f; t < prewarmSeconds; t += PrewarmStep)
                Step(PrewarmStep);
        }

        Step(Time.deltaTime);
    }

    public void ApplyHour(float hour)
    {
        this.hour = hour;
        tint = tintSource != null ? tintSource.ColorAt(hour) : Color.white;
        lightsAlpha = Mathf.Clamp01(lightsByHour.Evaluate(hour));

        for (int i = 0; i < occluders.Length; i++)
        {
            if (occluders[i] != null)
                occluders[i].color = tint;
        }

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].gameObject.activeSelf)
                ApplyLook(pool[i]);
        }
    }

    private void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        for (int i = 0; i < pool.Count; i++)
        {
            Vehicle vehicle = pool[i];

            if (!vehicle.gameObject.activeSelf)
                continue;

            vehicle.distance += vehicle.speed * deltaTime;

            if (vehicle.BackEdge > roadHalfLength)
                Despawn(vehicle);
            else
                Place(vehicle);
        }

        for (int i = 0; i < lanes.Count; i++)
            TickLane(i, deltaTime);
    }

    private void TickLane(int laneIndex, float deltaTime)
    {
        Lane lane = lanes[laneIndex];
        LaneState state = laneStates[laneIndex];

        state.timer -= deltaTime;

        if (state.timer > 0f)
            return;

        float density = densityByHour.Evaluate(hour);

        if (density <= 0.01f)
        {
            // A esta hora no pasa nadie: volver a mirar en un rato.
            state.timer = 1f;
            return;
        }

        state.timer = TrySpawn(laneIndex)
            ? UnityEngine.Random.Range(lane.spawnInterval.x, lane.spawnInterval.y) / density
            : RetrySeconds;
    }

    private bool TrySpawn(int laneIndex)
    {
        Lane lane = lanes[laneIndex];
        LaneState state = laneStates[laneIndex];
        Vehicle ahead = state.last;

        // La trompa del que entra arranca justo afuera de la ruta.
        float entry = -roadHalfLength;

        if (ahead != null && ahead.BackEdge - entry < minGap)
            return false;

        VehicleType type = PickType();

        if (type == null)
            return false;

        Sprite body = type.bodies[UnityEngine.Random.Range(0, type.bodies.Length)];

        if (body == null)
            return false;

        Vehicle vehicle = GetFromPool();
        vehicle.laneIndex = laneIndex;

        bool flip = !lane.towardsRight;
        vehicle.body.sprite = body;
        vehicle.body.flipX = flip;
        vehicle.body.sortingOrder = lane.sortingOrder;
        vehicle.lights.sprite = type.lights;
        vehicle.lights.flipX = flip;
        vehicle.lights.sortingOrder = lane.sortingOrder + 1;
        vehicle.gameObject.transform.localScale = new Vector3(lane.scale, lane.scale, 1f);

        // Los sprites miran a la derecha: en el sentido de circulación, min.x es la cola y max.x la
        // punta (con el haz de luz, si tiene). Espejado vale igual.
        Bounds bounds = body.bounds;

        if (type.lights != null)
            bounds.Encapsulate(type.lights.bounds);

        vehicle.back = bounds.min.x * lane.scale;
        vehicle.front = bounds.max.x * lane.scale;
        vehicle.distance = entry - vehicle.front;

        // Sin sobrepasos: con velocidades constantes, si no va más rápido que el de adelante nunca lo alcanza.
        vehicle.speed = UnityEngine.Random.Range(type.speed.x, type.speed.y) * lane.speedMultiplier;

        if (ahead != null)
            vehicle.speed = Mathf.Min(vehicle.speed, ahead.speed);

        vehicle.gameObject.SetActive(true);
        state.last = vehicle;

        Place(vehicle);
        ApplyLook(vehicle);
        return true;
    }

    private VehicleType PickType()
    {
        float total = 0f;

        for (int i = 0; i < vehicleTypes.Count; i++)
        {
            if (IsUsable(vehicleTypes[i]))
                total += vehicleTypes[i].weight;
        }

        if (total <= 0f)
            return null;

        float roll = UnityEngine.Random.value * total;
        VehicleType picked = null;

        for (int i = 0; i < vehicleTypes.Count; i++)
        {
            if (!IsUsable(vehicleTypes[i]))
                continue;

            picked = vehicleTypes[i];
            roll -= picked.weight;

            if (roll <= 0f)
                break;
        }

        return picked;
    }

    private static bool IsUsable(VehicleType type) =>
        type.weight > 0f && type.bodies != null && type.bodies.Length > 0;

    private void Place(Vehicle vehicle)
    {
        Lane lane = lanes[vehicle.laneIndex];
        float x = lane.towardsRight ? vehicle.distance : -vehicle.distance;
        vehicle.gameObject.transform.localPosition = new Vector3(x, lane.y, 0f);
    }

    private void ApplyLook(Vehicle vehicle)
    {
        vehicle.body.color = tint * lanes[vehicle.laneIndex].haze;

        bool lit = vehicle.lights.sprite != null && lightsAlpha > 0f;
        vehicle.lights.enabled = lit;

        if (lit)
            vehicle.lights.color = new Color(1f, 1f, 1f, lightsAlpha);
    }

    private Vehicle GetFromPool()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf)
                return pool[i];
        }

        var root = new GameObject("Vehiculo");
        root.transform.SetParent(transform, false);
        root.SetActive(false);

        var lightsObject = new GameObject("Luces");
        lightsObject.transform.SetParent(root.transform, false);

        var vehicle = new Vehicle
        {
            gameObject = root,
            body = root.AddComponent<SpriteRenderer>(),
            lights = lightsObject.AddComponent<SpriteRenderer>(),
        };

        if (material != null)
        {
            vehicle.body.sharedMaterial = material;
            vehicle.lights.sharedMaterial = material;
        }

        pool.Add(vehicle);
        return vehicle;
    }

    private void Despawn(Vehicle vehicle)
    {
        vehicle.gameObject.SetActive(false);

        LaneState state = laneStates[vehicle.laneIndex];

        if (state.last == vehicle)
            state.last = null;
    }

    private void OnDrawGizmosSelected()
    {
        // Los carriles a la vista, con una flecha hacia donde circulan, para acomodarlos contra la ruta del Paisaje.
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);

        foreach (Lane lane in lanes)
        {
            float direction = lane.towardsRight ? 1f : -1f;
            Vector3 from = transform.TransformPoint(new Vector3(-roadHalfLength * direction, lane.y, 0f));
            Vector3 to = transform.TransformPoint(new Vector3(roadHalfLength * direction, lane.y, 0f));
            Vector3 back = transform.TransformVector(new Vector3(-0.3f * direction, 0f, 0f));
            Vector3 up = transform.TransformVector(new Vector3(0f, 0.15f, 0f));

            Gizmos.DrawLine(from, to);
            Gizmos.DrawLine(to, to + back + up);
            Gizmos.DrawLine(to, to + back - up);
        }
    }
}
