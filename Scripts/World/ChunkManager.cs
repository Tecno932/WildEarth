using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    [Header("Chunk Settings")]
    public int chunkSize = 16;
    public int worldRadius = 3;
    public int worldHeight = 4; // número de capas de chunks en vertical

    [Header("References")]
    public Transform player;
    public Material chunkMaterial;

    private Dictionary<Vector3Int, GameObject> activeChunks = new();
    private ChunkStorage storage;
    private bool generating = false;
    private Vector3Int lastPlayerChunk;

    private void Start()
    {
        storage = FindObjectOfType<ChunkStorage>();
        if (storage == null)
        {
            GameObject obj = new("ChunkStorage");
            storage = obj.AddComponent<ChunkStorage>();
        }

        ChunkWorker.StartWorker();
        StartCoroutine(UpdateChunksLoop());
    }

    private void OnDestroy()
    {
        ChunkWorker.StopWorker();
    }

    private IEnumerator UpdateChunksLoop()
    {
        while (true)
        {
            UpdateChunksAroundPlayer();
            yield return new WaitForSeconds(0.25f);
        }
    }

    private void UpdateChunksAroundPlayer()
    {
        if (player == null || generating) return;

        Vector3Int playerChunk = new(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.y / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        if (playerChunk == lastPlayerChunk && activeChunks.Count > 0)
            return;

        lastPlayerChunk = playerChunk;
        StartCoroutine(GenerateChunksAroundPlayer(playerChunk));
    }

    private IEnumerator GenerateChunksAroundPlayer(Vector3Int center)
    {
        generating = true;

        List<Vector3Int> needed = new();
        for (int x = -worldRadius; x <= worldRadius; x++)
        {
            for (int z = -worldRadius; z <= worldRadius; z++)
            {
                for (int y = 0; y < worldHeight; y++) // genera niveles verticales
                {
                    needed.Add(new Vector3Int(center.x + x, y, center.z + z));
                }
            }
        }

        // eliminar chunks fuera del rango
        List<Vector3Int> toRemove = new();
        foreach (var kv in activeChunks)
            if (!needed.Contains(kv.Key))
                toRemove.Add(kv.Key);

        foreach (var coord in toRemove)
        {
            ChunkPool.Instance.ReturnChunk(activeChunks[coord]);
            activeChunks.Remove(coord);
        }

        // generar nuevos chunks
        foreach (var coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;

            byte[,,] data = null;
            if (storage.HasData(new Vector2Int(coord.x, coord.z)))
            {
                data = storage.GetChunkData(new Vector2Int(coord.x, coord.z));
            }
            else
            {
                ChunkWorker.EnqueueJob(coord, chunkSize, 0.08f, 16f);
                yield return new WaitUntil(() => ChunkWorker.TryGetCompletedJob(out data, out var c) &&
                    c.x == coord.x && c.y == coord.z);
                if (data != null)
                    storage.SaveChunk(new Vector2Int(coord.x, coord.z), data);
            }

            if (data != null)
                CreateChunk(coord, data);

            yield return null;
        }

        generating = false;
    }

    private void CreateChunk(Vector3Int coord, byte[,,] data)
    {
        Vector3 pos = new(coord.x * chunkSize, coord.y * chunkSize, coord.z * chunkSize);
        GameObject chunkObj = ChunkPool.Instance.GetChunk(pos, transform);

        Chunk chunk = chunkObj.GetComponent<Chunk>();
        chunk.material = chunkMaterial ?? AtlasBuilder.Instance.GetSharedMaterial();
        chunk.size = chunkSize;
        chunk.SetData(data, coord, FindObjectOfType<WorldGenerator>());
        activeChunks[coord] = chunkObj;

        Debug.Log($"🟩 Chunk generado en {coord}");
    }
}
