using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup explicativo de cierre anticipado ("Se terminó el día antes de tiempo"). Vive en
/// EndScene, en un canvas por encima de la tienda. Si la última noche terminó por strikes
/// (<see cref="StrikeSystem.ConsumeNightEndedByStrikes"/>) se muestra apenas carga la
/// escena y bloquea la tienda hasta que el jugador presiona "Ir a la tienda"; si no, se
/// desactiva solo.
///
/// Flujo actual (sin pantalla de resumen): Gameplay → EndScene (tienda) → este popup.
/// Si más adelante hay pantalla de estadísticas, este objeto se mueve ahí sin cambios.
/// </summary>
public class StrikeEndPopup : MonoBehaviour
{
    [Tooltip("Root del popup (fondo bloqueante + panel). Vacío = este mismo GameObject.")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button continueButton;
    [Tooltip("Ilustración (cliente enojado + cartel). Arte TBD: puede quedar vacía.")]
    [SerializeField] private Image illustration;

    [Header("Textos (spec v0.1)")]
    [SerializeField] private string title = "Se terminó el día antes de tiempo";
    [TextArea(3, 6)]
    [SerializeField] private string body =
        "Llegaste al límite de clientes perdidos de la jornada.\n\n" +
        "Cuando tres clientes se van por falta de paciencia, corren la voz y dejan de llegar nuevos clientes.\n\n" +
        "Atendiste a los que quedaban, pero por hoy no pasa nadie más. Mañana arrancás de nuevo.";

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;

        if (illustration != null && illustration.sprite == null)
            illustration.enabled = false;

        if (continueButton != null)
            continueButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        if (StrikeSystem.ConsumeNightEndedByStrikes())
            Open();
        else
            root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Close);
    }

    /// <summary>Muestra el popup y bloquea la tienda que quedó cargada detrás.</summary>
    public void Open() => root.SetActive(true);

    /// <summary>"Ir a la tienda": cierra el popup y deja la tienda habilitada normalmente.</summary>
    public void Close() => root.SetActive(false);
}
