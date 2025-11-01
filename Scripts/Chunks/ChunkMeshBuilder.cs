using System.Collections.Generic;
using UnityEngine;

public static class ChunkMeshBuilder
{
    public struct MeshData
    {
        public List<Vector3> vertices;
        public List<int> triangles;
        public List<Vector2> uvs;
    }

    public static MeshData BuildMesh(byte[,,] blocks, int size, System.Func<int,int,int,byte> getNeighbor)
    {
        MeshData mesh = new MeshData
        {
            vertices = new(size * size * 6),
            triangles = new(size * size * 6),
            uvs = new(size * size * 6)
        };

        for (int x = 0; x < size; x++)
        for (int y = 0; y < size; y++)
        for (int z = 0; z < size; z++)
        {
            byte block = blocks[x, y, z];
            if (block == 0) continue;

            for (int face = 0; face < 6; face++)
            {
                Vector3Int dir = GetNeighborOffset(face);
                byte neighbor = getNeighbor(x + dir.x, y + dir.y, z + dir.z);

                if (neighbor != 0) continue; // vecino sólido

                AddFace(mesh, block, x, y, z, face);
            }
        }

        return mesh;
    }

    private static Vector3Int GetNeighborOffset(int face) => face switch
    {
        0 => new(-1,0,0),
        1 => new(1,0,0),
        2 => new(0,1,0),
        3 => new(0,-1,0),
        4 => new(0,0,-1),
        5 => new(0,0,1),
        _ => Vector3Int.zero
    };

    private static void AddFace(MeshData mesh, byte id, int x, int y, int z, int face)
    {
        int vi = mesh.vertices.Count;
        Vector3[] faceVerts = ChunkGeometry.GetFaceVerts(new Vector3(x, y, z), face);
        mesh.vertices.AddRange(faceVerts);
        mesh.triangles.AddRange(new int[] { vi, vi+1, vi+2, vi, vi+2, vi+3 });

        Rect uv = AtlasBuilder.Instance.GetUV("dirt"); // provisional
        mesh.uvs.Add(new Vector2(uv.xMin, uv.yMin));
        mesh.uvs.Add(new Vector2(uv.xMax, uv.yMin));
        mesh.uvs.Add(new Vector2(uv.xMax, uv.yMax));
        mesh.uvs.Add(new Vector2(uv.xMin, uv.yMax));
    }
}
