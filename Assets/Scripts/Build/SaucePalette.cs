using UnityEngine;

/// <summary>
/// Colores de una salsa en pixel art, de oscuro a claro, más los trocitos (cebolla, morrón,
/// hierbas...). Lo arma <see cref="ToppingDraggable"/> con su inspector y lo usan el chorro
/// (<see cref="SauceStreamFX"/>) y el charco (<see cref="SauceDecal"/>).
/// </summary>
public sealed class SaucePalette
{
    /// <summary>Tamaño en mundo de un pixel de salsa.</summary>
    public float pixelSize;

    public Color32 outline;
    public Color32 shadow;
    public Color32 body;
    public Color32 light;
    public Color32 highlight;

    /// <summary>Aceite o jugo que se escurre alrededor del charco. Alfa 0 = sin halo.</summary>
    public Color32 halo;

    /// <summary>Sombrita del charco sobre el plato, debajo del borde de abajo.</summary>
    public Color32 castShadow;

    public Color32[] chunkLight = new Color32[0];
    public Color32[] chunkShade = new Color32[0];

    /// <summary>Fracción de pixeles del charco con un trocito.</summary>
    public float chunkDensity;

    /// <summary>Semilla del ruido: dos salsas en el mismo plato no repiten el dibujo.</summary>
    public int seed;

    /// <summary>Ruido estable por celda, de 0 a 1. Misma celda y semilla, mismo valor.</summary>
    public static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
