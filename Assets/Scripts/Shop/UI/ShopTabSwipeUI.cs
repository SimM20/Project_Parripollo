using System.Collections;
using UnityEngine;

/// <summary>
/// Swipe al cambiar de tab (por breadcrumb o por "Siguiente"): el contenido nuevo entra desde el
/// costado hacia el que se va — tab siguiente entra por la derecha, anterior por la izquierda — con fade.
/// Solo anima la entrada: <see cref="ShopGridUI"/> reconstruye la grilla en el mismo OnTabChanged.
/// </summary>
public class ShopTabSwipeUI : MonoBehaviour
{
    [System.Serializable]
    private struct SwipeTarget
    {
        public RectTransform rect;
        [Tooltip("Desde cuántas unidades del canvas entra.")]
        public float distance;
    }

    [Header("References")]
    [Tooltip("De acá sale el ShopSystem de la escena: así la instancia del prefab no necesita otro override.")]
    [SerializeField] private ShopGridUI grid;
    [Tooltip("Content de la grilla (lo recorta el Mask del Viewport) y la línea de ayuda.")]
    [SerializeField] private SwipeTarget[] targets;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float duration = 0.25f;

    private CanvasGroup[] groups;
    private float[] restX;
    private ShopSystem shop;
    private ShopTabType lastTab;
    private Coroutine running;

    void Awake()
    {
        groups = new CanvasGroup[targets.Length];
        restX = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            RectTransform rect = targets[i].rect;
            if (rect == null) continue;
            restX[i] = rect.anchoredPosition.x;
            if (!rect.TryGetComponent(out groups[i])) groups[i] = rect.gameObject.AddComponent<CanvasGroup>();
        }
    }

    void OnEnable()
    {
        shop = grid != null ? grid.Shop : null;
        if (shop == null) return;

        lastTab = shop.CurrentTab;
        shop.OnTabChanged += OnTabChanged;
    }

    void OnDisable()
    {
        if (shop != null) shop.OnTabChanged -= OnTabChanged;

        // Unity ya cortó la corrutina: dejar todo en su lugar por si se apagó a mitad del swipe.
        running = null;
        Apply(0f, 1f);
    }

    private void OnTabChanged()
    {
        ShopTabType current = shop.CurrentTab;
        float side = current > lastTab ? 1f : -1f;
        lastTab = current;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Swipe(side));
    }

    private IEnumerator Swipe(float side)
    {
        // Tiempo unscaled: el tutorial de la tienda puede pausar el juego.
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float remaining = 1f - t / duration;
            remaining *= remaining * remaining; // ease-out cúbico
            Apply(side * remaining, 1f - remaining);
            yield return null;
        }

        Apply(0f, 1f);
        running = null;
    }

    /// <param name="offset">Fracción de la distancia de cada target: ±1 afuera, 0 en su lugar.</param>
    private void Apply(float offset, float alpha)
    {
        for (int i = 0; i < groups.Length; i++)
        {
            RectTransform rect = targets[i].rect;
            if (rect == null) continue;

            // Solo x: la y del Content la maneja el ScrollRect (scroll vertical).
            Vector2 pos = rect.anchoredPosition;
            pos.x = restX[i] + offset * targets[i].distance;
            rect.anchoredPosition = pos;
            groups[i].alpha = alpha;
        }
    }
}
