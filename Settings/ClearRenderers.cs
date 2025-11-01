using UnityEngine;
using UnityEditor;

public class ClearRenderers : EditorWindow
{
    [MenuItem("Tools/Clear Renderer Materials")]
    static void ClearMaterials()
    {
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            r.sharedMaterial = null;
        }
        Debug.Log("✔️ Todos los renderers quedaron sin material asignado.");
    }
}
