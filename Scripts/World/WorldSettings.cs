using UnityEngine;

public static class WorldSettings
{
    public static int Seed { get; private set; }
    public static Vector2 Offset { get; private set; }
    private static bool initialized = false;

    /// <summary>
    /// Inicializa la seed global del mundo (solo una vez).
    /// Si pasas null -> seed aleatoria.
    /// </summary>
    public static void InitializeSeed(int? seed = null)
    {
        if (initialized) return;
        initialized = true;

        Seed = seed ?? Random.Range(int.MinValue, int.MaxValue);

        // Offset global derivado de la seed para evitar coordenadas negativas problemáticas
        System.Random rng = new System.Random(Seed);
        Offset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));

        Debug.Log($"🌱 Semilla mundial generada: {Seed} | Offset: {Offset}");
    }
}
