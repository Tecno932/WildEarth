//| Parámetro                           | Qué hace                                                                                 | Valores recomendados  |
//| ----------------------------------- | ---------------------------------------------------------------------------------------- | --------------------- |
//| **baseScale (0.0015f)**             | Escala general del terreno. Más chico = montañas grandes, más grande = colinas pequeñas. | 0.001–0.002           |
//| **octaves (7)**                     | Capas de ruido. Más octavas = más detalle.                                               | 6–8                   |
//| **persistence (0.5)**               | Cuánto decae la amplitud entre capas. Más bajo = más contraste.                          | 0.4–0.6               |
//| **lacunarity (2.3)**                | Cuánto aumenta la frecuencia entre capas. Más alto = más detalle.                        | 2.0–2.6               |
//| **Mathf.Pow(perlin, 2.2f)**         | Da forma a las montañas: más alto = picos puntiagudos.                                   | 1.5–2.5               |
//| **value * 1.6f - 0.15f**            | Ajusta el rango vertical (relieve total).                                                | multiplicador 1.4–1.8 |

//| Tipo de terreno                     | baseScale | persistence | lacunarity | pow | comentario       |
//| ----------------------------------- | --------- | ----------- | ---------- | --- | ---------------- |
//| **Llanuras suaves**                 | 0.002     | 0.6         | 2.0        | 1.2 | estilo pradera   |
//| **Colinas y montañas suaves**       | 0.0015    | 0.5         | 2.3        | 1.6 | equilibrio       |
//| **Montañas puntiagudas tipo Andes** | 0.0012    | 0.45        | 2.5        | 2.2 | relieves fuertes |
//| **Terreno caótico tipo fantasía**   | 0.001     | 0.4         | 2.8        | 2.4 | relieve extremo  |
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generador de ruido determinista coherente en todo el mundo.
/// Usa cache interno y la semilla global definida en WorldSettings.
/// </summary>
public static class Noise
{
    // 🧠 Cache para evitar recalcular el mismo punto muchas veces (ahorra CPU)
    private static readonly Dictionary<long, float> cache = new();

    /// <summary>
    /// Genera un valor de ruido global (0–1) basado en Perlin multi-octava.
    /// Controla la forma general del terreno: valles, montañas, colinas.
    /// </summary>
    /// <param name="worldX">Coordenada mundial X</param>
    /// <param name="worldZ">Coordenada mundial Z</param>
    /// <param name="octaves">Cantidad de capas de ruido (más = más detalle)</param>
    /// <param name="persistence">Qué tan rápido disminuye la amplitud entre octavas</param>
    /// <param name="lacunarity">Qué tan rápido aumenta la frecuencia entre octavas</param>
    /// <returns>Valor normalizado entre 0 y 1</returns>
    public static float GetGlobal(
        float worldX,
        float worldZ,
        int octaves = 6,
        float persistence = 0.5f,
        float lacunarity = 2.3f
    )
    {
        int seed = WorldSettings.Seed;
        Vector2 offset = WorldSettings.Offset;

        // 🔒 Generar clave única para cache (mezcla X, Z y seed)
        long key = ((long)Mathf.FloorToInt(worldX) << 32) ^ Mathf.FloorToInt(worldZ) ^ seed;
        if (cache.TryGetValue(key, out float cached))
            return cached;

        // =========================
        // 🌄 Parámetros del ruido
        // =========================
        float total = 0f;
        float frequency = 1f;   // 🔹 Aumenta con cada octava -> más detalle
        float amplitude = 1f;   // 🔹 Disminuye con cada octava -> suaviza
        float maxValue = 0f;

        // =========================
        // 🌍 Escala global del mundo
        // =========================
        // Estos números controlan el "tamaño del planeta":
        // - Bajá el 0.0015f para montañas más anchas y suaves.
        // - Subilo para colinas más cortas y detalladas.
        float baseScale = 0.0015f;

        // Offset global basado en la seed (cambia totalmente el mapa)
        float baseX = (worldX + offset.x + seed * 0.01f);
        float baseZ = (worldZ + offset.y - seed * 0.01f);

        // =========================
        // 🔁 Octavas: ruido fractal
        // =========================
        for (int i = 0; i < octaves; i++)
        {
            float nx = baseX * frequency * baseScale;
            float nz = baseZ * frequency * baseScale;

            float perlin = Mathf.PerlinNoise(nx, nz);

            // 💡 Este "pow" cambia la forma del relieve:
            // - Mayor exponente (>1): montañas más puntiagudas.
            // - Menor exponente (<1): terreno más plano.
            perlin = Mathf.Pow(perlin, 2.2f);

            total += perlin * amplitude;
            maxValue += amplitude;

            // persistence: controla cuánto "peso" tienen las siguientes octavas
            // 🔹 menor valor = más contraste entre montañas y valles
            amplitude *= persistence;

            // lacunarity: controla cuánto se duplica la frecuencia en cada paso
            // 🔹 mayor valor = más detalle fractal, montañas con texturas
            frequency *= lacunarity;
        }

        // Normalización
        float value = total / maxValue;

        // =========================
        // 🧮 Ajuste final del relieve
        // =========================
        // 🔹 Pow(1.1f): define la "pendiente" general (más o menos abrupta)
        // 🔹 *1.6f - 0.15f: remapea el rango (más fuerte o más plano)
        value = Mathf.Pow(value, 1.1f);
        value = Mathf.Clamp01(value * 1.6f - 0.15f);

        cache[key] = value;
        return value;
    }

    /// <summary>
    /// Limpia el cache del ruido (por ejemplo al cambiar de seed o mundo)
    /// </summary>
    public static void ClearCache()
    {
        cache.Clear();
    }
}
