using UnityEngine;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Sistema multihilo para generación procedural de chunks.
/// Gestiona trabajos encolados con prioridad, limitando carga.
/// </summary>
public static class ChunkWorker
{
    private static readonly Queue<Job> jobQueue = new();
    private static readonly Queue<Job> completedJobs = new();
    private static Thread workerThread;
    private static bool running = false;

    // 🔹 Nuevo: limitar cantidad de jobs activos
    private const int MaxJobsQueued = 32;

    private class Job
    {
        public Vector3Int coord;
        public int size;
        public WorldGenerator world;
        public byte[,,] result;
        public float priority; // más bajo = más importante
    }

    // ============================================================
    // 🧱 Inicio y detención del hilo
    // ============================================================
    public static void StartWorker()
    {
        if (running) return;
        running = true;

        workerThread = new Thread(ProcessJobs)
        {
            IsBackground = true,
            Name = "ChunkWorkerThread"
        };
        workerThread.Start();

        Debug.Log("[ChunkWorker] 🧱 Hilo de generación iniciado.");
    }

    public static void StopWorker()
    {
        running = false;
        workerThread?.Join();
        workerThread = null;
        Debug.Log("[ChunkWorker] ⛔ Hilo detenido correctamente.");
    }

    // ============================================================
    // 📥 Encolar trabajos con prioridad
    // ============================================================
    public static void EnqueueJob(Vector3Int coord, int size, WorldGenerator world)
    {
        lock (jobQueue)
        {
            // Evitar saturación
            if (jobQueue.Count >= MaxJobsQueued)
                return;

            float priority = 0f;
            if (world.player != null)
            {
                Vector3 playerChunk = new(
                    Mathf.FloorToInt(world.player.position.x / size),
                    0,
                    Mathf.FloorToInt(world.player.position.z / size)
                );
                priority = Vector3.Distance(playerChunk, coord);
            }

            jobQueue.Enqueue(new Job
            {
                coord = coord,
                size = size,
                world = world,
                priority = priority
            });
        }
    }

    // ============================================================
    // 📤 Recuperar trabajos terminados
    // ============================================================
    public static bool TryGetCompletedJob(out byte[,,] data, out Vector2Int coord)
    {
        lock (completedJobs)
        {
            if (completedJobs.Count > 0)
            {
                var job = completedJobs.Dequeue();
                data = job.result;
                coord = new Vector2Int(job.coord.x, job.coord.z);
                return true;
            }
        }

        data = null;
        coord = default;
        return false;
    }

    // ============================================================
    // 🔄 Bucle del hilo secundario
    // ============================================================
    private static void ProcessJobs()
    {
        while (running)
        {
            Job job = null;

            lock (jobQueue)
            {
                if (jobQueue.Count > 0)
                {
                    // 🔹 Elegir el job más cercano al jugador (menor prioridad)
                    Job[] arr = jobQueue.ToArray();
                    job = arr[0];
                    foreach (var j in arr)
                        if (j.priority < job.priority)
                            job = j;

                    // Remover ese job del queue
                    var tempList = new List<Job>(arr);
                    tempList.Remove(job);
                    jobQueue.Clear();
                    foreach (var j in tempList)
                        jobQueue.Enqueue(j);
                }
            }

            if (job != null && job.world != null)
            {
                job.result = GenerateChunk(job.coord, job.size, job.world);

                // 🔹 Guardar en cache del mundo para vecinos
                SaveChunkData(job.coord, job.result, job.world);

                lock (completedJobs)
                    completedJobs.Enqueue(job);

                Debug.Log($"[ChunkWorker] ✅ Chunk generado ({job.coord.x}, {job.coord.z})");
            }

            Thread.Sleep(2);
        }
    }

    // ============================================================
    // 🏔️ Generación procedural tipo Minecraft
    // ============================================================
    private static byte[,,] GenerateChunk(Vector3Int coord, int size, WorldGenerator world)
    {
        int seaLevel = world.seaLevel;
        float heightMult = world.heightMultiplier;

        int baseX = coord.x * size;
        int baseZ = coord.z * size;

        int localMaxHeight = 128;
        byte[,,] blocks = new byte[size, localMaxHeight, size];

        for (int x = 0; x < size; x++)
        for (int z = 0; z < size; z++)
        {
            int wx = baseX + x;
            int wz = baseZ + z;

            float baseNoise = Noise.GetGlobal(wx * 0.5f, wz * 0.5f, world.baseScale, world.octaves, world.persistence, world.lacunarity, world.exponent, world.SeedOffset);
            float detailNoise = Noise.GetGlobal(wx * 2f, wz * 2f, world.baseScale, world.octaves, world.persistence, world.lacunarity, world.exponent, world.SeedOffset);

            float combined = Mathf.Clamp01(baseNoise * 0.7f + detailNoise * 0.3f);
            int terrainHeight = Mathf.FloorToInt(combined * heightMult + 40f);

            Biome biome = BiomeManager.GetBiomeAt(wx, wz);
            bool underWater = terrainHeight < seaLevel;

            for (int y = 0; y < localMaxHeight; y++)
            {
                if (y > terrainHeight)
                    blocks[x, y, z] = (underWater && y <= seaLevel) ? (byte)4 : (byte)0;
                else if (y == terrainHeight)
                    blocks[x, y, z] = biome.topBlock;
                else if (y > terrainHeight - 4)
                    blocks[x, y, z] = biome.fillerBlock;
                else
                    blocks[x, y, z] = biome.stoneBlock;
            }
        }

        return blocks;
    }

    // ============================================================
    // 💾 Guardar chunk generado (para vecinos)
    // ============================================================
    private static void SaveChunkData(Vector3Int coord, byte[,,] data, WorldGenerator world)
    {
        if (world == null || data == null) return;

        // ⚠️ No accedemos a ningún componente de Unity aquí
        var storageField = typeof(WorldGenerator).GetField("storage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (storageField == null) return;

        ChunkStorage storage = storageField.GetValue(world) as ChunkStorage;
        if (storage == null) return;

        Vector2Int key = new(coord.x, coord.z);
        storage.SaveChunk(key, data);
    }
}

// ============================================================
// 🌍 Clase auxiliar para ruido fractal coherente
// ============================================================
public static class Noise
{
    public static float GetGlobal(float x, float z, float baseScale, int octaves, float persistence, float lacunarity, float exponent, Vector2 offset)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        float nx = (x + offset.x) * baseScale;
        float nz = (z + offset.y) * baseScale;

        for (int i = 0; i < octaves; i++)
        {
            float perlin = Mathf.PerlinNoise(nx * frequency, nz * frequency);
            perlin = Mathf.Pow(perlin, exponent);
            total += perlin * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return Mathf.Clamp01(total / maxValue);
    }
}
