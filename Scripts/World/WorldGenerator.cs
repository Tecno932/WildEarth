using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    [Header("World Settings")]
    public int chunkSize = 16;
    public int worldRadius = 171824; // tamaño total del mundo (en chunks)
    public int viewRadius = 8;       // radio de renderizado (en chunks)
    public float updateInterval = 0.25f;

    [Header("Noise Settings")]
    public float noiseScale = 0.08f;
    public float heightMultiplier = 16f;

    [Header("References")]
    public Transform player;
    public Material chunkMaterial;

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private ChunkStorage storage;
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private bool generating = false;

    void Start()
    {
        WorldSettings.InitializeSeed();

        // Usar la API moderna si está disponible
        storage = Object.FindFirstObjectByType<ChunkStorage>();
        if (storage == null)
        {
            GameObject obj = new("ChunkStorage");
            storage = obj.AddComponent<ChunkStorage>();
            Debug.Log("[WorldGenerator] ChunkStorage creado en runtime.");
        }

        ChunkWorker.StartWorker();
        Debug.Log("[WorldGenerator] ChunkWorker pedido para iniciar.");
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

                // No salir del mundo
                if (Mathf.Abs(coord.x) > worldRadius || Mathf.Abs(coord.y) > worldRadius)
                    continue;

                needed.Add(coord);
            }
        }

        // Ordenar por distancia
        needed.Sort((a, b) => Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

        // Remover chunks fuera del rango
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeChunks)
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // Generar nuevos chunks (uno por iteración/frame)
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

                // Espera hasta que se reciba un trabajo completado del mismo chunk
                float timeoutTime = Time.realtimeSinceStartup + 10f; // timeout por chunk
                bool gotIt = false;

                while (Time.realtimeSinceStartup < timeoutTime)
                {
                    if (ChunkWorker.TryGetCompletedJob(out data, out var completedCoord))
                    {
                        // completedCoord.x == chunkCoord.x && completedCoord.y == chunkCoord.z
                        if (completedCoord.x == chunkCoord.x && completedCoord.y == chunkCoord.z)
                        {
                            gotIt = true;
                            break;
                        }
                        else
                        {
                            // Si el trabajo completado corresponde a otro chunk,
                            // guardarlo inmediatamente (si storage existe) para no perderlo.
                            if (data != null && storage != null)
                            {
                                Vector2Int otherKey = new Vector2Int(completedCoord.x, completedCoord.y);
                                storage.SaveChunk(otherKey, data);
                                Debug.Log($"[WorldGenerator] Resultado de otro chunk ({otherKey.x},{otherKey.y}) guardado mientras esperamos.");
                            }
                            // continuar esperando el que pedimos
                            data = null;
                        }
                    }
                    else
                    {
                        // nada listo aún: yield un frame
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

            // generamos 1 por frame para no bloquear rendering
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

    // 🔸 Método necesario para el sistema de vecinos de Chunk.cs
    public byte[,,] GetChunkBlocks(Vector3Int chunkCoord)
    {
        Vector2Int key = new Vector2Int(chunkCoord.x, chunkCoord.z);
        if (storage != null && storage.HasData(key))
            return storage.GetChunkData(key);
        return null;
    }
}
