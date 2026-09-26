using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Selección por saltos con gamepad: el stick izquierdo / la cruceta mueven la selección al
/// elemento más cercano en esa dirección (qué es seleccionable lo decide
/// <see cref="GamepadNavTargets"/>). <see cref="InputManager"/> apoya el puntero sobre el
/// elemento seleccionado, así que agarrar, soltar y clickear siguen pasando por el mismo
/// camino que con el mouse.
///
///  • Al apretar A la selección se suelta (lo agarrado ya no es destino) y el puntero queda quieto.
///  • Arrastrando, los saltos recorren solo lugares donde soltar.
///  • Al empezar a arrastrar carne se selecciona el bloque donde está (si es de la parrilla), y
///    al rotarla se busca el bloque más cercano con el nuevo footprint.
///  • Al soltar A se re-selecciona lo que quedó bajo el puntero (p. ej. la carne recién puesta).
///  • Al abrir un panel lateral se selecciona su primer elemento.
/// </summary>
public sealed class GamepadNavigator
{
    private const float PressThreshold = 0.5f;
    private const float ReleaseThreshold = 0.35f;
    private const float FirstRepeatDelay = 0.32f;
    private const float RepeatInterval = 0.12f;
    /// <summary>Radio (en alturas de pantalla) para re-seleccionar lo que quedó bajo el puntero.</summary>
    private const float RefocusRadius = 0.08f;
    /// <summary>Radio para re-encontrar el bloque de la carne tras rotarla (el footprint cambió de forma).</summary>
    private const float RotateRefocusRadius = 0.25f;
    private const float PanelFocusWindow = 0.6f;

    private readonly List<NavTarget> candidates = new List<NavTarget>();

    private NavTarget focus;
    private bool hasFocus;

    private bool navHeld;
    private float nextRepeatTime;
    private bool refocusPending;
    private GamepadNavTargets.DragKind lastDrag;

    private bool stockWasOpen, toppingsWasOpen;
    private SlidingPanel panelToFocus;
    private float panelFocusDeadline;

    public bool HasFocus => hasFocus;
    public NavTarget Focus => focus;

    public void ClearFocus()
    {
        hasFocus = false;
        refocusPending = false;
        panelToFocus = null;
    }

    /// <summary>
    /// Un frame. Devuelve true si la selección saltó por input de navegación este frame.
    /// Con <paramref name="active"/> en false (se juega con mouse) solo sigue el estado de los
    /// paneles y no selecciona nada.
    /// </summary>
    public bool Tick(bool active, Vector2 navInput, Vector2 pointer, bool holding, bool releasedThisFrame, GameObject pressed)
    {
        if (!active)
        {
            ClearFocus();
            TrackPanels(true, GamepadNavTargets.DragKind.None, null);
            return false;
        }

        GamepadNavTargets.DragKind drag = holding
            ? GamepadNavTargets.DetectDrag(pressed)
            : GamepadNavTargets.DragKind.None;

        if (hasFocus && !GamepadNavTargets.Refresh(ref focus, holding, drag, pressed))
        {
            bool wasBlock = focus.kind == NavTarget.Kind.GridBlock;
            hasFocus = false;
            // Se fue lo seleccionado sin que se agarrara nada (panel cerrado, cliente que se fue):
            // buscar lo más cercano para no quedar a la deriva.
            if (!holding) refocusPending = true;
            // Se rotó la carne: el bloque viejo ya no es su footprint, buscar el nuevo al lado.
            else if (wasBlock) FocusNearest(pointer, true, drag, pressed, RotateRefocusRadius);
        }

        // Arrancó un arrastre: si lo agarrado ya está sobre un destino (la carne en su bloque
        // de la parrilla), se selecciona ese destino para que el recuadro aparezca de entrada.
        if (holding && drag != GamepadNavTargets.DragKind.None && lastDrag == GamepadNavTargets.DragKind.None && !hasFocus)
            FocusNearest(pointer, true, drag, pressed, RefocusRadius);
        lastDrag = drag;

        if (releasedThisFrame)
            refocusPending = true;
        else if (refocusPending && !holding)
        {
            refocusPending = false;
            if (!hasFocus)
                FocusNearest(pointer, false, drag, null, RefocusRadius);
        }

        TrackPanels(holding, drag, pressed);

        return HandleNavigation(navInput, pointer, holding, drag, pressed);
    }

