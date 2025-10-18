using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WorldGenerator : MonoBehaviour
{
    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public int worldRadius = 2;
    public float updateInterval = 0.25f; // cada cuánto se actualizan los chunks

    [Header("Noise Settings")]
    public float noiseScale = 0.08f;
    public float heightMultiplier = 8f;

    [Header("Materials")]
    public Material chunkMaterial;

    [Header("References")]
    public Transform player;

    // IDs de bloques
    private const byte AIR = 0;
    private const byte DIRT = 1;
    private const byte GRASS = 2;

    // Control de chunks activos
    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new();
    private readonly Dictionary<Vector2Int, byte[,,]> chunkCache = new();

    private Vector2Int lastPlayerChunk;
    private bool generating = false;

    void Start()
    {
        StartCoroutine(WaitForAtlasThenGenerate());
    }

    private IEnumerator WaitForAtlasThenGenerate()
    {
        yield return new WaitUntil(() => 
            AtlasBuilder.Instance != null && AtlasBuilder.Instance.GetAtlasTexture() != null);

        Debug.Log("✅ Atlas listo, generando mundo...");
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

        // no regenerar si el jugador no cambió de chunk
        if (playerChunk == lastPlayerChunk && activeChunks.Count > 0)
            return;

        lastPlayerChunk = playerChunk;
        StartCoroutine(GenerateVisibleChunksAsync(playerChunk));
    }

    private IEnumerator GenerateVisibleChunksAsync(Vector2Int center)
    {
        generating = true;

        HashSet<Vector2Int> needed = new();
        for (int x = -worldRadius; x <= worldRadius; x++)
        {
            for (int z = -worldRadius; z <= worldRadius; z++)
            {
                needed.Add(new Vector2Int(center.x + x, center.y + z));
            }
        }

        // Quitar los chunks que quedaron fuera del rango
        List<Vector2Int> toRemove = new();
        foreach (var kv in activeChunks)
        {
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);
        }
        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // Generar los nuevos chunks progresivamente
        foreach (Vector2Int coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;

            Vector3Int chunkCoord = new Vector3Int(coord.x, 0, coord.y);
            byte[,,] data = GetOrGenerateChunkData(chunkCoord);

            GameObject chunkObj = ChunkPool.Instance.GetChunk(
                new Vector3(coord.x * chunkSize, 0, coord.y * chunkSize),
                transform
            );

            Chunk chunk = chunkObj.GetComponent<Chunk>();
            chunk.material = chunkMaterial ?? AtlasBuilder.Instance.GetSharedMaterial();
            chunk.SetData(data, chunkCoord, this);

            activeChunks[coord] = chunkObj;

            yield return null; // 👈 evita congelar el frame
        }

        generating = false;
    }

    private byte[,,] GetOrGenerateChunkData(Vector3Int chunkCoord)
    {
        Vector2Int key = new Vector2Int(chunkCoord.x, chunkCoord.z);
        if (chunkCache.TryGetValue(key, out byte[,,] cached))
            return cached;

        byte[,,] data = new byte[chunkSize, chunkSize, chunkSize];

        int baseX = chunkCoord.x * chunkSize;
        int baseZ = chunkCoord.z * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int h = Mathf.FloorToInt(Mathf.PerlinNoise(
                    (baseX + x) * noiseScale,
                    (baseZ + z) * noiseScale) * heightMultiplier);
                h = Mathf.Clamp(h, 0, chunkSize - 1);

                for (int y = 0; y < chunkSize; y++)
                {
                    if (y < h) data[x, y, z] = DIRT;
                    else if (y == h) data[x, y, z] = GRASS;
                    else data[x, y, z] = AIR;
                }
            }
        }

        chunkCache[key] = data;
        return data;
    }

    public byte GetBlockAt(Vector3Int worldPos)
    {
        int chunkX = Mathf.FloorToInt((float)worldPos.x / chunkSize);
        int chunkZ = Mathf.FloorToInt((float)worldPos.z / chunkSize);
        int localX = Mathf.FloorToInt(Mathf.Repeat(worldPos.x, chunkSize));
        int localZ = Mathf.FloorToInt(Mathf.Repeat(worldPos.z, chunkSize));
        int localY = worldPos.y;

        Vector2Int key = new Vector2Int(chunkX, chunkZ);
        if (!chunkCache.TryGetValue(key, out byte[,,] data))
            data = GetOrGenerateChunkData(new Vector3Int(chunkX, 0, chunkZ));

        if (localY < 0 || localY >= chunkSize) return 0;
        return data[localX, localY, localZ];
    }
}
