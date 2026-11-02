using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila de opciones "ETIQUETA    &lt;  valor  &gt;": recorre una lista de textos con dos flechas.
/// Se usa en lugar de un Dropdown porque con gamepad (navegación por saltos) las flechas son
/// botones comunes y no abren una lista flotante.
/// </summary>
public class OptionSelectorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [Tooltip("Texto chico opcional debajo o al lado de la etiqueta (p. ej. 'PRÓXIMAMENTE').")]
    [SerializeField] private TMP_Text noteText;
    [Tooltip("Al pasar del último vuelve al primero.")]
    [SerializeField] private bool wrap = true;
    [Tooltip("Opacidad de la fila cuando está deshabilitada.")]
    [SerializeField] [Range(0f, 1f)] private float disabledAlpha = 0.4f;

    /// <summary>El jugador cambió el valor con las flechas. Recibe el índice nuevo.</summary>
    public event Action<int> OnValueChanged;

    private readonly List<string> options = new List<string>();
    private int index;
    private bool interactable = true;
    private string displayOverride;
    private CanvasGroup canvasGroup;

    public int Index => index;
    public int Count => options.Count;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (previousButton != null) previousButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    public void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }

    public void SetNote(string text)
    {
        if (noteText == null) return;
        noteText.text = text;
        noteText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>Reemplaza las opciones y selecciona <paramref name="selected"/> sin disparar el evento.</summary>
    public void SetOptions(IList<string> values, int selected)
    {
        options.Clear();
        options.AddRange(values);
        index = options.Count == 0 ? 0 : Mathf.Clamp(selected, 0, options.Count - 1);
        Refresh();
    }

    /// <summary>Selecciona un índice sin disparar el evento.</summary>
    public void SetIndex(int value)
    {
        if (options.Count == 0) return;
        index = Mathf.Clamp(value, 0, options.Count - 1);
        Refresh();
    }

    public void SetInteractable(bool value)
    {
        interactable = value;
        Refresh();
    }

    /// <summary>Muestra este texto en lugar del valor (p. ej. "VSYNC" con el tope de FPS anulado). Null = el valor.</summary>
    public void SetDisplayOverride(string text)
    {
        displayOverride = text;
        Refresh();
    }

    private void Step(int direction)
    {
        if (!interactable || options.Count < 2) return;

        int next = index + direction;
        if (wrap)
            next = (next % options.Count + options.Count) % options.Count;
        else
            next = Mathf.Clamp(next, 0, options.Count - 1);

        if (next == index) return;

        index = next;
        Refresh();
        OnValueChanged?.Invoke(index);
    }

    private void Refresh()
    {
        if (valueText != null)
            valueText.text = displayOverride ?? (options.Count > 0 ? options[index] : "-");

        bool canStep = interactable && options.Count > 1;
        if (previousButton != null) previousButton.interactable = canStep && (wrap || index > 0);
        if (nextButton != null) nextButton.interactable = canStep && (wrap || index < options.Count - 1);

        if (canvasGroup != null)
            canvasGroup.alpha = interactable ? 1f : disabledAlpha;
    }
}
