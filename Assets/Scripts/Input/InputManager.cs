using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XInput;

/// <summary>
/// Única puerta de entrada del input del juego (Input System nuevo). Nadie más lee teclas,
/// botones del mouse ni gamepads: todos preguntan acá por intenciones.
///
///  • Puntero: <see cref="PointerPosition"/>, <see cref="PrimaryPressed"/>/<see cref="PrimaryHeld"/>/
///    <see cref="PrimaryReleased"/> (click izquierdo o A/✕) y <see cref="SecondaryPressed"/>
///    (click derecho o X/□). Con gamepad el puntero es virtual: el stick izquierdo / la cruceta
///    lo hacen saltar entre elementos (<see cref="GamepadNavigator"/>) y el stick derecho lo
///    mueve libre, para apuntar fino.
///  • Acciones: <see cref="WasPressed(GameAction)"/>. Los bindings viven en
///    <c>Assets/Input/GameControls.inputactions</c> (mapa "Gameplay").
///  • Colliders del mundo: <see cref="WorldPointerDispatcher"/> manda OnWorldPointerDown/Drag/Up/
///    Enter/Exit con el puntero de acá (reemplaza a los OnMouseXXX de Unity).
///  • UI (uGUI): el gamepad maneja un <see cref="Mouse"/> virtual, así que botones y hovers de
///    Canvas funcionan con el mismo cursor. El InputSystemUIInputModule de cada escena recibe
///    el mapa "UI" del asset (ver <see cref="ConfigureUIModule"/>).
///
/// Vive en <c>Resources/InputManager.prefab</c> y se instancia solo antes de cargar la primera
/// escena (DontDestroyOnLoad), así existe en todas las escenas sin setup.
/// Corre antes que cualquier otro script: el resto lee un estado ya resuelto para el frame.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class InputManager : MonoBehaviour
{
    private const string ResourcePath = "InputManager";
    private const string GameplayMapName = "Gameplay";
    private const string UIMapName = "UI";

    public static InputManager Instance { get; private set; }

    [SerializeField] private InputActionAsset actions;

    [Header("Cursor libre (stick derecho)")]
    [Tooltip("Velocidad con el stick a fondo, en alturas de pantalla por segundo.")]
    [SerializeField] private float cursorSpeed = 0.8f;
    [Tooltip("Curva de respuesta del stick: 1 = lineal; más alto = más precisión con el stick apenas inclinado.")]
    [SerializeField] [Range(1f, 4f)] private float cursorResponseExponent = 2f;

    [Header("Cambio de esquema")]
    [Tooltip("Inclinación mínima de un stick para que el gamepad tome el control del puntero.")]
    [SerializeField] [Range(0.05f, 0.9f)] private float stickActivationThreshold = 0.25f;
    [Tooltip("Píxeles que tiene que moverse el mouse en un frame para que recupere el control del puntero.")]
    [SerializeField] private float mouseActivationPixels = 3f;

    /// <summary>Cambió el esquema activo (mouse ↔ gamepad).</summary>
    public static event Action<InputScheme> OnSchemeChanged;

    /// <summary>Cambió la familia del gamepad en uso (Xbox ↔ PlayStation ↔ genérico): cambian los nombres de los botones.</summary>
    public static event Action<GamepadFamily> OnGamepadFamilyChanged;

    // ── API estática: los consumidores no necesitan chequear Instance ──

    /// <summary>Posición del puntero en píxeles de pantalla (origen abajo a la izquierda, z = 0). Reemplaza a Input.mousePosition.</summary>
    public static Vector3 PointerPosition => Instance != null ? (Vector3)Instance.pointerPosition : Vector3.zero;
    public static bool PrimaryPressed => Instance != null && Instance.primaryPressed;
    public static bool PrimaryHeld => Instance != null && Instance.primaryHeld;
    public static bool PrimaryReleased => Instance != null && Instance.primaryReleased;
    public static bool SecondaryPressed => Instance != null && Instance.secondaryPressed;

    public static InputScheme ActiveScheme => Instance != null ? Instance.scheme : InputScheme.KeyboardMouse;
    public static GamepadFamily ActiveGamepadFamily => Instance != null ? Instance.gamepadFamily : GamepadFamily.None;
    public static bool UsingGamepad => ActiveScheme == InputScheme.Gamepad;

    /// <summary>
    /// Ruta del control ligado a la acción del mapa Gameplay en ese esquema ("&lt;Keyboard&gt;/q",
    /// "&lt;Gamepad&gt;/leftShoulder"). La primera que aparezca; null si no hay o si todavía no existe
    /// el InputManager. La usa <see cref="InputPrompts"/> para nombrar teclas y botones en los textos.
    /// </summary>
    public static string GetBindingPath(string actionName, InputScheme scheme)
    {
        if (Instance == null || Instance.actions == null) return null;

        InputAction action = Instance.actions.FindActionMap(GameplayMapName)?.FindAction(actionName);
        if (action == null) return null;

        string group = scheme == InputScheme.Gamepad ? "Gamepad" : "KeyboardMouse";
        foreach (InputBinding binding in action.bindings)
        {
            if (binding.isComposite || binding.isPartOfComposite) continue;
            if (binding.groups == null || Array.IndexOf(binding.groups.Split(InputBinding.Separator), group) < 0) continue;
            if (binding.effectivePath.StartsWith("<Joystick>")) continue;
            return binding.effectivePath;
        }
        return null;
    }

    /// <summary>Tipo de control elegido en Opciones (lo aplica <see cref="GameSettings"/>).</summary>
    public static InputMode Mode => Instance != null ? Instance.inputMode : InputMode.Auto;

    /// <summary>
    /// Restringe el input a un esquema: <see cref="InputMode.KeyboardMouse"/> apaga los gamepads y
    /// <see cref="InputMode.Gamepad"/> apaga el mouse real (el teclado sigue andando en los dos, como
    /// en Auto). Se hace deshabilitando dispositivos, así también la UI deja de verlos. Solo gamepad
    /// sin ninguno conectado deja el mouse prendido hasta que se conecte uno.
    /// </summary>
    public static void SetInputMode(InputMode mode)
    {
        // Sin instancia no hay nada que aplicar: Awake lo lee de GameSettings.
        if (Instance == null) return;

        Instance.inputMode = mode;
        Instance.ApplyInputMode();
    }

    /// <summary>Hay un elemento seleccionado con la navegación del gamepad.</summary>
    public static bool HasNavFocus => Instance != null && Instance.scheme == InputScheme.Gamepad && Instance.navigator.HasFocus;
    /// <summary>Elemento seleccionado (recuadro en pantalla, y si es un destino para soltar). Válido si <see cref="HasNavFocus"/>.</summary>
    public static NavTarget NavFocus => Instance != null ? Instance.navigator.Focus : default;
    /// <summary>
    /// Con gamepad y un hueco o bloque de la parrilla seleccionado como destino del arrastre:
    /// centro en mundo donde va a quedar la pieza. Los draggables con agarre desplazado lo usan
    /// para dibujar la carne exactamente sobre los slots que va a ocupar.
    /// </summary>
    public static bool TryGetGridSnap(out Vector3 worldCenter)
    {
        worldCenter = default;
        if (!HasNavFocus) return false;

        NavTarget focus = Instance.navigator.Focus;
        if (!focus.isDropTarget || (focus.kind != NavTarget.Kind.GridBlock && focus.kind != NavTarget.Kind.GridSlot))
            return false;

        worldCenter = focus.worldAnchor;
        return true;
    }

    /// <summary>Con gamepad, el puntero se está moviendo libre (stick derecho) y hay que dibujar la flecha.</summary>
    public static bool FreeCursorActive => Instance != null && Instance.scheme == InputScheme.Gamepad && Instance.freeCursor;

    /// <summary>La acción se apretó este frame (flanco, como Input.GetKeyDown).</summary>
    public static bool WasPressed(GameAction action)
    {
        if (Instance == null) return false;
        InputAction a = Instance.gameActions[(int)action];
        return a != null && a.WasPressedThisFrame();
    }

    // ── Estado ──
    private InputAction[] gameActions;
    private InputAction cursorMoveAction;
    private InputAction navigateAction;
    private InputAction pointerPrimaryAction;
    private InputAction pointerSecondaryAction;
    private InputActionMap uiMap;

    private InputScheme scheme = InputScheme.KeyboardMouse;
    private GamepadFamily gamepadFamily = GamepadFamily.None;
    private InputMode inputMode = InputMode.Auto;

    private Mouse realMouse;
    private Mouse virtualMouse;
    private Vector2 virtualPosition;
    private Vector2 lastVirtualPosition;
    private bool virtualLeft, virtualRight;

    private Vector2 pointerPosition;
    private bool primaryPressed, primaryHeld, primaryReleased, secondaryPressed;

    private readonly WorldPointerDispatcher worldPointer = new WorldPointerDispatcher();
    private readonly GamepadNavigator navigator = new GamepadNavigator();
    private bool freeCursor;
    private EventSystem configuredEventSystem;
    private IDisposable anyButtonListener;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        OnSchemeChanged = null;
        OnGamepadFamilyChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        InputManager prefab = Resources.Load<InputManager>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[InputManager] Falta Resources/{ResourcePath}.prefab: el juego no va a recibir input.");
            return;
        }

        Instantiate(prefab).name = prefab.name;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (actions == null)
        {
            Debug.LogError("[InputManager] No hay InputActionAsset asignado.");
            enabled = false;
            return;
        }

        BindActions();

        virtualMouse = InputSystem.AddDevice<Mouse>("VirtualCursorMouse");
        realMouse = FindRealMouse();
        pointerPosition = realMouse != null ? realMouse.position.ReadValue() : Vector2.zero;

        // Sin mouse ni teclado nativos, el backend del Input System está apagado: pasa cuando se
        // cambió 'Active Input Handling' y el Editor todavía no se reinició. No hay input posible.
        if (realMouse == null && Keyboard.current == null)
            Debug.LogError("[InputManager] El Input System no ve ningún mouse ni teclado. Si se acaba de cambiar " +
                           "'Player Settings > Active Input Handling', hay que reiniciar Unity.");

        InputSystem.onDeviceChange += HandleDeviceChange;
        anyButtonListener = InputSystem.onAnyButtonPress.Call(HandleAnyButtonPress);

        inputMode = GameSettings.Current.inputMode;
        ApplyInputMode();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        InputSystem.onDeviceChange -= HandleDeviceChange;
        // Los dispositivos apagados por el tipo de control quedarían apagados en el Editor al salir de Play.
        SetExclusiveDevicesEnabled(padsOn: true, mouseOn: true);
        anyButtonListener?.Dispose();
        actions?.FindActionMap(GameplayMapName)?.Disable();

        if (virtualMouse != null && virtualMouse.added)
            InputSystem.RemoveDevice(virtualMouse);

        Cursor.visible = true;
        Instance = null;
    }

    private void BindActions()
    {
        InputActionMap gameplay = actions.FindActionMap(GameplayMapName, throwIfNotFound: true);

        GameAction[] values = (GameAction[])Enum.GetValues(typeof(GameAction));
        gameActions = new InputAction[values.Length];
        foreach (GameAction value in values)
        {
            gameActions[(int)value] = gameplay.FindAction(value.ToString());
            if (gameActions[(int)value] == null)
                Debug.LogError($"[InputManager] El mapa '{GameplayMapName}' no tiene la acción '{value}'.");
        }

        cursorMoveAction = gameplay.FindAction("CursorMove", throwIfNotFound: true);
        navigateAction = gameplay.FindAction("Navigate", throwIfNotFound: true);
        pointerPrimaryAction = gameplay.FindAction("PointerPrimary", throwIfNotFound: true);
        pointerSecondaryAction = gameplay.FindAction("PointerSecondary", throwIfNotFound: true);
        uiMap = actions.FindActionMap(UIMapName, throwIfNotFound: true);

        gameplay.Enable();
    }

    private void Update()
    {
        UpdateScheme();
        UpdatePointer();
        FeedVirtualMouse();
        EnsureUIModule();

        worldPointer.Tick(pointerPosition, primaryPressed, primaryHeld);
    }

    // ── Esquema activo ──

    private void UpdateScheme()
    {
        if (scheme == InputScheme.KeyboardMouse)
        {
            if (cursorMoveAction.ReadValue<Vector2>().magnitude >= stickActivationThreshold)
                SetScheme(InputScheme.Gamepad, cursorMoveAction.activeControl?.device);
            else if (navigateAction.ReadValue<Vector2>().magnitude >= stickActivationThreshold)
                SetScheme(InputScheme.Gamepad, navigateAction.activeControl?.device);
        }
        else if (realMouse != null)
        {
            bool moved = realMouse.delta.ReadValue().sqrMagnitude >= mouseActivationPixels * mouseActivationPixels;
            bool scrolled = realMouse.scroll.ReadValue().sqrMagnitude > 0f;
            if (moved || scrolled)
                SetScheme(InputScheme.KeyboardMouse, realMouse);
        }
    }

    private void HandleAnyButtonPress(InputControl control)
    {
        InputDevice device = control.device;
        if (device == virtualMouse) return;

        if (device is Gamepad || device is Joystick)
            SetScheme(InputScheme.Gamepad, device);
        else if (device is Mouse)
            SetScheme(InputScheme.KeyboardMouse, device);
        // El teclado no cambia el esquema: se puede apretar una tecla con el cursor del gamepad.
    }

    private void SetScheme(InputScheme next, InputDevice device)
    {
        if (!IsSchemeAllowed(next)) return;

        if (next == InputScheme.Gamepad && device != null)
        {
            GamepadFamily family = GetFamily(device);
            if (family != gamepadFamily)
            {
                gamepadFamily = family;
                OnGamepadFamilyChanged?.Invoke(family);
            }
        }

        if (next == scheme) return;

        if (next == InputScheme.Gamepad)
        {
            // El cursor virtual arranca donde estaba el mouse: el puntero no salta.
            virtualPosition = ClampToScreen(pointerPosition);
            lastVirtualPosition = virtualPosition;
            freeCursor = false;
        }

        scheme = next;
        Cursor.visible = next == InputScheme.KeyboardMouse;
        OnSchemeChanged?.Invoke(next);
    }

    private bool IsSchemeAllowed(InputScheme next)
    {
        switch (inputMode)
        {
            case InputMode.KeyboardMouse: return next == InputScheme.KeyboardMouse;
            case InputMode.Gamepad: return next == InputScheme.Gamepad || !AnyPadConnected();
            default: return true;
        }
    }

    // ── Tipo de control ──

    private void ApplyInputMode()
    {
        bool anyPad = AnyPadConnected();
        bool padsOn = inputMode != InputMode.KeyboardMouse;
        bool mouseOn = inputMode != InputMode.Gamepad || !anyPad;

        SetExclusiveDevicesEnabled(padsOn, mouseOn);

        if (!padsOn)
            SetScheme(InputScheme.KeyboardMouse, realMouse);
        else if (!mouseOn)
            SetScheme(InputScheme.Gamepad, FirstPad());
    }

    /// <summary>Prende o apaga los gamepads/joysticks y los mouse reales. El mouse virtual y el teclado no se tocan.</summary>
    private void SetExclusiveDevicesEnabled(bool padsOn, bool mouseOn)
    {
        foreach (InputDevice device in InputSystem.devices.ToArray())
        {
            if (IsPad(device))
                SetDeviceEnabled(device, padsOn);
            else if (device is Mouse && device != virtualMouse)
                SetDeviceEnabled(device, mouseOn);
        }
    }

    private static void SetDeviceEnabled(InputDevice device, bool enabled)
    {
        if (device.enabled == enabled) return;

        if (enabled) InputSystem.EnableDevice(device);
        else InputSystem.DisableDevice(device);
    }

    private static bool IsPad(InputDevice device) => device is Gamepad || device is Joystick;

    private static bool AnyPadConnected() => Gamepad.all.Count > 0 || Joystick.all.Count > 0;

    private static InputDevice FirstPad()
    {
        if (Gamepad.all.Count > 0) return Gamepad.all[0];
        return Joystick.all.Count > 0 ? Joystick.all[0] : null;
    }

    private static GamepadFamily GetFamily(InputDevice device)
    {
        // DualSense deriva de DualShockGamepad: PS4 y PS5 caen acá.
        if (device is DualShockGamepad) return GamepadFamily.PlayStation;
        if (device is XInputController) return GamepadFamily.Xbox;
        return GamepadFamily.Generic;
    }

    // ── Puntero ──

    private void UpdatePointer()
    {
        bool padPrimaryDown = pointerPrimaryAction.WasPressedThisFrame();
        bool padPrimaryUp = pointerPrimaryAction.WasReleasedThisFrame();
        bool padPrimaryHeld = pointerPrimaryAction.IsPressed();

        bool mousePrimaryDown = false, mousePrimaryUp = false, mousePrimaryHeld = false, mouseSecondaryDown = false;
        if (realMouse != null)
        {
            mousePrimaryDown = realMouse.leftButton.wasPressedThisFrame;
            mousePrimaryUp = realMouse.leftButton.wasReleasedThisFrame;
            mousePrimaryHeld = realMouse.leftButton.isPressed;
            mouseSecondaryDown = realMouse.rightButton.wasPressedThisFrame;
        }

        bool wasHeld = primaryHeld;
        primaryHeld = mousePrimaryHeld || padPrimaryHeld;
        // Los flancos del frame cubren un click que entra y sale dentro del mismo frame.
        primaryPressed = mousePrimaryDown || padPrimaryDown || (primaryHeld && !wasHeld);
        primaryReleased = !primaryHeld && (wasHeld || mousePrimaryUp || padPrimaryUp);
        secondaryPressed = mouseSecondaryDown || pointerSecondaryAction.WasPressedThisFrame();

        bool usingPad = scheme == InputScheme.Gamepad;

        if (usingPad)
        {
            // Stick derecho: cursor libre. Suelta la selección y dibuja la flecha.
            Vector2 stick = cursorMoveAction.ReadValue<Vector2>();
            float magnitude = Mathf.Clamp01(stick.magnitude);
            if (magnitude > 0f)
            {
                // unscaledDeltaTime: el cursor se mueve también con el juego en pausa.
                float speed = Mathf.Pow(magnitude, cursorResponseExponent) * cursorSpeed * Screen.height;
                virtualPosition = ClampToScreen(virtualPosition + stick / magnitude * speed * Time.unscaledDeltaTime);
                navigator.ClearFocus();
                freeCursor = true;
            }
        }

        // Stick izquierdo / cruceta: saltos entre elementos. Corre también con mouse para
        // seguir el estado de los paneles.
        bool jumped = navigator.Tick(usingPad, navigateAction.ReadValue<Vector2>(), virtualPosition,
                                     primaryHeld, primaryReleased, worldPointer.PressedObject);
        if (jumped)
            freeCursor = false;

        if (usingPad)
        {
            if (navigator.HasFocus)
                virtualPosition = ClampToScreen(navigator.Focus.point);
            pointerPosition = virtualPosition;
        }
        else if (realMouse != null)
        {
            pointerPosition = realMouse.position.ReadValue();
        }
    }

    private static Vector2 ClampToScreen(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, 0f, Mathf.Max(0f, Screen.width - 1)),
            Mathf.Clamp(position.y, 0f, Mathf.Max(0f, Screen.height - 1)));
    }

    /// <summary>
    /// Copia el cursor virtual al <see cref="Mouse"/> virtual para que la UI (EventSystem) lo
    /// trate como un mouse más: hover, click y scroll de Canvas sin código extra. Va por evento
    /// en cola, así que la UI lo ve en el frame siguiente.
    /// </summary>
    private void FeedVirtualMouse()
    {
        if (virtualMouse == null) return;

        bool usingPad = scheme == InputScheme.Gamepad;
        bool left = usingPad && pointerPrimaryAction.IsPressed();
        bool right = usingPad && pointerSecondaryAction.IsPressed();
        bool moved = usingPad && virtualPosition != lastVirtualPosition;

        if (!moved && left == virtualLeft && right == virtualRight)
            return;

        MouseState state = new MouseState
        {
            position = usingPad ? virtualPosition : lastVirtualPosition,
            delta = moved ? virtualPosition - lastVirtualPosition : Vector2.zero,
        }
        .WithButton(MouseButton.Left, left)
        .WithButton(MouseButton.Right, right);

        InputSystem.QueueStateEvent(virtualMouse, state);

        if (usingPad)
            lastVirtualPosition = virtualPosition;
        virtualLeft = left;
        virtualRight = right;
    }

    // ── Dispositivos ──

    private Mouse FindRealMouse()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Mouse mouse && mouse != virtualMouse)
                return mouse;
        }
        return null;
    }

    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is Mouse && device != virtualMouse)
            realMouse = FindRealMouse();

        // Un dispositivo nuevo respeta el tipo de control; en "solo gamepad", conectar o sacar el
        // último gamepad apaga o devuelve el mouse.
        bool plugged = change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected
                       || change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;
        if (plugged && inputMode != InputMode.Auto && (IsPad(device) || device is Mouse) && device != virtualMouse)
            ApplyInputMode();

        // Se desconectó el gamepad con el que se jugaba: el mouse recupera el puntero.
        if (change == InputDeviceChange.Removed && scheme == InputScheme.Gamepad
            && (device is Gamepad || device is Joystick) && Gamepad.all.Count == 0 && Joystick.all.Count == 0)
        {
            SetScheme(InputScheme.KeyboardMouse, realMouse);
        }
    }

    // ── UI ──

    /// <summary>
    /// Deja el EventSystem activo con un <see cref="InputSystemUIInputModule"/> que usa el mapa
    /// "UI" del asset del juego. Las escenas ya vienen con ese módulo; esto cubre los que se
    /// crean en runtime (TutorialManager) y cualquier StandaloneInputModule que haya quedado.
    /// </summary>
    private void EnsureUIModule()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem == configuredEventSystem)
            return;

        configuredEventSystem = eventSystem;

        StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Debug.LogWarning($"[InputManager] '{eventSystem.name}' tenía un StandaloneInputModule (input viejo): se reemplaza por InputSystemUIInputModule.", eventSystem);
            legacy.enabled = false;
            Destroy(legacy);
        }

        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null)
            module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        ConfigureUIModule(module);
    }

    /// <summary>
    /// Asigna las acciones del mapa "UI" al módulo. Mismo asset para todas las escenas: así el
    /// gamepad no dispara Submit sobre un botón que quedó seleccionado mientras se juega.
    /// </summary>
    public void ConfigureUIModule(InputSystemUIInputModule module)
    {
        if (module == null || uiMap == null) return;
        if (module.actionsAsset == actions && module.point != null && module.point.action?.actionMap == uiMap)
            return;

        bool wasEnabled = module.enabled;
        module.enabled = false;

        module.actionsAsset = actions;
        module.point = Reference("Point");
        module.leftClick = Reference("Click");
        module.rightClick = Reference("RightClick");
        module.middleClick = Reference("MiddleClick");
        module.scrollWheel = Reference("ScrollWheel");
        module.move = Reference("Navigate");
        module.submit = Reference("Submit");
        module.cancel = Reference("Cancel");
        module.trackedDevicePosition = null;
        module.trackedDeviceOrientation = null;

        module.enabled = wasEnabled;
    }

    private InputActionReference Reference(string actionName)
    {
        InputAction action = uiMap.FindAction(actionName);
        return action != null ? InputActionReference.Create(action) : null;
    }
}