    private bool HandleNavigation(Vector2 navInput, Vector2 pointer, bool holding, GamepadNavTargets.DragKind drag, GameObject pressed)
    {
        float magnitude = navInput.magnitude;
        if (magnitude < (navHeld ? ReleaseThreshold : PressThreshold))
        {
            navHeld = false;
            return false;
        }

        float now = Time.unscaledTime;
        if (navHeld && now < nextRepeatTime)
            return false;

        nextRepeatTime = now + (navHeld ? RepeatInterval : FirstRepeatDelay);
        navHeld = true;

        GamepadNavTargets.Collect(candidates, holding, drag, pressed);
        Vector2 origin = hasFocus ? focus.point : pointer;
        Object exclude = hasFocus ? focus.key : null;

        if (!GamepadNavTargets.TryPickInDirection(candidates, origin, navInput, exclude, out NavTarget next))
            return false;

        focus = next;
        hasFocus = true;
        refocusPending = false;
        panelToFocus = null;
        return true;
    }

    private void FocusNearest(Vector2 pointer, bool holding, GamepadNavTargets.DragKind drag, GameObject pressed, float radius)
    {
        GamepadNavTargets.Collect(candidates, holding, drag, pressed);
        if (GamepadNavTargets.TryPickNearest(candidates, pointer, radius * Screen.height, out NavTarget nearest))
        {
            focus = nearest;
            hasFocus = true;
        }
    }

    /// <summary>Al abrirse un panel lateral, selecciona su primer elemento (arriba a la izquierda).</summary>
    private void TrackPanels(bool holding, GamepadNavTargets.DragKind drag, GameObject pressed)
    {
        StockPanelController stock = StockPanelController.Instance;
        ToppingsPanelController toppings = ToppingsPanelController.Instance;
        bool stockOpen = stock != null && stock.IsOpen;
        bool toppingsOpen = toppings != null && toppings.IsOpen;

        if (stockOpen && !stockWasOpen) BeginPanelFocus(stock);
        if (toppingsOpen && !toppingsWasOpen) BeginPanelFocus(toppings);
        stockWasOpen = stockOpen;
        toppingsWasOpen = toppingsOpen;

        if (panelToFocus == null) return;

        if (holding || !panelToFocus.IsOpen || Time.unscaledTime > panelFocusDeadline)
        {
            panelToFocus = null;
            return;
        }

        // Mientras desliza, sus elementos pueden estar todavía fuera de pantalla: se reintenta.
        GamepadNavTargets.Collect(candidates, false, drag, pressed);
        bool found = false;
        NavTarget best = default;
        for (int i = 0; i < candidates.Count; i++)
        {
            NavTarget c = candidates[i];
            Component component = c.key as Component;
            if (component == null || !component.transform.IsChildOf(panelToFocus.transform))
                continue;
            if (component.GetComponent<StockPanelTab>() != null)
                continue;

            // Arriba a la izquierda: primero la fila más alta, después la columna más a la izquierda.
            if (!found || c.point.y > best.point.y + 1f
                || (Mathf.Abs(c.point.y - best.point.y) <= 1f && c.point.x < best.point.x))
            {
                best = c;
                found = true;
            }
        }

        if (!found) return;

        focus = best;
        hasFocus = true;
        refocusPending = false;
        panelToFocus = null;
    }

    private void BeginPanelFocus(SlidingPanel panel)
    {
        panelToFocus = panel;
        panelFocusDeadline = Time.unscaledTime + PanelFocusWindow;
    }
}
