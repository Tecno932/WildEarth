using UnityEngine;
using UnityEditor;

public class ClearRenderers : EditorWindow
{
    [MenuItem("Tools/Clear Renderer Materials")]
    static void ClearMaterials()
    {
        var renderers = GameObject.FindObjectsOfType<MeshRenderer>();
        foreach (var r in renderers)
        {
            r.sharedMaterial = null;
        }
        Debug.Log("✔️ Todos los renderers quedaron sin material asignado.");
    }
}
