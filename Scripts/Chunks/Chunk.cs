using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    [Header("Chunk Settings")]
    public int size = 16;
    public float blockSize = 1f;

    [Header("Runtime References")]
    public WorldGenerator worldRef;
    public Vector3Int chunkCoord;

    private MeshFilter mf;
    private MeshRenderer mr;
    private MeshCollider mc;

    private byte[,,] blocks;
    private Dictionary<Vector3Int, byte[,,]> neighborCache = new();

    private bool meshBuilt = false;
    private bool needsRebuild = false;
    private bool neighborsCached = false;

    public Material material
    {
        get => mr != null ? mr.sharedMaterial : null;
        set { if (mr != null) mr.sharedMaterial = value; }
    }

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mc = GetComponent<MeshCollider>();
        mc.cookingOptions = MeshColliderCookingOptions.None;
    }

    public void SetData(byte[,,] data, Vector3Int coord, WorldGenerator world)
    {
        blocks = data;
        chunkCoord = coord;
        worldRef = world;
        size = data.GetLength(0);

        meshBuilt = false;
        needsRebuild = false;
        neighborsCached = false;

        StartCoroutine(BuildMeshAsync());
    }

    private IEnumerator BuildMeshAsync()
    {
        float timeout = Time.time + 2f;
        while (!AreNeighborsReady() && Time.time < timeout)
            yield return null;

        CacheNeighbors();
        neighborsCached = true;

        yield return null;
        BuildMeshFast();

        meshBuilt = true;
        NotifyNeighbors();
    }

    private bool AreNeighborsReady()
    {
        Vector3Int[] dirs =
        {
            new Vector3Int(-1,0,0),
            new Vector3Int(1,0,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1)
        };

        foreach (var dir in dirs)
        {
            Vector3Int nc = chunkCoord + dir;
            if (!worldRef.IsChunkLoaded(nc)) return false;

            var neighbor = worldRef.GetChunkBlocks(nc);
            if (neighbor == null) return false;
        }

        return true;
    }

    public void CacheNeighbors()
    {
        neighborCache.Clear();
        Vector3Int[] dirs =
        {
            new Vector3Int(-1,0,0),
            new Vector3Int(1,0,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1)
        };

        foreach (var dir in dirs)
        {
            Vector3Int nc = chunkCoord + dir;
            neighborCache[dir] = worldRef.GetChunkBlocks(nc);
        }
    }

    private void BuildMeshFast()
    {
        List<Vector3> verts = new();
        List<int> tris = new();
        List<Vector2> uvs = new();

        int height = blocks.GetLength(1);

        for (int x = 0; x < size; x++)
        for (int y = 0; y < height; y++)
        for (int z = 0; z < size; z++)
        {
            byte block = blocks[x, y, z];
            if (block == 0) continue;

            for (int face = 0; face < 6; face++)
            {
                Vector3Int off = GetNeighborOffset(face);
                byte neighbor = GetBlockGlobal(x + off.x, y + off.y, z + off.z);
                if (neighbor != 0) continue;

                AddFace(verts, tris, uvs, x, y, z, face, block);
            }
        }

        Mesh mesh = new() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
    }

    private void AddFace(List<Vector3> verts, List<int> tris, List<Vector2> uvs,
                         int x, int y, int z, int face, byte block)
    {
        int vi = verts.Count;
        Vector3[] fv = GetFaceVertices(new Vector3(x, y, z), face);
        verts.AddRange(fv);

        tris.Add(vi + 0); tris.Add(vi + 1); tris.Add(vi + 2);
        tris.Add(vi + 0); tris.Add(vi + 2); tris.Add(vi + 3);

        string blockName = GetBlockName(block);
        BlockInfo info = BlockDatabase.Instance.GetBlock(blockName);
        string tex = info?.textures?.all ?? "dirt";
        Rect uv = AtlasBuilder.Instance.GetUV(tex);

        uvs.Add(new Vector2(uv.xMin, uv.yMin));
        uvs.Add(new Vector2(uv.xMax, uv.yMin));
        uvs.Add(new Vector2(uv.xMax, uv.yMax));
        uvs.Add(new Vector2(uv.xMin, uv.yMax));
    }

    private byte GetBlockGlobal(int x, int y, int z)
    {
        int height = blocks.GetLength(1);
        if (x >= 0 && x < size && y >= 0 && y < height && z >= 0 && z < size)
            return blocks[x, y, z];

        Vector3Int offset = Vector3Int.zero;
        int nx = x, ny = y, nz = z;

        if (x < 0) { offset.x = -1; nx = size - 1; }
        else if (x >= size) { offset.x = 1; nx = 0; }

        if (z < 0) { offset.z = -1; nz = size - 1; }
        else if (z >= size) { offset.z = 1; nz = 0; }

        if (ny < 0 || ny >= height)
            return 0;

        if (!neighborCache.TryGetValue(offset, out var neighbor) || neighbor == null)
            return 0;

        return neighbor[nx, ny, nz];
    }

    private static Vector3Int GetNeighborOffset(int face) => face switch
    {
        0 => new Vector3Int(-1, 0, 0),
        1 => new Vector3Int(1, 0, 0),
        2 => new Vector3Int(0, 1, 0),
        3 => new Vector3Int(0, -1, 0),
        4 => new Vector3Int(0, 0, -1),
        5 => new Vector3Int(0, 0, 1),
        _ => Vector3Int.zero
    };

    private static Vector3[] GetFaceVertices(Vector3 pos, int face)
    {
        switch (face)
        {
            case 0: return new[] { pos + new Vector3(0, 0, 1), pos + new Vector3(0, 1, 1), pos + new Vector3(0, 1, 0), pos + new Vector3(0, 0, 0) };
            case 1: return new[] { pos + new Vector3(1, 0, 0), pos + new Vector3(1, 1, 0), pos + new Vector3(1, 1, 1), pos + new Vector3(1, 0, 1) };
            case 2: return new[] { pos + new Vector3(0, 1, 1), pos + new Vector3(1, 1, 1), pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0) };
            case 3: return new[] { pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0), pos + new Vector3(1, 0, 1), pos + new Vector3(0, 0, 1) };
            case 4: return new[] { pos + new Vector3(0, 0, 0), pos + new Vector3(0, 1, 0), pos + new Vector3(1, 1, 0), pos + new Vector3(1, 0, 0) };
            case 5: return new[] { pos + new Vector3(1, 0, 1), pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1), pos + new Vector3(0, 0, 1) };
            default: return null;
        }
    }

    private string GetBlockName(byte id) => id switch
    {
        1 => "dirt",
        2 => "grass",
        3 => "stone",
        4 => "water",
        _ => "dirt"
    };

    public void SetBlock(Vector3Int localPos, byte id)
    {
        if (localPos.x < 0 || localPos.x >= size ||
            localPos.y < 0 || localPos.y >= blocks.GetLength(1) ||
            localPos.z < 0 || localPos.z >= size)
            return;

        blocks[localPos.x, localPos.y, localPos.z] = id;
        needsRebuild = true;
    }

    void LateUpdate()
    {
        if (needsRebuild && meshBuilt)
        {
            needsRebuild = false;
            BuildMeshFast();
            NotifyNeighbors();
        }
    }

    public void Rebuild()
    {
        if (meshBuilt) BuildMeshFast();
    }

    private void NotifyNeighbors()
    {
        Vector3Int[] dirs =
        {
            new Vector3Int(-1,0,0),
            new Vector3Int(1,0,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1)
        };

        foreach (var dir in dirs)
        {
            Chunk neighbor = worldRef.GetChunk(chunkCoord + dir);
            if (neighbor != null && neighbor.neighborsCached)
                neighbor.Rebuild();
        }
    }
}
