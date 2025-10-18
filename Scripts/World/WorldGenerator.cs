using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public int worldRadius = 3;
    public float updateInterval = 0.25f;

    [Header("Noise Settings")]
    public float noiseScale = 0.08f;
    public float heightMultiplier = 8f;

    [Header("LOD Settings")]
    public bool enableLOD = true;
    public int lod1Distance = 2;
    public int lod2Distance = 4;
    public int lod3Distance = 6;

    [Header("References")]
    public Transform player;
    public Material chunkMaterial;

    private Dictionary<Vector2Int, GameObject> activeChunks = new();
    private ChunkStorage storage;
    private bool generating = false;
    private Vector2Int lastPlayerChunk;

    private void Start()
    {
        storage = FindObjectOfType<ChunkStorage>();
        if (storage == null)
        {
            GameObject obj = new("ChunkStorage");
            storage = obj.AddComponent<ChunkStorage>();
        }

        ChunkWorker.StartWorker();
        StartCoroutine(WaitForAtlasThenGenerate());
    }

    private void OnDestroy()
    {
        ChunkWorker.StopWorker();
    }

    private IEnumerator WaitForAtlasThenGenerate()
    {
        // 🔹 Esperar a que el atlas esté listo sin bloquear el editor
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

        Vector2Int playerChunk = new(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        if (playerChunk == lastPlayerChunk && activeChunks.Count > 0)
            return;

        lastPlayerChunk = playerChunk;
        StartCoroutine(GenerateChunksAroundPlayer(playerChunk));
    }

    private IEnumerator GenerateChunksAroundPlayer(Vector2Int center)
    {
        generating = true;

        List<Vector2Int> needed = new();
        for (int x = -worldRadius; x <= worldRadius; x++)
        {
            for (int z = -worldRadius; z <= worldRadius; z++)
            {
                if (x * x + z * z <= worldRadius * worldRadius)
                    needed.Add(new Vector2Int(center.x + x, center.y + z));
            }
        }

        needed.Sort((a, b) => Vector2.Distance(a, center).CompareTo(Vector2.Distance(b, center)));

        // 🔹 Eliminar chunks fuera de rango
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeChunks)
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // 🔹 Generar gradualmente (1 chunk por frame)
        foreach (var coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;

            byte[,,] data = null;

            if (storage.HasData(coord))
            {
                data = storage.GetChunkData(coord);
            }
            else
            {
                Vector3Int chunkCoord = new(coord.x, 0, coord.y);
                ChunkWorker.EnqueueJob(chunkCoord, chunkSize, noiseScale, heightMultiplier);

                // 🔸 Esperar a que el chunk esté listo sin bloquear Unity
                yield return new WaitUntil(() => ChunkWorker.TryGetCompletedJob(out data, out var completedCoord)
                    && completedCoord.x == chunkCoord.x && completedCoord.y == chunkCoord.z);

                if (data != null)
                    storage.SaveChunk(coord, data);
            }

            if (data != null)
                CreateChunk(coord, data, chunkSize);

            yield return null; // ⚡ genera uno por frame
        }

        generating = false;
    }

    private void CreateChunk(Vector2Int coord, byte[,,] data, int lodSize)
    {
        Vector3 pos = new(coord.x * chunkSize, 0, coord.y * chunkSize);
        GameObject chunkObj = ChunkPool.Instance.GetChunk(pos, transform);

        Chunk chunk = chunkObj.GetComponent<Chunk>();
        chunk.material = chunkMaterial ?? AtlasBuilder.Instance.GetSharedMaterial();
        chunk.size = lodSize;
        chunk.SetData(data, new Vector3Int(coord.x, 0, coord.y), this);
        activeChunks[coord] = chunkObj;

        Debug.Log($"🟩 Chunk generado en {coord.x}, {coord.y}");
    }

    // ===============================================================
    // LOD
    // ===============================================================
    private int GetLODChunkSize(float distance)
    {
        if (!enableLOD) return chunkSize;
        if (distance <= lod1Distance) return chunkSize;
        else if (distance <= lod2Distance) return chunkSize / 2;
        else if (distance <= lod3Distance) return chunkSize / 4;
        else return chunkSize / 8;
    }

    // ===============================================================
    // Utilidades
    // ===============================================================
    public byte GetBlockAt(Vector3Int worldPos)
    {
        int chunkX = Mathf.FloorToInt(worldPos.x / chunkSize);
        int chunkZ = Mathf.FloorToInt(worldPos.z / chunkSize);
        Vector2Int key = new(chunkX, chunkZ);

        if (!storage.HasData(key))
            return 0;

        var data = storage.GetChunkData(key);
        int localX = Mathf.FloorToInt(Mathf.Repeat(worldPos.x, chunkSize));
        int localZ = Mathf.FloorToInt(Mathf.Repeat(worldPos.z, chunkSize));
        int localY = worldPos.y;

        if (localY < 0 || localY >= chunkSize) return 0;
        return data[localX, localY, localZ];
    }

    public byte[,,] GetChunkBlocks(Vector3Int chunkCoord)
    {
        Vector2Int key = new(chunkCoord.x, chunkCoord.z);
        if (storage.HasData(key))
            return storage.GetChunkData(key);
        return null;
    }
}
