using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChunkStorage : MonoBehaviour
{
    private Dictionary<Vector2Int, byte[,,]> memoryCache = new();
    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "Chunks");
        if (!Directory.Exists(savePath))
            Directory.CreateDirectory(savePath);

        Debug.Log($"📂 Guardando chunks en: {savePath}");
    }

    void OnApplicationQuit()
    {
        // 🔥 Borrar automáticamente todos los datos guardados al cerrar sesión
        try
        {
            if (Directory.Exists(savePath))
            {
                Directory.Delete(savePath, true);
                Directory.CreateDirectory(savePath);
                Debug.Log("🧹 Todos los datos de chunks fueron eliminados al cerrar sesión.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ Error al borrar los datos de chunks: {e.Message}");
        }
    }

    public bool HasData(Vector2Int coord)
    {
        return memoryCache.ContainsKey(coord) || File.Exists(GetChunkPath(coord));
    }

    public byte[,,] GetChunkData(Vector2Int coord)
    {
        if (memoryCache.TryGetValue(coord, out var data))
            return data;

        string path = GetChunkPath(coord);
        if (File.Exists(path))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                data = DeserializeRLE(bytes);
                if (data != null)
                {
                    memoryCache[coord] = data;
                    return data;
                }
                else
                {
                    Debug.LogWarning($"⚠ Chunk corrupto o viejo en {path}, regenerando...");
                    File.Delete(path);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error leyendo chunk {path}: {e.Message}");
            }
        }

        return null;
    }

    public void SaveChunk(Vector2Int coord, byte[,,] data)
    {
        memoryCache[coord] = data;
        byte[] bytes = SerializeRLE(data);
        File.WriteAllBytes(GetChunkPath(coord), bytes);
    }

    private string GetChunkPath(Vector2Int coord)
    {
        return Path.Combine(savePath, $"chunk_{coord.x}_{coord.y}.bin");
    }

    // =========================================================
    // 🔸 RLE Compression con versión y protección
    // =========================================================
    private byte[] SerializeRLE(byte[,,] data)
    {
        List<byte> compressed = new();

        compressed.Add(1); // versión 1 del formato

        byte last = data[0, 0, 0];
        int count = 0;

        foreach (byte b in data)
        {
            if (b == last && count < 255)
            {
                count++;
            }
            else
            {
                compressed.Add(last);
                compressed.Add((byte)count);
                last = b;
                count = 1;
            }
        }

        compressed.Add(last);
        compressed.Add((byte)count);

        return compressed.ToArray();
    }

    private byte[,,] DeserializeRLE(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 3)
        {
            Debug.LogWarning("⚠ Archivo vacío o corrupto en DeserializeRLE()");
            return null;
        }

        int index = 0;
        byte version = bytes[index++];
        if (version != 1)
        {
            Debug.LogWarning($"⚠ Versión de chunk desconocida ({version}), se regenerará.");
            return null;
        }

        List<byte> flat = new();

        while (index + 1 < bytes.Length)
        {
            byte value = bytes[index++];
            int count = bytes[index++];

            for (int j = 0; j < count; j++)
                flat.Add(value);
        }

        int total = flat.Count;
        int size = Mathf.RoundToInt(Mathf.Pow(total, 1f / 3f));

        if (size <= 0 || total < size * size * size)
        {
            Debug.LogWarning($"⚠ Chunk corrupto o incompleto (size={size}, total={total})");
            return null;
        }

        byte[,,] data = new byte[size, size, size];
        int idx = 0;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                for (int z = 0; z < size; z++)
                    data[x, y, z] = flat[idx++];

        return data;
    }

    public void ClearCache()
    {
        memoryCache.Clear();
    }
}
