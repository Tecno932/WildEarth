using System.Collections.Generic;
using UnityEngine;

public class ChunkPool : MonoBehaviour
{
    public static ChunkPool Instance { get; private set; }

    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public GameObject GetChunk(Vector3 position, Transform parent = null)
    {
        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.SetActive(true);
        }
        else
        {
            obj = new GameObject("Chunk");
            obj.AddComponent<Chunk>();
        }

        obj.transform.parent = parent;
        obj.transform.position = position;
        return obj;
    }

    public void ReturnChunk(GameObject chunk)
    {
        chunk.SetActive(false);
        pool.Enqueue(chunk);
    }
}
