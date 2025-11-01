using System.Collections.Generic;
using UnityEngine;

public class BiomeManager
{
    private static List<Biome> biomes = new();
    private static bool initialized = false;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        biomes.Clear();

        biomes.Add(new Biome("Plains", 0.6f, 0.7f, new Color(0.45f, 0.8f, 0.45f), 2, 1, 3));
        biomes.Add(new Biome("Desert", 1.0f, 0.1f, new Color(0.9f, 0.8f, 0.3f), 1, 1, 3));
        biomes.Add(new Biome("Snow", 0.1f, 0.5f, new Color(0.9f, 0.9f, 1f), 1, 1, 3));
        biomes.Add(new Biome("Forest", 0.5f, 0.8f, new Color(0.2f, 0.6f, 0.2f), 2, 1, 3));
        biomes.Add(new Biome("Mountains", 0.4f, 0.4f, new Color(0.6f, 0.6f, 0.6f), 3, 3, 3));
    }

    public static Biome GetBiomeAt(float x, float z)
    {
        Initialize();

        float temp = Mathf.PerlinNoise(x * 0.002f, z * 0.002f);
        float hum  = Mathf.PerlinNoise(x * 0.002f + 1000, z * 0.002f + 1000);

        Biome best = biomes[0];
        float bestDist = float.MaxValue;

        foreach (var b in biomes)
        {
            float dist = Mathf.Abs(b.temperature - temp) + Mathf.Abs(b.humidity - hum);
            if (dist < bestDist)
            {
                best = b;
                bestDist = dist;
            }
        }

        return best;
    }
}
