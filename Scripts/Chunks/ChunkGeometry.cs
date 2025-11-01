using UnityEngine;

public static class ChunkGeometry
{
    public static Vector3[] GetFaceVerts(Vector3 pos, int face)
    {
        return face switch
        {
            0 => new[] { pos + new Vector3(0,0,1), pos + new Vector3(0,1,1), pos + new Vector3(0,1,0), pos + new Vector3(0,0,0) }, // left
            1 => new[] { pos + new Vector3(1,0,0), pos + new Vector3(1,1,0), pos + new Vector3(1,1,1), pos + new Vector3(1,0,1) }, // right
            2 => new[] { pos + new Vector3(0,1,1), pos + new Vector3(1,1,1), pos + new Vector3(1,1,0), pos + new Vector3(0,1,0) }, // top
            3 => new[] { pos + new Vector3(0,0,0), pos + new Vector3(1,0,0), pos + new Vector3(1,0,1), pos + new Vector3(0,0,1) }, // bottom
            4 => new[] { pos + new Vector3(0,0,0), pos + new Vector3(0,1,0), pos + new Vector3(1,1,0), pos + new Vector3(1,0,0) }, // back
            5 => new[] { pos + new Vector3(1,0,1), pos + new Vector3(1,1,1), pos + new Vector3(0,1,1), pos + new Vector3(0,0,1) }, // front
            _ => null
        };
    }
}
 