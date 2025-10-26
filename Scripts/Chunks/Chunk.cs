using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public int size = 16;
    public float blockSize = 1f;

    private MeshFilter mf;
    private MeshRenderer mr;
    private MeshCollider mc;

    public WorldGenerator worldRef;
    public Vector3Int chunkCoord;

    private Material defaultMat;
    private Material tintedMat;

    private byte[,,] blocks;
    private Dictionary<Vector3Int, byte[,,]> neighborCache = new();

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mc = GetComponent<MeshCollider>();
    }

    public Material material
    {
        get => mr != null ? mr.sharedMaterial : null;
        set { if (mr != null) mr.sharedMaterial = value; }
    }

    public void SetData(byte[,,] data, Vector3Int coord, WorldGenerator world)
    {
        Initialize(data, coord, world);
    }

    public void Initialize(byte[,,] data, Vector3Int coord, WorldGenerator world)
    {
        blocks = data;
        chunkCoord = coord;
        worldRef = world;
        size = data.GetLength(0);

        // 🔹 Obtener materiales desde el AtlasBuilder
        defaultMat = AtlasBuilder.Instance.GetMaterialForBlock("stone");
        tintedMat = AtlasBuilder.Instance.GetMaterialForBlock("grass");

        CacheNeighbors();
        BuildMesh();
    }

    private void CacheNeighbors()
    {
        neighborCache.Clear();
        Vector3Int[] dirs = {
            new(-1, 0, 0), new(1, 0, 0),
            new(0, 0, -1), new(0, 0, 1)
        };

        foreach (var dir in dirs)
        {
            Vector3Int nc = chunkCoord + dir;
            neighborCache[nc - chunkCoord] = worldRef.GetChunkBlocks(nc) ?? new byte[size, size, size];
        }
    }

    private void BuildMesh()
    {
        List<Vector3> vertsDefault = new();
        List<int> trisDefault = new();
        List<Vector2> uvsDefault = new();

        List<Vector3> vertsTinted = new();
        List<int> trisTinted = new();
        List<Vector2> uvsTinted = new();

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        for (int z = 0; z < size; z++)
        {
            byte block = blocks[x, y, z];
            if (block == 0) continue;

            BlockInfo info = BlockDatabase.Instance.GetBlock(GetBlockName(block));
            bool isTinted = info.name.Contains("grass") || info.name.Contains("vine") || info.name.Contains("leaves");

            var verts = isTinted ? vertsTinted : vertsDefault;
            var tris = isTinted ? trisTinted : trisDefault;
            var uvs = isTinted ? uvsTinted : uvsDefault;

            for (int face = 0; face < 6; face++)
            {
                Vector3Int neighbor = GetNeighborOffset(face);
                byte neighborBlock = GetBlockGlobal(x + neighbor.x, y + neighbor.y, z + neighbor.z);
                if (neighborBlock != 0) continue;

                AddFace(block, x, y, z, face, verts, tris, uvs);

                if (info.textures.overlay != null && info.textures.overlay.Length > 0 && face is 0 or 1 or 4 or 5)
                {
                    string overlayTex = info.textures.overlay[0];
                    AddOverlayFace(x, y, z, face, verts, tris, uvs, overlayTex);
                }
            }
        }

        Mesh mesh = new() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.subMeshCount = 2;

        var vertsCombined = new List<Vector3>();
        vertsCombined.AddRange(vertsDefault);
        vertsCombined.AddRange(vertsTinted);

        mesh.SetVertices(vertsCombined);
        mesh.SetTriangles(trisDefault, 0);
        mesh.SetTriangles(ShiftIndices(trisTinted, vertsDefault.Count), 1);
        mesh.SetUVs(0, CombineUVs(uvsDefault, uvsTinted));

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;

        // 🟩 Usa ambos materiales en el mismo MeshRenderer
        mr.sharedMaterials = new[] { defaultMat, tintedMat };
    }

    private static List<int> ShiftIndices(List<int> list, int offset)
    {
        List<int> shifted = new(list.Count);
        foreach (int i in list) shifted.Add(i + offset);
        return shifted;
    }

    private static List<Vector2> CombineUVs(List<Vector2> a, List<Vector2> b)
    {
        List<Vector2> all = new(a.Count + b.Count);
        all.AddRange(a);
        all.AddRange(b);
        return all;
    }

    // ============================
    // 🧩 Generación de caras
    // ============================
    private void AddFace(byte blockId, int x, int y, int z, int face,
                         List<Vector3> verts, List<int> tris, List<Vector2> uvs)
    {
        int vertIndex = verts.Count;
        Vector3 basePos = new(x, y, z);
        Vector3[] faceVerts = GetFaceVertices(basePos, face);
        verts.AddRange(faceVerts);

        tris.Add(vertIndex + 0);
        tris.Add(vertIndex + 1);
        tris.Add(vertIndex + 2);
        tris.Add(vertIndex + 0);
        tris.Add(vertIndex + 2);
        tris.Add(vertIndex + 3);

        string blockName = GetBlockName(blockId);
        BlockInfo info = BlockDatabase.Instance.GetBlock(blockName);

        string texId = info.textures.all;
        if (face == 2 && info.textures.top != null && info.textures.top.Length > 0)
        {
            int idx = Mathf.FloorToInt(Mathf.Abs(Mathf.Sin(x * 928.5f + z * 517.2f)) * info.textures.top.Length) % info.textures.top.Length;
            texId = info.textures.top[idx];
        }
        else if (face == 3 && info.textures.bottom != null && info.textures.bottom.Length > 0)
            texId = info.textures.bottom[0];
        else if (info.textures.side != null && info.textures.side.Length > 0)
            texId = info.textures.side[0];

        if (string.IsNullOrEmpty(texId)) texId = "dirt";
        Rect uvRect = AtlasBuilder.Instance.GetUV(texId);
        AddFaceUVs(face, uvRect, uvs);
    }

    private void AddOverlayFace(int x, int y, int z, int face,
                                List<Vector3> verts, List<int> tris, List<Vector2> uvs,
                                string overlayTex)
    {
        int vertIndex = verts.Count;
        Vector3 basePos = new(x, y, z);
        Vector3[] faceVerts = GetFaceVertices(basePos, face);

        Vector3 offset = GetNormal(face) * 0.001f;
        for (int i = 0; i < 4; i++)
            faceVerts[i] += offset;

        verts.AddRange(faceVerts);

        tris.Add(vertIndex + 0);
        tris.Add(vertIndex + 1);
        tris.Add(vertIndex + 2);
        tris.Add(vertIndex + 0);
        tris.Add(vertIndex + 2);
        tris.Add(vertIndex + 3);

        Rect uvRect = AtlasBuilder.Instance.GetUV(overlayTex);
        AddFaceUVs(face, uvRect, uvs);
    }

    private void AddFaceUVs(int face, Rect uvRect, List<Vector2> uvs)
    {
        Vector2[] faceUVs = face switch
        {
            0 => new[] { new Vector2(uvRect.xMax, uvRect.yMin), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(uvRect.xMin, uvRect.yMin) },
            1 => new[] { new Vector2(uvRect.xMin, uvRect.yMin), new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMin) },
            2 => new[] { new Vector2(uvRect.xMin, uvRect.yMin), new Vector2(uvRect.xMax, uvRect.yMin), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMin, uvRect.yMax) },
            3 => new[] { new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMin), new Vector2(uvRect.xMin, uvRect.yMin) },
            4 => new[] { new Vector2(uvRect.xMax, uvRect.yMin), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(uvRect.xMin, uvRect.yMin) },
            5 => new[] { new Vector2(uvRect.xMin, uvRect.yMin), new Vector2(uvRect.xMin, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMax), new Vector2(uvRect.xMax, uvRect.yMin) },
            _ => null
        };
        uvs.AddRange(faceUVs);
    }

    private static Vector3 GetNormal(int face) => face switch
    {
        0 => Vector3.left,
        1 => Vector3.right,
        2 => Vector3.up,
        3 => Vector3.down,
        4 => Vector3.back,
        5 => Vector3.forward,
        _ => Vector3.zero
    };

    private static Vector3[] GetFaceVertices(Vector3 pos, int face) => face switch
    {
        0 => new[] { pos + new Vector3(0, 0, 1), pos + new Vector3(0, 1, 1), pos + new Vector3(0, 1, 0), pos + new Vector3(0, 0, 0) },
        1 => new[] { pos + new Vector3(1, 0, 0), pos + new Vector3(1, 1, 0), pos + new Vector3(1, 1, 1), pos + new Vector3(1, 0, 1) },
        2 => new[] { pos + new Vector3(0, 1, 1), pos + new Vector3(1, 1, 1), pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0) },
        3 => new[] { pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0), pos + new Vector3(1, 0, 1), pos + new Vector3(0, 0, 1) },
        4 => new[] { pos + new Vector3(0, 0, 0), pos + new Vector3(0, 1, 0), pos + new Vector3(1, 1, 0), pos + new Vector3(1, 0, 0) },
        5 => new[] { pos + new Vector3(1, 0, 1), pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1), pos + new Vector3(0, 0, 1) },
        _ => null
    };

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

    private byte GetBlockGlobal(int x, int y, int z)
    {
        if (x >= 0 && x < size && y >= 0 && y < size && z >= 0 && z < size)
            return blocks[x, y, z];

        Vector3Int offset = Vector3Int.zero;
        int nx = x, ny = y, nz = z;

        if (x < 0) { offset.x = -1; nx = size - 1; }
        else if (x >= size) { offset.x = 1; nx = 0; }

        if (z < 0) { offset.z = -1; nz = size - 1; }
        else if (z >= size) { offset.z = 1; nz = 0; }

        if (!neighborCache.TryGetValue(offset, out var neighbor) || neighbor == null)
            return 0;

        if (ny < 0 || ny >= neighbor.GetLength(1) ||
            nx < 0 || nx >= neighbor.GetLength(0) ||
            nz < 0 || nz >= neighbor.GetLength(2))
            return 0;

        return neighbor[nx, ny, nz];
    }

    private string GetBlockName(byte id) => id switch
    {
        1 => "dirt",
        2 => "grass",
        3 => "stone",
        _ => "dirt"
    };
}
