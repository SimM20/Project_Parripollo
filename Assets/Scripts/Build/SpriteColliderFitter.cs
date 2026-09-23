using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ajusta el Collider2D de un visual a la silueta REAL del sprite que está mostrando.
///
/// Por qué existe: los visuales de carne salen todos del mismo prefab genérico
/// (`StockPrefab`) y recién después se les asigna el sprite del corte. Todos los cortes
/// se dibujan sobre un lienzo de 100x100 px, así que medir el collider con
/// `sprite.bounds` daba SIEMPRE el mismo cuadrado de 1x1 unidad, sin importar el corte:
/// un chorizo ocupa 84x53 px de ese lienzo, un vacío 92x59 y un paty 82x87. Sobre el
/// plato, con el margen del 20% que llevaba el agarre, la caja del chorizo terminaba
/// midiendo más del doble de su alto real y se quedaba con los clicks destinados al plato.
///
/// La silueta sale del **physics shape** del sprite: los PNG de cortes se importan con
/// `spriteGenerateFallbackPhysicsShape`, así que Unity genera el contorno por alfa sin
/// que haya que dibujar nada a mano. Si un sprite no trae shape (o no hay sprite) se cae
/// a una `BoxCollider2D` del tamaño de `sprite.bounds`, que es el comportamiento viejo.
/// </summary>
public static class SpriteColliderFitter
{
    private static readonly List<Vector2> ShapePoints = new List<Vector2>(64);
    private static readonly List<Vector2[]> ValidPaths = new List<Vector2[]>(4);

    /// <summary>
    /// Deja en 'go' un collider que calza con el sprite de 'renderer' y lo devuelve.
    /// 'padding' es el margen de agarre en unidades locales del sprite (se escala con el
    /// transform): empuja el contorno hacia afuera para que agarrar un corte finito no
    /// exija precisión de cirujano.
    /// </summary>
    public static Collider2D Fit(GameObject go, SpriteRenderer renderer, float padding = 0f)
    {
        if (go == null)
            return null;

        Sprite sprite = renderer != null ? renderer.sprite : null;
        bool flipX = renderer != null && renderer.flipX;
        bool flipY = renderer != null && renderer.flipY;

        if (sprite != null && TryFitPolygon(go, sprite, flipX, flipY, padding, out PolygonCollider2D polygon))
        {
            // La caja del prefab genérico ya no manda: se apaga (no se destruye) para que
            // siga estando disponible si un sprite posterior obliga al fallback.
            BoxCollider2D box = go.GetComponent<BoxCollider2D>();
            if (box != null)
                box.enabled = false;

            return polygon;
        }

        return FitBox(go, sprite, padding);
    }

    /// <summary>Contorno por alfa del sprite. False si el sprite no trae physics shape usable.</summary>
    private static bool TryFitPolygon(GameObject go, Sprite sprite, bool flipX, bool flipY, float padding,
                                      out PolygonCollider2D polygon)
    {
        polygon = null;

        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return false;

        ValidPaths.Clear();
        Vector2 spriteCenter = sprite.bounds.center;

        for (int i = 0; i < shapeCount; i++)
        {
            ShapePoints.Clear();
            if (sprite.GetPhysicsShape(i, ShapePoints) < 3)
                continue;

            // El margen se empuja desde el centro del CONTORNO, no del lienzo: un corte
            // dibujado descentrado no se deforma hacia un lado.
            Vector2 center = Vector2.zero;
            for (int p = 0; p < ShapePoints.Count; p++)
                center += ShapePoints[p];
            center /= ShapePoints.Count;

            var path = new Vector2[ShapePoints.Count];
            for (int p = 0; p < ShapePoints.Count; p++)
            {
                Vector2 point = ShapePoints[p];

                if (padding > 0f)
                {
                    Vector2 outward = point - center;
                    if (outward.sqrMagnitude > 1e-6f)
                        point += outward.normalized * padding;
                }

                // flipX/flipY del SpriteRenderer espejan el DIBUJO pero no el shape:
                // hay que espejarlo a mano alrededor del centro del sprite. Sin esto la
                // carne de la cara B queda con el collider de la cara A.
                if (flipX) point.x = 2f * spriteCenter.x - point.x;
                if (flipY) point.y = 2f * spriteCenter.y - point.y;

                path[p] = point;
            }

            // Espejar en un solo eje invierte el sentido del contorno. PolygonCollider2D lo
            // tolera, pero devolverlo a su orientación original evita sorpresas al depurar.
            if (flipX ^ flipY)
                System.Array.Reverse(path);

            ValidPaths.Add(path);
        }

        if (ValidPaths.Count == 0)
            return false;

        polygon = go.GetComponent<PolygonCollider2D>();
        if (polygon == null)
            polygon = go.AddComponent<PolygonCollider2D>();

        polygon.enabled = true;
        polygon.offset = Vector2.zero;
        polygon.pathCount = ValidPaths.Count;

        for (int i = 0; i < ValidPaths.Count; i++)
            polygon.SetPath(i, ValidPaths[i]);

        ValidPaths.Clear();
        return true;
    }

    /// <summary>Fallback: caja del tamaño del sprite (o 1x1 si no hay sprite).</summary>
    private static Collider2D FitBox(GameObject go, Sprite sprite, float padding)
    {
        // Si venía de un sprite con contorno y el nuevo no lo tiene, el polígono viejo
        // seguiría respondiendo al mouse con la silueta equivocada.
        PolygonCollider2D stale = go.GetComponent<PolygonCollider2D>();
        if (stale != null)
            stale.enabled = false;

        BoxCollider2D box = go.GetComponent<BoxCollider2D>();
        if (box == null)
            box = go.AddComponent<BoxCollider2D>();

        box.enabled = true;

        if (sprite != null)
        {
            box.size = (Vector2)sprite.bounds.size + Vector2.one * (2f * Mathf.Max(0f, padding));
            box.offset = sprite.bounds.center;
        }
        else
        {
            box.size = Vector2.one;
            box.offset = Vector2.zero;
        }

        return box;
    }
}
