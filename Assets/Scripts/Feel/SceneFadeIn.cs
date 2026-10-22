using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transición de escena: la escena que se va funde a negro (<see cref="FadeOutThenLoad"/>, la usa
/// <see cref="SceneManagementUtils"/>) y la nueva aparece desde negro. Las dos curvas son lentas en
/// los extremos para que el paso por negro se note. Corre en tiempo sin escalar.
/// Durante la salida la escena vieja sigue viva: el velo bloquea los clicks de UI y se descartan
/// pedidos de carga repetidos (gana el primero). En la entrada no bloquea nada.
/// Se crea solo, sin setup de escena, y vive en DontDestroyOnLoad.
/// Queda por debajo del cursor del gamepad (32000) y por encima del resto de la UI.
/// </summary>
public sealed class SceneFadeIn : MonoBehaviour
{
    private const float FadeOutSeconds = 0.35f;
    private const float FadeInSeconds = 0.35f;
    private const int CanvasSortingOrder = 31000;
    private static readonly Color VeilColor = new Color(0.06f, 0.04f, 0.03f, 1f);

    private enum Phase { Idle, Out, In }

    private static SceneFadeIn instance;

    private Image veil;
    private Canvas canvas;
    private GraphicRaycaster raycaster;
    private Phase phase;
    private float elapsed;
    private float outFrom;
    private float outSeconds;
    private Action pendingLoad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (instance == null)
            instance = Create();

        instance.BeginIn();
    }

    /// <summary>
    /// Funde a negro y recién ahí ejecuta <paramref name="load"/> (limpieza + LoadScene). Si ya hay
    /// una salida en curso, el pedido se descarta.
    /// </summary>
    public static void FadeOutThenLoad(Action load)
    {
        if (instance == null)
            instance = Create();

        instance.BeginOut(load);
    }

    private static SceneFadeIn Create()
    {
        var go = new GameObject("SceneFadeIn");
        DontDestroyOnLoad(go);

        SceneFadeIn fade = go.AddComponent<SceneFadeIn>();
        fade.canvas = go.AddComponent<Canvas>();
        fade.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fade.canvas.sortingOrder = CanvasSortingOrder;
        fade.raycaster = go.AddComponent<GraphicRaycaster>();
        fade.raycaster.enabled = false;

        var veilGo = new GameObject("Veil", typeof(RectTransform));
        veilGo.transform.SetParent(go.transform, false);
        RectTransform rect = (RectTransform)veilGo.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fade.veil = veilGo.AddComponent<Image>();
        fade.veil.color = VeilColor;
        // Solo bloquea mientras el raycaster está prendido (fundido de salida).
        fade.veil.raycastTarget = true;
        return fade;
    }

    private void BeginOut(Action load)
    {
        if (phase == Phase.Out) return;

        // Toda carga pasa por acá (botones de menú, pausa, tienda, fin de la noche): un solo enganche.
        GamepadHaptics.Play(HapticEvent.SceneTransition);

        // Si la entrada todavía no terminó, se oscurece desde donde quedó y en proporción.
        outFrom = phase == Phase.In ? veil.color.a : 0f;
        outSeconds = Mathf.Max(FadeOutSeconds * (1f - outFrom), 0.01f);
        pendingLoad = load;
        elapsed = 0f;
        phase = Phase.Out;
        canvas.enabled = true;
        raycaster.enabled = true;
        SetAlpha(outFrom);
    }

    private void BeginIn()
    {
        pendingLoad = null;
        elapsed = 0f;
        phase = Phase.In;
        canvas.enabled = true;
        raycaster.enabled = false;
        SetAlpha(1f);
    }

    private void Update()
    {
        if (phase == Phase.Idle) return;

        // Los primeros frames de la escena traen el pico de carga: el delta se topea para que ese
        // salto no se coma medio fundido de golpe.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        elapsed += dt;

        if (phase == Phase.Out)
        {
            // Carga ya pedida: el velo queda en negro hasta que sceneLoaded arranque la entrada.
            if (pendingLoad == null) return;

            float tOut = elapsed / outSeconds;
            SetAlpha(Mathf.Lerp(outFrom, 1f, FeelEase.InOutCubic(tOut)));
            if (tOut < 1f) return;

            Action load = pendingLoad;
            pendingLoad = null;
            load();
            return;
        }

        float t = elapsed / FadeInSeconds;

        if (t >= 1f)
        {
            phase = Phase.Idle;
            canvas.enabled = false;
            return;
        }

        SetAlpha(1f - FeelEase.InOutCubic(t));
    }

    private void SetAlpha(float alpha)
    {
        Color c = VeilColor;
        c.a = alpha;
        veil.color = c;
    }
}
