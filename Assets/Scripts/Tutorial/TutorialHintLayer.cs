using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Capa de los carteles del tutorial: un canvas en pantalla por encima del juego, sin
/// GraphicRaycaster (nunca le saca un click ni un hover a nada), que crea, recicla y retira los
/// <see cref="TutorialHintView"/>. Con el juego en pausa (menú o diálogo) no se dibuja.
///
/// No decide qué cartel mostrar ni cuándo: eso lo hace quien la usa (el director del tutorial,
/// o <see cref="TutorialHintDebug"/> para probar).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class TutorialHintLayer : MonoBehaviour
{
    public static TutorialHintLayer Instance { get; private set; }

    [SerializeField] private TutorialHintView hintPrefab;
    [SerializeField] private InputGlyphSetSO glyphs;

    private readonly List<TutorialHintView> views = new List<TutorialHintView>();
    private Canvas canvas;

    public InputGlyphSetSO Glyphs => glyphs;

    /// <summary>Cámara para convertir coordenadas de pantalla a la capa. Null en overlay.</summary>
    public Camera UiCamera => canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        canvas = GetComponent<Canvas>();

        if (hintPrefab == null)
            Debug.LogWarning("[TutorialHintLayer] Falta la referencia 'hintPrefab': no se pueden mostrar carteles.");
        if (glyphs == null)
            Debug.LogWarning("[TutorialHintLayer] Falta la referencia 'glyphs': los carteles van a salir sin ícono.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        // Toda pausa pasa por GamePause: los carteles no quedan encima del menú ni del diálogo.
        bool visible = !GamePause.IsPaused;
        if (canvas.enabled != visible)
            canvas.enabled = visible;
    }

    /// <summary>
    /// Muestra un cartel que apunta a <paramref name="target"/> (objeto del mundo o elemento de UI).
    /// Devuelve el cartel para poder retirarlo con <see cref="TutorialHintView.Dismiss"/>, o null.
    /// </summary>
    public TutorialHintView Show(HintPrompt prompt, string textKey, Transform target,
                                 HintPlacement placement, Vector2 offset = default)
    {
        if (hintPrefab == null || target == null)
            return null;

        TutorialHintView view = GetFreeView();
        view.Show(this, prompt, textKey, target, placement, offset);
        return view;
    }

    /// <summary>Retira todos los carteles a la vista.</summary>
    public void HideAll(bool completed = false)
    {
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null)
                views[i].Dismiss(completed);
        }
    }

    private TutorialHintView GetFreeView()
    {
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null && !views[i].IsInUse)
                return views[i];
        }

        TutorialHintView created = Instantiate(hintPrefab, transform);
        created.name = hintPrefab.name;
        views.Add(created);
        return created;
    }
}
