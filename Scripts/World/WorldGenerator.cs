using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class WorldGenerator : MonoBehaviour
{
    [Header("World Settings")]
    public int chunkSize = 16;
    public int viewRadius = 8;
    public float updateInterval = 0.25f;

    [Header("Terrain Settings")]
    public int seaLevel = 55;
    public float heightMultiplier = 80f;

    [Header("Noise Settings")]
    [Range(0.0005f, 0.01f)] public float baseScale = 0.0035f;
    [Range(1, 8)] public int octaves = 5;
    [Range(0.3f, 0.8f)] public float persistence = 0.5f;
    [Range(1.5f, 3.0f)] public float lacunarity = 2.0f;
    [Range(1.0f, 3.0f)] public float exponent = 1.3f;

    [Header("Seed Settings")]
    public int customSeed = 0;

    [Header("References")]
    public Transform player;
    public Material grassTintMaterial;

    private Dictionary<Vector3Int, Chunk> activeChunks = new();
    private ChunkStorage storage;
    private Vector3Int lastPlayerChunk = new(int.MinValue, 0, int.MinValue);
    private bool generating = false;

    private CharacterController playerController;
    private Rigidbody playerRb;
    private bool playerFrozen = false;

    private static int seed;
    private static Vector2 offset;

    public Vector2 SeedOffset => offset;

    // ============================================================
    // 🔹 Inicialización
    // ============================================================
    void Start()
    {
        if (customSeed != 0)
            InitializeSeed(customSeed);
        else
            InitializeSeed(Random.Range(int.MinValue, int.MaxValue));

        BiomeManager.Initialize();

        storage = FindFirstObjectByType<ChunkStorage>();
        if (storage == null)
        {
            var go = new GameObject("ChunkStorage");
            storage = go.AddComponent<ChunkStorage>();
        }

        if (player != null)
        {
            playerController = player.GetComponent<CharacterController>();
            playerRb = player.GetComponent<Rigidbody>();
        }

        if (grassTintMaterial == null)
        {
            grassTintMaterial = new Material(Shader.Find("Standard"));
            grassTintMaterial.color = Color.green;
        }

        ChunkWorker.StartWorker();
        StartCoroutine(WaitForAtlasThenGenerate());
    }

    private void InitializeSeed(int s)
    {
        seed = s;
        System.Random rng = new System.Random(seed);
        offset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));
        Debug.Log($"🌱 Seed mundial: {seed} | Offset: {offset}");
    }

    private IEnumerator WaitForAtlasThenGenerate()
    {
        yield return new WaitUntil(() =>
            AtlasBuilder.Instance != null &&
            AtlasBuilder.Instance.GetAtlasTexture() != null);

        StartCoroutine(UpdateWorldLoop());
    }

    private IEnumerator UpdateWorldLoop()
    {
        while (true)
        {
            // 🟩 Procesar resultados del hilo de generación en cada frame
            while (ChunkWorker.TryGetCompletedJob(out var data, out var coord2))
            {
                Vector3Int worldCoord = new(coord2.x, 0, coord2.y);
                if (!activeChunks.ContainsKey(worldCoord))
                    CreateChunk(worldCoord, data);
            }

            // 🟦 Actualizar los chunks alrededor del jugador (encolar nuevos)
            if (!generating)
                UpdateChunksAroundPlayer();

            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void UpdateChunksAroundPlayer()
    {
        if (player == null) return;

        Vector3Int playerChunk = new(
            Mathf.FloorToInt(player.position.x / chunkSize),
            0,
            Mathf.FloorToInt(player.position.z / chunkSize)
        );

        HandlePlayerFreeze(playerChunk);

        if (playerChunk == lastPlayerChunk) return;
        lastPlayerChunk = playerChunk;

        if (!generating)
            StartCoroutine(GenerateChunksAroundPlayer(playerChunk));
    }

    private void HandlePlayerFreeze(Vector3Int currentChunk)
    {
        bool chunkLoaded = activeChunks.ContainsKey(currentChunk);
        if (!chunkLoaded && !playerFrozen) FreezePlayer(true);
        else if (chunkLoaded && playerFrozen) FreezePlayer(false);
    }

    private void FreezePlayer(bool freeze)
    {
        playerFrozen = freeze;
        if (playerController != null)
            playerController.enabled = !freeze;
        if (playerRb != null)
            playerRb.constraints = freeze ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
    }

    // ============================================================
    // 🔹 Generación circular de chunks
    // ============================================================
    private IEnumerator GenerateChunksAroundPlayer(Vector3Int center)
    {
        generating = true;

        List<Vector3Int> needed = new();
        for (int x = -viewRadius; x <= viewRadius; x++)
        for (int z = -viewRadius; z <= viewRadius; z++)
            if (Mathf.Sqrt(x * x + z * z) <= viewRadius)
                needed.Add(new Vector3Int(center.x + x, 0, center.z + z));

        needed.Sort((a, b) =>
            Vector2.SqrMagnitude(new Vector2(a.x - center.x, a.z - center.z))
            .CompareTo(Vector2.SqrMagnitude(new Vector2(b.x - center.x, b.z - center.z)))
        );

        foreach (var kv in activeChunks.ToList())
            if (!needed.Contains(kv.Key))
            {
                ChunkPool.Instance.ReturnChunk(kv.Value.gameObject);
                activeChunks.Remove(kv.Key);
            }

        // 🔹 Solo encolamos trabajos; la lectura se hace en UpdateWorldLoop()
        foreach (var coord in needed)
        {
            if (activeChunks.ContainsKey(coord)) continue;
            ChunkWorker.EnqueueJob(coord, chunkSize, this);
        }

        generating = false;
        yield break;
    }

    // ============================================================
    // 🏔️ Generación de terreno tipo Minecraft
    // ============================================================
    private byte[,,] GenerateChunkData(Vector3Int coord)
    {
        int worldX = coord.x * chunkSize;
        int worldZ = coord.z * chunkSize;

        // 🔸 El tamaño vertical se calcula dinámicamente (más seguro)
        int verticalSize = 256;
        byte[,,] blocks = new byte[chunkSize, verticalSize, chunkSize];

        for (int x = 0; x < chunkSize; x++)
        for (int z = 0; z < chunkSize; z++)
        {
            int wx = worldX + x;
            int wz = worldZ + z;

            // 🌄 Ruido base (relieve grande)
            float baseNoise = FractalNoise(wx * 0.003f, wz * 0.003f);
            // 🌿 Detalle fino
            float detailNoise = FractalNoise(wx * 0.02f, wz * 0.02f) * 0.15f;

            // 🎚️ Altura final
            float heightValue = (baseNoise + detailNoise) * heightMultiplier;
            int terrainHeight = Mathf.FloorToInt(50 + heightValue);

            // ⛅ Bioma local
            Biome biome = BiomeManager.GetBiomeAt(wx, wz);
            bool underWater = terrainHeight < seaLevel;

            for (int y = 0; y < verticalSize; y++)
            {
                if (y > terrainHeight)
                {
                    blocks[x, y, z] = (underWater && y <= seaLevel) ? (byte)4 : (byte)0; // agua o aire
                }
                else if (y == terrainHeight)
                {
                    blocks[x, y, z] = biome.topBlock; // césped
                }
                else if (y > terrainHeight - 4)
                {
                    blocks[x, y, z] = biome.fillerBlock; // tierra
                }
                else
                {
                    blocks[x, y, z] = biome.stoneBlock; // piedra
                }
            }
        }

        return blocks;
    }

    // ============================================================
    // 🔸 Fractal Noise (suavizado tipo Minecraft)
    // ============================================================
    private float FractalNoise(float x, float z)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        float nx = (x + offset.x);
        float nz = (z + offset.y);

        for (int i = 0; i < octaves; i++)
        {
            float perlin = Mathf.PerlinNoise(nx * frequency, nz * frequency);
            total += perlin * amplitude;
            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Mathf.Pow(total / maxValue, exponent);
    }

    // ============================================================
    // 🔹 Creación visual del chunk
    // ============================================================
    private void CreateChunk(Vector3Int coord, byte[,,] data)
    {
        Vector3 pos = new(coord.x * chunkSize, 0, coord.z * chunkSize);
        GameObject chunkObj = ChunkPool.Instance.GetChunk(pos, transform);
        Chunk chunk = chunkObj.GetComponent<Chunk>();
        chunk.size = chunkSize;
        // 🔹 Reconstruir vecinos adyacentes para eliminar caras visibles entre chunks
        RebuildNeighborChunks(coord);

        Biome dominantBiome = BiomeManager.GetBiomeAt(
            coord.x * chunkSize + chunkSize / 2,
            coord.z * chunkSize + chunkSize / 2
        );

        Material matInstance = new Material(grassTintMaterial);
        matInstance.SetColor("_TintColor", dominantBiome.grassTint);

        chunk.material = matInstance;
        chunk.SetData(data, coord, this);
        chunk.Rebuild();

        activeChunks[coord] = chunk;
        // 🔹 Actualizar bordes de los vecinos (por si ya existían)
        RebuildNeighborChunks(coord);
    }

    private void RebuildNeighborChunks(Vector3Int coord)
    {
        Vector3Int[] dirs =
        {
            new(-1, 0, 0),
            new(1, 0, 0),
            new(0, 0, -1),
            new(0, 0, 1)
        };

        foreach (var dir in dirs)
        {
            Vector3Int neighborCoord = coord + dir;
            if (activeChunks.TryGetValue(neighborCoord, out Chunk neighbor))
            {
                // 🔹 Recalcular su cache de vecinos y reconstruir la malla
                neighbor.CacheNeighbors();
                neighbor.Rebuild();
            }
        }
    }

    // ============================================================
    // 🧭 Métodos auxiliares
    // ============================================================
    public bool IsChunkLoaded(Vector3Int coord) => activeChunks.ContainsKey(coord);

    public Chunk GetChunk(Vector3Int coord)
    {
        return activeChunks.TryGetValue(coord, out Chunk chunk) ? chunk : null;
    }

    public byte[,,] GetChunkBlocks(Vector3Int coord)
    {
        Vector2Int key = new(coord.x, coord.z);
        return storage?.GetChunkData(key);
    }

    public void SetBlock(Vector3Int pos, byte id)
    {
        Vector3Int chunkCoord = new(
            Mathf.FloorToInt((float)pos.x / chunkSize),
            0,
            Mathf.FloorToInt((float)pos.z / chunkSize)
        );

        Chunk chunk = GetChunk(chunkCoord);
        if (chunk == null) return;

        int lx = Mathf.FloorToInt(Mathf.Repeat(pos.x, chunkSize));
        int ly = Mathf.FloorToInt(pos.y);
        int lz = Mathf.FloorToInt(Mathf.Repeat(pos.z, chunkSize));

        chunk.SetBlock(new Vector3Int(lx, ly, lz), id);
        UpdateNeighborChunksIfNeeded(chunkCoord, lx, lz);
    }

    private void UpdateNeighborChunksIfNeeded(Vector3Int chunkCoord, int lx, int lz)
    {
        bool atLeft = lx == 0;
        bool atRight = lx == chunkSize - 1;
        bool atFront = lz == chunkSize - 1;
        bool atBack = lz == 0;

        if (atLeft) GetChunk(chunkCoord + Vector3Int.left)?.Rebuild();
        if (atRight) GetChunk(chunkCoord + Vector3Int.right)?.Rebuild();
        if (atFront) GetChunk(chunkCoord + new Vector3Int(0, 0, 1))?.Rebuild();
        if (atBack) GetChunk(chunkCoord + new Vector3Int(0, 0, -1))?.Rebuild();
    }

    public Vector3Int GetBlockCoords(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.x),
            Mathf.FloorToInt(worldPos.y),
            Mathf.FloorToInt(worldPos.z)
        );
    }
}
