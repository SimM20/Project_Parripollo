using UnityEngine;

/// <summary>
/// Fantasma del frasco dado vuelta en el punto donde conviene verter: con el frasco ahí, la salsa cae
/// en el centro del plato (<see cref="ToppingDraggable.TryGetIdealPourPosition"/>). Es el objetivo del
/// cartel <see cref="HintAnchorId.SaucePourPoint"/> y solo se ve mientras un cartel lo señala: con el
/// cartel aprendido, arrastrar un frasco no muestra nada.
///
/// Lo crea <see cref="TutorialHintContext"/> la primera vez que hace falta. Solo visual: no tiene
/// collider ni toca el juego. Se dibuja justo debajo del frasco arrastrado, así al llegar lo tapa.
/// </summary>
public class SaucePourGuide : MonoBehaviour
{
    private const float MinAlpha = 0.3f;
    private const float MaxAlpha = 0.6f;
    private const float PulseFrequency = 1.4f;
    private const float FadeSpeed = 4f;

    private SpriteRenderer ghost;
    private ToppingDraggable jar;
    private float alpha;

    public static SaucePourGuide Create()
    {
        var go = new GameObject("SaucePourGuide");
        SaucePourGuide guide = go.AddComponent<SaucePourGuide>();
        guide.ghost = go.AddComponent<SpriteRenderer>();
        guide.ghost.enabled = false;
        return guide;
    }

    /// <summary>
    /// Se ubica sobre el frasco que se está arrastrando y devuelve a qué apuntar, o null si no hay un
    /// frasco en la mano esperando para verter (o no hay plato).
    /// </summary>
    public Transform Resolve()
    {
        ToppingDraggable held = ToppingDraggable.Held;
        jar = held != null && !held.IsPouring ? held : null;

        if (jar == null || !jar.TryGetIdealPourPosition(out Vector3 position))
        {
            jar = null;
            alpha = 0f;
            ghost.enabled = false;
            return null;
        }

        SpriteRenderer jarRenderer = jar.GetComponent<SpriteRenderer>();
        ghost.enabled = jarRenderer != null;
        if (jarRenderer != null)
        {
            ghost.sprite = jarRenderer.sprite;
            ghost.sortingLayerID = jarRenderer.sortingLayerID;
            ghost.sortingOrder = jarRenderer.sortingOrder - 1;
        }

        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, 180f));
        transform.localScale = jar.transform.lossyScale;
        return transform;
    }

    private void LateUpdate()
    {
        if (jar != null)
            Resolve();

        if (!ghost.enabled)
            return;

        // Aparece con el cartel y late suave; sin cartel que lo señale se apaga.
        bool pointed = TutorialHintLayer.Instance != null && TutorialHintLayer.Instance.IsPointingAt(transform);
        float pulse = Mathf.Lerp(MinAlpha, MaxAlpha, (Mathf.Sin(Time.unscaledTime * PulseFrequency * 2f * Mathf.PI) + 1f) * 0.5f);
        alpha = Mathf.MoveTowards(alpha, pointed ? pulse : 0f, FadeSpeed * Time.unscaledDeltaTime);
        ghost.color = new Color(1f, 1f, 1f, alpha);
    }
}
