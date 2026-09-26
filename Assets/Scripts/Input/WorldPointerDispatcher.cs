using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reemplazo de los OnMouseXXX de Unity para los colliders del mundo, alimentado por el
/// puntero de <see cref="InputManager"/> (mouse real o cursor virtual del gamepad).
///
/// Los OnMouseXXX nativos solo escuchan al mouse del sistema: con el cursor del gamepad
/// nunca se dispararían. Por eso los scripts ya no declaran OnMouseDown/Drag/Up/Enter/Exit
/// sino los mensajes de <see cref="Messages"/>, y este dispatcher los envía con la misma
/// semántica que el SendMouseEvents de Unity:
///
///  • Down  → al objeto bajo el puntero el frame en que se aprieta el botón principal.
///  • Drag  → cada frame con el botón apretado, SIEMPRE al objeto del Down (aunque el puntero ya no esté encima).
///  • Up    → al objeto del Down al soltar, esté donde esté el puntero. Click además si se soltó sobre el mismo objeto.
///  • Enter / Over / Exit → al cambiar el objeto bajo el puntero (Over cada frame que sigue encima).
///
/// Igual que el nativo: raycast 3D y 2D por separado (cada uno con su propio estado), respeta
/// <see cref="Camera.eventMask"/> (así <see cref="GamePause"/> apaga el mundo con eventMask = 0)
/// y entrega por SendMessage, así que el mensaje llega a todos los componentes del objeto.
/// </summary>
public sealed class WorldPointerDispatcher
{
    /// <summary>Nombres de los mensajes. Equivalen 1:1 a OnMouseDown, OnMouseDrag, etc.</summary>
    public static class Messages
    {
        public const string Down = "OnWorldPointerDown";
        public const string Drag = "OnWorldPointerDrag";
        public const string Up = "OnWorldPointerUp";
        /// <summary>Equivale a OnMouseUpAsButton: se soltó sobre el mismo objeto que se apretó.</summary>
        public const string Click = "OnWorldPointerClick";
        public const string Enter = "OnWorldPointerEnter";
        public const string Over = "OnWorldPointerOver";
        public const string Exit = "OnWorldPointerExit";
    }

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
                Send(hit, Messages.Down);
            }
        }
        else if (!held)
        {
            if (downHit[i] != null)
            {
                GameObject down = downHit[i];
                downHit[i] = null;

                if (hit == down)
                    Send(down, Messages.Click);
                Send(down, Messages.Up);
            }
        }
        else if (downHit[i] != null)
        {
            Send(downHit[i], Messages.Drag);
        }

        // Un objeto destruido compara igual a null: el Exit simplemente no sale, como en Unity.
        if (hit == lastHit[i])
        {
            if (hit != null)
                Send(hit, Messages.Over);
        }
        else
        {
            if (lastHit[i] != null)
                Send(lastHit[i], Messages.Exit);

            if (hit != null)
            {
                Send(hit, Messages.Enter);
                Send(hit, Messages.Over);
            }
        }

        lastHit[i] = hit;
    }

    private static void Send(GameObject target, string message)
    {
        if (target != null)
            target.SendMessage(message, SendMessageOptions.DontRequireReceiver);
    }
}
