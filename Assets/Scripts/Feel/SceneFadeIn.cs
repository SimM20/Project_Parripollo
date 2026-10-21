using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Entrada de escena: cada escena nueva aparece desde negro en un fundido corto, en vez de cortar
/// en seco desde la anterior. Solo presentación: no hay fundido de salida (la carga no se demora
/// ni un frame), el velo no tiene raycaster (no bloquea clicks ni el joystick) y corre en tiempo
/// sin escalar. Se crea solo, sin setup de escena, y vive en DontDestroyOnLoad.
/// Queda por debajo del cursor del gamepad (32000) y por encima del resto de la UI.
/// </summary>
public sealed class SceneFadeIn : MonoBehaviour
{
    private const float FadeSeconds = 0.35f;
    private const int CanvasSortingOrder = 31000;
    private static readonly Color VeilColor = new Color(0.06f, 0.04f, 0.03f, 1f);

    private static SceneFadeIn instance;

    private Image veil;
    private Canvas canvas;
    private float elapsed;
    private bool fading;

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

        instance.Begin();
    }

    private static SceneFadeIn Create()
    {
        var go = new GameObject("SceneFadeIn");
        DontDestroyOnLoad(go);

        SceneFadeIn fade = go.AddComponent<SceneFadeIn>();
        fade.canvas = go.AddComponent<Canvas>();
        fade.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fade.canvas.sortingOrder = CanvasSortingOrder;

        var veilGo = new GameObject("Veil", typeof(RectTransform));
        veilGo.transform.SetParent(go.transform, false);
        RectTransform rect = (RectTransform)veilGo.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fade.veil = veilGo.AddComponent<Image>();
        fade.veil.color = VeilColor;
        fade.veil.raycastTarget = false;
        return fade;
    }

    private void Begin()
    {
        elapsed = 0f;
        fading = true;
        canvas.enabled = true;
        SetAlpha(1f);
    }

    private void Update()
    {
        if (!fading) return;

        // Los primeros frames de la escena traen el pico de carga: el delta se topea para que ese
        // salto no se coma medio fundido de golpe.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        elapsed += dt;
        float t = elapsed / FadeSeconds;

        if (t >= 1f)
        {
            fading = false;
            canvas.enabled = false;
            return;
        }

        SetAlpha(1f - FeelEase.OutCubic(t));
    }

    private void SetAlpha(float alpha)
    {
        Color c = VeilColor;
        c.a = alpha;
        veil.color = c;
    }
}
