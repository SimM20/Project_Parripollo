using UnityEngine;

/// <summary>
/// Capa de parallax con la cámara quieta: el sprite se desliza solo en horizontal y se repite sin
/// costura. La profundidad sale de la velocidad: las capas lejanas van más lento que las cercanas.
///
/// El renderer pasa a <see cref="SpriteDrawMode.Tiled"/> con <see cref="tiles"/> copias del sprite
/// a lo ancho, y el objeto se corre hasta medio sprite para cada lado de su posición original. Al
/// dar la vuelta salta un sprite entero, y como el dibujo se repite cada un sprite, el salto no se
/// ve. Requisitos del sprite: al menos tan ancho como la vista (los del fondo miden 19,2 u; la vista
/// a 16:9, ~18,9 u) e importado con Mesh Type = Full Rect (si no, el Tiled se dibuja mal).
///
/// Corre con Time.deltaTime: la pausa (<see cref="GamePause"/>) frena también las nubes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("Deriva horizontal en unidades por segundo. Positivo = hacia la derecha. Más lento = más lejos.")]
    [SerializeField] private float driftSpeed = 0.1f;

    [Tooltip("Copias del sprite a lo ancho. Con 2 alcanza si el sprite cubre la vista.")]
    [Min(2)]
    [SerializeField] private int tiles = 2;

    private Vector3 origin;
    private float period;
    private float offset;

    private void Awake()
    {
        origin = transform.localPosition;

        var spriteRenderer = GetComponent<SpriteRenderer>();
        Sprite sprite = spriteRenderer.sprite;

        if (sprite == null)
        {
            Debug.LogWarning("[ParallaxLayer] '" + name + "' no tiene sprite: la capa queda quieta.", this);
            enabled = false;
            return;
        }

        Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit;

        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        spriteRenderer.tileMode = SpriteTileMode.Continuous;
        spriteRenderer.size = new Vector2(spriteSize.x * tiles, spriteSize.y);

        // Cada cuánto se repite el dibujo, en unidades del padre (el objeto puede estar escalado).
        period = spriteSize.x * Mathf.Abs(transform.localScale.x);
    }

    private void Update()
    {
        // Siempre entre -period/2 y +period/2: con 2 copias la vista queda cubierta en todo el recorrido.
        float half = period * 0.5f;
        offset = Mathf.Repeat(offset + driftSpeed * Time.deltaTime + half, period) - half;

        transform.localPosition = origin + new Vector3(offset, 0f, 0f);
    }
}
