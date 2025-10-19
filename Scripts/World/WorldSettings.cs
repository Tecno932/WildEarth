using UnityEngine;

public static class WorldSettings
{
    public static int Seed { get; private set; }
    public static Vector2 Offset { get; private set; }

    public static void InitializeSeed(int? seed = null)
    {
        if (seed.HasValue)
            Seed = seed.Value;
        else
            Seed = Random.Range(int.MinValue, int.MaxValue);

        // 🔸 Offset global estable, se mantiene igual durante toda la sesión
        System.Random rng = new(Seed);
        Offset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));

        Debug.Log($"🌱 Semilla mundial generada: {Seed} | Offset: {Offset}");
    }
}
