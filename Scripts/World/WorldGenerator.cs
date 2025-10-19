using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WorldGenerator mejorado:
/// - inicializa seed (customSeed opcional)
/// - genera chunks alrededor del player (viewRadius)
/// - opcional: precomputar y cachear un área mayor (precomputeRadius)
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    [Header("World Settings")]
    public int chunkSize = 16;
    public int worldRadius = 512; // tamaño total del mundo (en chunks)
    public int viewRadius = 8;       // radio de renderizado (en chunks)
    public float updateInterval = 0.25f;

    [Header("Noise Settings")]
    public float noiseScale = 0.08f;
    public float heightMultiplier = 16f;

    [Header("Seed Settings")]
    [Tooltip("Si es 0, se generará una semilla aleatoria.")]
    public int customSeed = 0;

    [Header("Precompute (cache)")]
    [Tooltip("Si > 0, se encolan y guardan (no se instancian) chunks en un radio alrededor del origen (0,0).")]
    public int precomputeRadius = 0;
    [Tooltip("Cuántos jobs encolar por frame para precompute (para no saturar).")]
    public int precomputeBatchSize = 256;

    [Header("References")]
    public Transform player;
    public Material chunkMaterial;

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private ChunkStorage storage;
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private bool generating = false;

    void Start()
    {
        // Inicializar seed global
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

        // Encontrar / crear ChunkStorage
        storage = Object.FindFirstObjectByType<ChunkStorage>();
        if (storage == null)
        {
            GameObject obj = new("ChunkStorage");
            storage = obj.AddComponent<ChunkStorage>();
            Debug.Log("[WorldGenerator] ChunkStorage creado en runtime.");
        }

        // Iniciar worker
        ChunkWorker.StartWorker();
        Debug.Log("[WorldGenerator] ChunkWorker pedido para iniciar.");

        // Esperar atlas y lanzar generación + precompute si está activado
        StartCoroutine(WaitForAtlasThenGenerate());
    }

    void OnDestroy()
    {
        ChunkWorker.StopWorker();
        Debug.Log("[WorldGenerator] Parando worker.");
    }

    private IEnumerator WaitForAtlasThenGenerate()
    {
        yield return new WaitUntil(() =>
            AtlasBuilder.Instance != null &&
            AtlasBuilder.Instance.GetAtlasTexture() != null);

        Debug.Log("✅ Atlas listo. Generando mundo...");

        // Si se quiere precomputar, lanzar la rutina de precompute (no bloqueante)
        if (precomputeRadius > 0)
            StartCoroutine(PrecomputeWorldData(precomputeRadius));

        // Luego arrancar el loop de generación visible
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

        Vector2Int playerChunk = new Vector2Int(
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
        {
            for (int z = -viewRadius; z <= viewRadius; z++)
            {
                Vector2Int coord = new Vector2Int(center.x + x, center.y + z);

                if (Mathf.Abs(coord.x) > worldRadius || Mathf.Abs(coord.y) > worldRadius)
                    continue;

                needed.Add(coord);
            }
        }

        // Priorizar por distancia
        needed.Sort((a, b) => Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

        // Remover chunks fuera del rango de visión
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeChunks)
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // Generar/instanciar los necesarios
        foreach (var coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;

            byte[,,] data = null;
            if (storage != null && storage.HasData(coord))
            {
                data = storage.GetChunkData(coord);
            }
            else
            {
                Vector3Int chunkCoord = new Vector3Int(coord.x, 0, coord.y);
                ChunkWorker.EnqueueJob(chunkCoord, chunkSize, noiseScale, heightMultiplier);

                // Espera segura por el resultado (timeout)
                float timeoutTime = Time.realtimeSinceStartup + 10f;
                bool gotIt = false;

                while (Time.realtimeSinceStartup < timeoutTime)
                {
                    if (ChunkWorker.TryGetCompletedJob(out data, out var completedCoord))
                    {
                        // TryGetCompletedJob devuelve (x,z) => completedCoord.y es z
                        if (completedCoord.x == chunkCoord.x && completedCoord.y == chunkCoord.z)
                        {
                            gotIt = true;
                            break;
                        }
                        else
                        {
                            // Guardar resultados de otros chunks que lleguen antes
                            if (data != null && storage != null)
                            {
                                Vector2Int otherKey = new Vector2Int(completedCoord.x, completedCoord.y);
                                if (!storage.HasData(otherKey))
                                {
                                    storage.SaveChunk(otherKey, data);
                                    Debug.Log($"[WorldGenerator] Guardado resultado de chunk {otherKey.x},{otherKey.y} recibido mientras esperábamos.");
                                }
                            }
                            data = null;
                        }
                    }
                    else
                    {
                        // no hay completados -> esperar un frame
                        yield return null;
                    }
                }

                if (!gotIt)
                {
                    Debug.LogWarning($"[WorldGenerator] Timeout esperando chunk {chunkCoord.x},{chunkCoord.z}. Saltando.");
                    data = null;
                }

                if (data != null && storage != null)
                    storage.SaveChunk(coord, data);
            }

            if (data != null)
                CreateChunk(coord, data, chunkSize);

            // generar 1 por frame para no bloquear rendering
            yield return null;
        }

        generating = false;
    }

    private void CreateChunk(Vector2Int coord, byte[,,] data, int size)
    {
        Vector3 pos = new Vector3(coord.x * size, 0, coord.y * size);
        GameObject chunkObj = ChunkPool.Instance.GetChunk(pos, transform);

        Chunk chunk = chunkObj.GetComponent<Chunk>();
        if (chunk == null)
        {
            Debug.LogError("[WorldGenerator] El object obtenido no tiene componente Chunk.");
            return;
        }

        chunk.material = chunkMaterial ?? (AtlasBuilder.Instance != null ? AtlasBuilder.Instance.GetSharedMaterial() : null);
        chunk.size = size;
        chunk.SetData(data, new Vector3Int(coord.x, 0, coord.y), this);
        activeChunks[coord] = chunkObj;

        Debug.Log($"🟩 Chunk generado en {coord.x}, {coord.y}");
    }

    // ===============================================================
    // PRECOMPUTE: encola muchos jobs y los guarda en storage (no instantiate)
    // ===============================================================
    private IEnumerator PrecomputeWorldData(int radius)
    {
        if (storage == null)
        {
            Debug.LogWarning("[WorldGenerator] No hay ChunkStorage para precompute. Abortando precompute.");
            yield break;
        }

        Debug.Log($"[WorldGenerator] Iniciando precompute radius={radius} (esto puede tardar).");

        // Crear lista de coords a encolar (alrededor del origen). Podés cambiar origen si querés.
        List<Vector2Int> allCoords = new();
        for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                Vector2Int coord = new Vector2Int(x, z);
                if (Mathf.Abs(coord.x) > worldRadius || Mathf.Abs(coord.y) > worldRadius) continue;
                if (!storage.HasData(coord))
                    allCoords.Add(coord);
            }

        // Encolar en batches (para no saturar memoria y CPU)
        int idx = 0;
        while (idx < allCoords.Count)
        {
            int end = Mathf.Min(idx + precomputeBatchSize, allCoords.Count);
            for (int i = idx; i < end; i++)
            {
                Vector2Int c = allCoords[i];
                ChunkWorker.EnqueueJob(new Vector3Int(c.x, 0, c.y), chunkSize, noiseScale, heightMultiplier);
            }
            idx = end;

            // Recibir resultados y guardarlos
            float receiveTimeout = Time.realtimeSinceStartup + 5f; // espera corta por batch
            while (Time.realtimeSinceStartup < receiveTimeout)
            {
                if (ChunkWorker.TryGetCompletedJob(out var data, out var coord))
                {
                    Vector2Int key = new Vector2Int(coord.x, coord.y);
                    if (data != null && !storage.HasData(key))
                    {
                        storage.SaveChunk(key, data);
                    }
                }
                else
                {
                    // No hay más completados por ahora
                    break;
                }
            }

            // yield un frame para mantener editor responsivo
            yield return null;
        }

        // finalmente vaciar la cola de completados restante
        while (true)
        {
            if (ChunkWorker.TryGetCompletedJob(out var data, out var coord))
            {
                Vector2Int key = new Vector2Int(coord.x, coord.y);
                if (data != null && !storage.HasData(key))
                    storage.SaveChunk(key, data);
            }
            else break;

            // cede ocasionalmente
            yield return null;
        }

        Debug.Log("[WorldGenerator] Precompute finalizado.");
    }

    // Método para que otros sistemas pidan datos de chunks (neighbouring)
    public byte[,,] GetChunkBlocks(Vector3Int chunkCoord)
    {
        Vector2Int key = new Vector2Int(chunkCoord.x, chunkCoord.z);
        if (storage != null && storage.HasData(key))
            return storage.GetChunkData(key);
        return null;
    }
}
