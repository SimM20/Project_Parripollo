using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Piezas compartidas por las secuencias del menú principal (<see cref="MainMenuIntro"/>,
/// <see cref="MainMenuOutro"/>): progreso de un tramo, redondeo a la grilla del arte, velo a
/// pantalla completa y la entrada que salta la secuencia. Sin estado.
/// </summary>
public static class MenuSequence
{
    /// <summary>TMP_Text.maxVisibleCharacters por defecto: todos los caracteres visibles.</summary>
    public const int AllCharacters = 99999;

    /// <summary>
    /// Tope del delta por frame: los primeros frames de una escena traen el pico de carga y ese
    /// salto no se tiene que comer media secuencia (igual que SceneFadeIn).
    /// </summary>
    public const float MaxFrameStep = 1f / 30f;

    /// <summary>Avance 0–1 de un tramo que empieza en <paramref name="start"/>. Duración 0 = corte.</summary>
    public static float Progress(float t, float start, float seconds) =>
        seconds > 0f ? Mathf.Clamp01((t - start) / seconds) : (t >= start ? 1f : 0f);

    /// <summary>Redondea un desplazamiento a múltiplos de <paramref name="step"/>. 0 = sin redondeo.</summary>
    public static float Snap(float value, float step) =>
        step > 0f ? Mathf.Round(value / step) * step : value;

    public static CanvasGroup GetOrAddGroup(Component target)
    {
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Velo a pantalla completa, último hijo del canvas: tapa fondo y menú. No recibe raycasts
    /// (el bloqueo de clicks lo hace el CanvasGroup del menú).
    /// </summary>
    public static Image CreateVeil(Transform canvas, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.transform.SetAsLastSibling();
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    public static void SetVeilAlpha(Image veil, Color color, float alpha)
    {
        color.a = alpha;
        veil.color = color;
        veil.enabled = alpha > 0f;
    }

    /// <summary>
    /// Destello del sol en el avance <paramref name="p"/> (0–1): sigue al sol, crece de
    /// <paramref name="fromScale"/> a <paramref name="toScale"/>, se prende rápido, se sostiene y se
    /// apaga. Fuera del tramo queda apagado.
    /// </summary>
    public static void EvaluateHalo(SpriteRenderer halo, Transform sun, Color color, float fromScale, float toScale, float p)
    {
        bool visible = p > 0f && p < 1f;
        halo.enabled = visible;
        if (!visible) return;

        halo.transform.position = sun.position;
        halo.transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, FeelEase.OutCubic(p));

        const float riseShare = 0.15f;
        color.a *= Mathf.Clamp01(p / riseShare) * (1f - FeelEase.InQuad((p - riseShare) / (1f - riseShare)));
        halo.color = color;
    }

    /// <summary>Cualquier tecla, click o A / B / Start este frame.</summary>
    public static bool SkipPressed()
    {
        // El teclado se lee directo para que Enter y Espacio también salten: InputManager no los
        // expone fuera del módulo de UI.
        Keyboard keyboard = Keyboard.current;
        return (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            || InputManager.PrimaryPressed
            || InputManager.WasPressed(GameAction.Back)
            || InputManager.WasPressed(GameAction.Pause);
    }
}
