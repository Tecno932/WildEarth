using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generador principal de mundo voxel:
/// - Inicializa la semilla global.
/// - Controla los chunks visibles según la posición del jugador.
/// - Crea los chunks con materiales por tipo (default + tinted).
/// - Soporta precompute opcional.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    [Header("World Settings")]
    public int chunkSize = 16;
    public int worldRadius = 512;
    public int viewRadius = 8;
    public float updateInterval = 0.25f;

    [Header("Noise Settings")]
    public float noiseScale = 0.08f;
    public float heightMultiplier = 16f;

    [Header("Seed Settings")]
    public int customSeed = 0;

    [Header("Precompute (cache)")]
    public int precomputeRadius = 0;
    public int precomputeBatchSize = 256;

    [Header("References")]
    public Transform player;
    public Material defaultMaterial; // tierra, piedra, etc.
    public Material tintedMaterial;  // pasto, hojas, enredaderas, etc.

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private ChunkStorage storage;
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private bool generating = false;

    void Start()
    {
        // 🔹 Inicializar seed global
        if (customSeed != 0)
        {
            WorldSettings.InitializeSeed(customSeed);
            Debug.Log($"🌍 Usando semilla personalizada: {customSeed}");
        }
        else
        {
            WorldSettings.InitializeSeed();
            Debug.Log($"🌍 Semilla aleatoria generada: {WorldSettings.Seed}");
        }

        // 🔹 Crear o encontrar almacenamiento
        storage = Object.FindFirstObjectByType<ChunkStorage>();
        if (storage == null)
        {
            GameObject obj = new("ChunkStorage");
            storage = obj.AddComponent<ChunkStorage>();
            Debug.Log("[WorldGenerator] ChunkStorage creado en runtime.");
        }

        // 🔹 Iniciar el worker
        ChunkWorker.StartWorker();
        Debug.Log("[WorldGenerator] ChunkWorker iniciado.");

        // 🔹 Esperar atlas antes de generar
        StartCoroutine(WaitForAtlasThenGenerate());
    }

    void OnDestroy() => ChunkWorker.StopWorker();

    private IEnumerator WaitForAtlasThenGenerate()
    {
        yield return new WaitUntil(() =>
            AtlasBuilder.Instance != null &&
            AtlasBuilder.Instance.GetAtlasTexture() != null);

        if (precomputeRadius > 0)
            StartCoroutine(PrecomputeWorldData(precomputeRadius));

        StartCoroutine(UpdateWorldLoop());
    }

    private IEnumerator UpdateWorldLoop()
    {
        while (true)
        {
            UpdateChunksAroundPlayer();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void UpdateChunksAroundPlayer()
    {
        if (player == null || generating) return;

        Vector2Int playerChunk = new(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        if (playerChunk != lastPlayerChunk)
        {
            lastPlayerChunk = playerChunk;
            StartCoroutine(GenerateChunksAroundPlayer(playerChunk));
        }
    }

    private IEnumerator GenerateChunksAroundPlayer(Vector2Int center)
    {
        generating = true;

        List<Vector2Int> needed = new();
        for (int x = -viewRadius; x <= viewRadius; x++)
        for (int z = -viewRadius; z <= viewRadius; z++)
        {
            Vector2Int coord = new(center.x + x, center.y + z);
            if (Mathf.Abs(coord.x) > worldRadius || Mathf.Abs(coord.y) > worldRadius) continue;
            needed.Add(coord);
        }

        needed.Sort((a, b) => Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

        // 🔹 Remover los que ya no están en vista
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeChunks)
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // 🔹 Crear los nuevos necesarios
        foreach (var coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;

            byte[,,] data = storage?.GetChunkData(coord);
            if (data == null)
            {
                Vector3Int chunkCoord = new(coord.x, 0, coord.y);
                ChunkWorker.EnqueueJob(chunkCoord, chunkSize, noiseScale, heightMultiplier);
                float timeout = Time.realtimeSinceStartup + 10f;

                while (Time.realtimeSinceStartup < timeout)
                {
                    if (ChunkWorker.TryGetCompletedJob(out data, out var completedCoord))
                    {
                        if (completedCoord.x == chunkCoord.x && completedCoord.y == chunkCoord.z)
                            break;
                    }
                    yield return null;
                }

                if (data != null && storage != null)
                    storage.SaveChunk(coord, data);
            }

            if (data != null)
                CreateChunk(coord, data, chunkSize);

            yield return null;
        }

        generating = false;
    }

    private void CreateChunk(Vector2Int coord, byte[,,] data, int size)
    {
        Vector3 pos = new(coord.x * size, 0, coord.y * size);
        GameObject chunkObj = ChunkPool.Instance.GetChunk(pos, transform);

        Chunk chunk = chunkObj.GetComponent<Chunk>();
        if (chunk == null)
        {
            Debug.LogError("[WorldGenerator] El objeto obtenido no tiene componente Chunk.");
            return;
        }

        chunk.Initialize(data, new Vector3Int(coord.x, 0, coord.y), this);
        activeChunks[coord] = chunkObj;
    }

    private IEnumerator PrecomputeWorldData(int radius)
    {
        yield break; // (puedo pasarte la versión extendida si querés caching completo)
    }

    public byte[,,] GetChunkBlocks(Vector3Int chunkCoord)
    {
        Vector2Int key = new(chunkCoord.x, chunkCoord.z);
        return storage?.GetChunkData(key);
    }
}
