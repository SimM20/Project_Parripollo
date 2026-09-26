using TMPro;
using UnityEngine;

/// <summary>
/// Texto fijo de escena o prefab traducido por clave (<see cref="Loc"/>). Pisa el texto del
/// <see cref="TMP_Text"/> al habilitarse y cada vez que cambia el idioma.
///
/// Solo para textos estáticos: si un script escribe el texto (plata, hora, nombres), el script
/// pide la clave con <see cref="Loc.Get"/> y este componente no va. En el Editor el TMP conserva
/// el texto en español para que la escena se lea igual.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Clave de las tablas de Resources/Localization (p. ej. 'menu.play').")]
    [SerializeField] private string key;

    private TMP_Text text;

    public string Key
    {
        get => key;
        set
        {
            key = value;
            Refresh();
        }
    }

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Loc.OnTextsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.OnTextsChanged -= Refresh;
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(key)) return;
        if (text == null) text = GetComponent<TMP_Text>();
        if (text != null) text.text = Loc.Get(key);
    }
}
