using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reemplazo de los OnMouseXXX de Unity para los colliders del mundo, alimentado por el
/// puntero de <see cref="InputManager"/> (mouse real o cursor virtual del gamepad).
///
/// Los OnMouseXXX nativos solo escuchan al mouse del sistema: con el cursor del gamepad
/// nunca se dispararían. Por eso los scripts ya no declaran OnMouseDown/Drag/Up/Enter/Exit
/// sino que implementan las interfaces IWorldPointerXXXHandler (ver WorldPointerHandlers.cs),
/// y este dispatcher las llama con la misma semántica que el SendMouseEvents de Unity:
///
///  • Down  → al objeto bajo el puntero el frame en que se aprieta el botón principal.
///  • Drag  → cada frame con el botón apretado, SIEMPRE al objeto del Down (aunque el puntero ya no esté encima).
///  • Up    → al objeto del Down al soltar, esté donde esté el puntero. Click además si se soltó sobre el mismo objeto.
///  • Enter / Over / Exit → al cambiar el objeto bajo el puntero (Over cada frame que sigue encima).
///
/// Igual que el nativo: raycast 3D y 2D por separado (cada uno con su propio estado), respeta
/// <see cref="Camera.eventMask"/> (así <see cref="GamePause"/> apaga el mundo con eventMask = 0)
/// y el evento llega a todos los componentes del objeto que implementen la interfaz.
/// </summary>
public sealed class WorldPointerDispatcher
{
    private const int Physics3D = 0;
    private const int Physics2DIndex = 1;
    private const int HitKinds = 2;

    private readonly GameObject[] currentHit = new GameObject[HitKinds];
    private readonly GameObject[] lastHit = new GameObject[HitKinds];
    private readonly GameObject[] downHit = new GameObject[HitKinds];

    private Camera[] cameras = new Camera[4];

    /// <summary>
    /// Objeto que recibió el Down del arrastre en curso (null si no hay botón apretado sobre
    /// algo). Es lo que se está agarrando: la navegación con gamepad decide los destinos por esto.
    /// </summary>
    public GameObject PressedObject => downHit[Physics2DIndex] != null ? downHit[Physics2DIndex] : downHit[Physics3D];
    private static readonly IComparer<Camera> ByDepth =
        Comparer<Camera>.Create((a, b) => a.depth.CompareTo(b.depth));

    /// <summary>
    /// Un frame de eventos. Se llama una sola vez por frame, antes de los Update del resto
    /// (InputManager corre primero), igual que el SendMouseEvents nativo.
    /// </summary>
    public void Tick(Vector2 screenPosition, bool pressedThisFrame, bool held)
    {
        Raycast(screenPosition);

        for (int i = 0; i < HitKinds; i++)
            SendEvents(i, pressedThisFrame, held);
    }

    /// <summary>Suelta todo sin mandar Up (el objeto ya no existe o se reinició la escena).</summary>
    public void Reset()
    {
        for (int i = 0; i < HitKinds; i++)
        {
            currentHit[i] = null;
            lastHit[i] = null;
            downHit[i] = null;
        }
    }

    private void Raycast(Vector2 screenPosition)
    {
        for (int i = 0; i < HitKinds; i++)
            currentHit[i] = null;

        int count = Camera.allCamerasCount;
        if (cameras.Length < count)
            cameras = new Camera[count];

        Camera.GetAllCameras(cameras);
        System.Array.Sort(cameras, 0, count, ByDepth);

        for (int c = 0; c < count; c++)
        {
            Camera cam = cameras[c];
            if (cam == null || cam.targetTexture != null)
                continue;

            if (!cam.pixelRect.Contains(screenPosition))
                continue;

            // Sin eventMask la cámara no manda eventos (así pausa GamePause al mundo).
            if (cam.eventMask == 0)
                continue;

            Ray ray = cam.ScreenPointToRay(screenPosition);
            float dirZ = ray.direction.z;
            float distance = Mathf.Approximately(0f, dirZ)
                ? Mathf.Infinity
                : Mathf.Abs((cam.farClipPlane - cam.nearClipPlane) / dirZ);
            int mask = cam.cullingMask & cam.eventMask;
            bool clearsBackground = cam.clearFlags == CameraClearFlags.Skybox
                                 || cam.clearFlags == CameraClearFlags.SolidColor;

            if (Physics.Raycast(ray, out RaycastHit hit3D, distance, mask))
                currentHit[Physics3D] = hit3D.rigidbody != null ? hit3D.rigidbody.gameObject : hit3D.collider.gameObject;
            else if (clearsBackground)
                currentHit[Physics3D] = null;

            RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray, distance, mask);
            if (hit2D.collider != null)
                currentHit[Physics2DIndex] = hit2D.rigidbody != null ? hit2D.rigidbody.gameObject : hit2D.collider.gameObject;
            else if (clearsBackground)
                currentHit[Physics2DIndex] = null;
        }

        System.Array.Clear(cameras, 0, count);
    }

    private void SendEvents(int i, bool pressedThisFrame, bool held)
    {
        GameObject hit = currentHit[i];

        if (pressedThisFrame)
        {
            if (hit != null)
            {
                downHit[i] = hit;
                Send<IWorldPointerDownHandler>(hit, h => h.OnWorldPointerDown());
            }
        }
        else if (!held)
        {
            if (downHit[i] != null)
            {
                GameObject down = downHit[i];
                downHit[i] = null;

                if (hit == down)
                    Send<IWorldPointerClickHandler>(down, h => h.OnWorldPointerClick());
                Send<IWorldPointerUpHandler>(down, h => h.OnWorldPointerUp());
            }
        }
        else if (downHit[i] != null)
        {
            Send<IWorldPointerDragHandler>(downHit[i], h => h.OnWorldPointerDrag());
        }

        // Un objeto destruido compara igual a null: el Exit simplemente no sale, como en Unity.
        if (hit == lastHit[i])
        {
            if (hit != null)
                Send<IWorldPointerOverHandler>(hit, h => h.OnWorldPointerOver());
        }
        else
        {
            if (lastHit[i] != null)
                Send<IWorldPointerExitHandler>(lastHit[i], h => h.OnWorldPointerExit());

            if (hit != null)
            {
                Send<IWorldPointerEnterHandler>(hit, h => h.OnWorldPointerEnter());
                Send<IWorldPointerOverHandler>(hit, h => h.OnWorldPointerOver());
            }
        }

        lastHit[i] = hit;
    }

    /// <summary>
    /// Llama al evento en todos los componentes del objeto que lo implementan, igual que el
    /// SendMessage de antes: nada si el objeto está apagado, y una excepción en un componente
    /// no corta a los demás. La lista sale de un pool: un handler puede disparar otro evento.
    /// </summary>
    private static void Send<T>(GameObject target, System.Action<T> call) where T : class
    {
        if (target == null || !target.activeInHierarchy)
            return;

        using (UnityEngine.Pool.ListPool<T>.Get(out List<T> handlers))
        {
            target.GetComponents(handlers);
            for (int i = 0; i < handlers.Count; i++)
            {
                try
                {
                    call(handlers[i]);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e, handlers[i] as Object);
                }
            }
        }
    }
}
