using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    [HideInInspector] public int size = 16;
    public float blockSize = 1f;
    [HideInInspector] public byte[,,] blocks;
    public Material material;

    private MeshFilter mf;
    private MeshRenderer mr;
    private MeshCollider mc;

    public WorldGenerator worldRef;
    public Vector3Int chunkCoord;

    // === Vértices del cubo (en sentido antihorario, normales hacia afuera) ===
    private static readonly Vector3[] voxelVerts = new Vector3[8] {
        new Vector3(0, 0, 0), // 0
        new Vector3(1, 0, 0), // 1
        new Vector3(1, 1, 0), // 2
        new Vector3(0, 1, 0), // 3
        new Vector3(0, 0, 1), // 4
        new Vector3(1, 0, 1), // 5
        new Vector3(1, 1, 1), // 6
        new Vector3(0, 1, 1)  // 7
    };

    // === Caras del cubo (normales hacia afuera) ===
    // Orden: BACK, FRONT, TOP, BOTTOM, LEFT, RIGHT
    private static readonly int[,] voxelTris = new int[6, 6] {
        {0, 3, 1, 1, 3, 2}, // Back  (Z-)
        {5, 6, 4, 4, 6, 7}, // Front (Z+)
        {3, 7, 2, 2, 7, 6}, // Top   (Y+)
        {1, 5, 0, 0, 5, 4}, // Bottom(Y-)
        {4, 7, 0, 0, 7, 3}, // Left  (X-)
        {1, 2, 5, 5, 2, 6}  // Right (X+)
    };

    private static readonly Vector2[] voxelUvs = new Vector2[6] {
        new Vector2(0, 0),
        new Vector2(0, 1),
        new Vector2(1, 0),
        new Vector2(1, 0),
        new Vector2(0, 1),
        new Vector2(1, 1)
    };

    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        mc = GetComponent<MeshCollider>();
    }

    public void SetData(byte[,,] data, Vector3Int coord, WorldGenerator world)
    {
        blocks = data;
        chunkCoord = coord;
        worldRef = world;
        size = data.GetLength(0);

        if (material != null)
            mr.sharedMaterial = material;
        else if (AtlasBuilder.Instance != null)
            mr.sharedMaterial = AtlasBuilder.Instance.GetSharedMaterial();

        BuildMesh();
    }

    private bool IsSolidGlobal(int x, int y, int z)
    {
        if (x >= 0 && x < size && y >= 0 && y < size && z >= 0 && z < size)
            return blocks[x, y, z] != 0;

        if (worldRef == null) return false;

        Vector3Int worldBlockPos = new Vector3Int(
            chunkCoord.x * size + x,
            y,
            chunkCoord.z * size + z
        );

        byte id = worldRef.GetBlockAt(worldBlockPos);
        return id != 0;
    }

    private void BuildMesh()
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int vertexIndex = 0;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int z = 0; z < size; z++)
                {
                    byte blockId = blocks[x, y, z];
                    if (blockId == 0) continue;

                    Vector3 blockPos = new Vector3(x, y, z) * blockSize;

                    for (int face = 0; face < 6; face++)
                    {
                        if (CheckNeighborSolid(face, x, y, z)) continue;

                        // 🔹 Añadir vértices, triángulos y UVs
                        for (int i = 0; i < 6; i++)
                        {
                            int triIndex = voxelTris[face, i];
                            verts.Add(blockPos + voxelVerts[triIndex] * blockSize);
                            tris.Add(vertexIndex);
                            vertexIndex++;

                            // --- TEXTURA SEGÚN LA CARA ---
                            string blockName = GetBlockName(blockId);
                            BlockInfo info = BlockDatabase.Instance.GetBlock(blockName);

                            string texId = face switch
                            {
                                2 => info.textures.top != null && info.textures.top.Length > 0 ? info.textures.top[0] : info.textures.all, // top
                                3 => info.textures.bottom ?? info.textures.all, // bottom
                                _ => info.textures.side ?? info.textures.all   // laterales
                            };

                            if (string.IsNullOrEmpty(texId))
                                texId = "dirt";

                            Rect uvRect = AtlasBuilder.Instance.GetUV(texId);

                            Vector2 baseUV = voxelUvs[i];
                            Vector2 atlasUV = new Vector2(
                                Mathf.Lerp(uvRect.xMin, uvRect.xMax, baseUV.x),
                                Mathf.Lerp(uvRect.yMin, uvRect.yMax, baseUV.y)
                            );
                            uvs.Add(atlasUV);
                        }
                    }
                }
            }
        }

        Mesh mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
    }

    private bool CheckNeighborSolid(int face, int x, int y, int z)
    {
        return face switch
        {
            0 => IsSolidGlobal(x, y, z - 1), // Back
            1 => IsSolidGlobal(x, y, z + 1), // Front
            2 => IsSolidGlobal(x, y + 1, z), // Top
            3 => IsSolidGlobal(x, y - 1, z), // Bottom
            4 => IsSolidGlobal(x - 1, y, z), // Left
            5 => IsSolidGlobal(x + 1, y, z), // Right
            _ => false
        };
    }

    private string GetBlockName(byte id)
    {
        return id switch
        {
            1 => "dirt",
            2 => "grass",
            3 => "stone",
            _ => "dirt"
        };
    }
}
