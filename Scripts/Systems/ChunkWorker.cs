using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public static class ChunkWorker
{
    private static readonly Queue<Job> jobQueue = new();
    private static readonly List<Job> completedJobs = new();
    private static Thread workerThread;
    private static bool running = false;

    private class Job
    {
        public Vector3Int coord;
        public int size;
        public float heightMultiplier;
        public byte[,,] result;
    }

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
        Debug.Log("[ChunkWorker] Hilo de generación iniciado.");
    }

    public static void StopWorker()
    {
        running = false;
        workerThread?.Join();
        workerThread = null;
        Debug.Log("[ChunkWorker] Hilo detenido correctamente.");
    }

    public static void EnqueueJob(Vector3Int coord, int size, float noiseScale, float heightMultiplier)
    {
        lock (jobQueue)
        {
            jobQueue.Enqueue(new Job
            {
                coord = coord,
                size = size,
                heightMultiplier = heightMultiplier
            });
        }
    }

    public static bool TryGetCompletedJob(out byte[,,] data, out Vector2Int coord)
    {
        lock (completedJobs)
        {
            if (completedJobs.Count > 0)
            {
                var job = completedJobs[0];
                completedJobs.RemoveAt(0);
                data = job.result;
                coord = new Vector2Int(job.coord.x, job.coord.z);
                return true;
            }
        }

        data = null;
        coord = default;
        return false;
    }

    private static void ProcessJobs()
    {
        while (running)
        {
            Job job = null;
            lock (jobQueue)
            {
                if (jobQueue.Count > 0)
                    job = jobQueue.Dequeue();
            }

            if (job != null)
            {
                job.result = GenerateChunk(job.coord, job.size, job.heightMultiplier);
                lock (completedJobs)
                    completedJobs.Add(job);
            }

            Thread.Sleep(1);
        }
    }

    private static byte[,,] GenerateChunk(Vector3Int coord, int size, float heightMultiplier)
    {
        byte[,,] data = new byte[size, size, size];

        int baseX = coord.x * size;
        int baseZ = coord.z * size;

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                float worldX = baseX + x;
                float worldZ = baseZ + z;

                // ✅ Ruido global coherente
                float noiseValue = Noise.GetGlobal(worldX, worldZ);
                int terrainHeight = Mathf.FloorToInt(noiseValue * heightMultiplier);
                terrainHeight = Mathf.Clamp(terrainHeight, 1, size - 2);

                for (int y = 0; y < size; y++)
                {
                    if (y < terrainHeight - 3)
                        data[x, y, z] = 3;   // Stone
                    else if (y < terrainHeight)
                        data[x, y, z] = 1;   // Dirt
                    else if (y == terrainHeight)
                        data[x, y, z] = 2;   // Grass
                    else
                        data[x, y, z] = 0;   // Air
                }
            }
        }

        return data;
    }
}
