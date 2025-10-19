using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generador de ruido determinista coherente en todo el mundo.
/// Usa cache interno y la semilla global definida en WorldSettings.
/// </summary>
public static class Noise
{
    // Cache de valores ya calculados (reduce carga CPU)
    private static readonly Dictionary<long, float> cache = new();

    public static float GetGlobal(float worldX, float worldZ, int octaves = 4, float persistence = 0.5f, float lacunarity = 2f)
    {
        int seed = WorldSettings.Seed;
        Vector2 offset = WorldSettings.Offset;

        // 🔒 Generar clave única
        long key = ((long)Mathf.FloorToInt(worldX) << 32) ^ Mathf.FloorToInt(worldZ) ^ seed;
        if (cache.TryGetValue(key, out float cached))
            return cached;

        float total = 0f;
        float frequency = 1f;
        float amplitude = 1f;
        float maxValue = 0f;

        // Usamos coordenadas globales absolutas + offset global
        float baseX = (worldX + offset.x + seed * 0.01f);
        float baseZ = (worldZ + offset.y - seed * 0.01f);

        for (int i = 0; i < octaves; i++)
        {
            float nx = baseX * frequency * 0.0015f; // Escala global baja = mundo grande
            float nz = baseZ * frequency * 0.0015f;
            float perlin = Mathf.PerlinNoise(nx, nz);

            total += perlin * amplitude;
            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        float value = total / maxValue;
        cache[key] = value;
        return value;
    }

    public static void ClearCache()
    {
        cache.Clear();
    }
}
