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
        public float noiseScale;
        public float heightMultiplier;
        public byte[,,] result;
        public bool done;
    }

    public static void StartWorker()
    {
        if (running) return;
        running = true;
        workerThread = new Thread(ProcessJobs);
        workerThread.Start();
    }

    public static void StopWorker()
    {
        running = false;
        workerThread?.Join();
    }

    public static void EnqueueJob(Vector3Int coord, int size, float noiseScale, float heightMultiplier)
    {
        lock (jobQueue)
        {
            jobQueue.Enqueue(new Job { coord = coord, size = size, noiseScale = noiseScale, heightMultiplier = heightMultiplier });
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

// Nuevo método para saber si ya no quedan trabajos pendientes
public static bool IsQueueEmpty()
{
    lock (jobQueue)
    {
        return jobQueue.Count == 0;
    }
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
                job.result = GenerateChunk(job.coord, job.size, job.noiseScale, job.heightMultiplier);
                job.done = true;

                lock (completedJobs)
                    completedJobs.Add(job);
            }

            Thread.Sleep(1); // da tiempo a Unity
        }
    }

    private static byte[,,] GenerateChunk(Vector3Int coord, int size, float noiseScale, float heightMultiplier)
    {
        byte[,,] data = new byte[size, size, size];
        int baseX = coord.x * size;
        int baseZ = coord.z * size;

        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                int h = Mathf.FloorToInt(
                    Mathf.PerlinNoise((baseX + x) * noiseScale, (baseZ + z) * noiseScale) * heightMultiplier
                );
                h = Mathf.Clamp(h, 0, size - 1);

                for (int y = 0; y < size; y++)
                {
                    if (y < h) data[x, y, z] = 1;        // DIRT
                    else if (y == h) data[x, y, z] = 2;  // GRASS
                    else data[x, y, z] = 0;              // AIR
                }
            }
        }
        return data;
    }
}
