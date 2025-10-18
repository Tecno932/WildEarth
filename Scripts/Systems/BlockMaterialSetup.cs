using UnityEngine;

[ExecuteAlways]
public class BlockMaterialSetup : MonoBehaviour
{
    [Tooltip("Arrastrar el material Blocks.mat aquí (el mismo que usa AtlasBuilder).")]
    public Material targetMaterial;

    void Start()
    {
        if (targetMaterial == null)
        {
            Debug.LogWarning("BlockMaterialSetup: no asignaste targetMaterial en el inspector.");
            return;
        }

        if (AtlasBuilder.Instance == null)
        {
            Debug.LogWarning("BlockMaterialSetup: AtlasBuilder.Instance no está listo aún.");
            return;
        }

        Texture2D atlas = AtlasBuilder.Instance.GetAtlasTexture();
        if (atlas == null)
        {
            Debug.LogWarning("BlockMaterialSetup: atlas aún no generado o es null.");
            return;
        }

        Material inst = Instantiate(targetMaterial);
        inst.name = targetMaterial.name + "_instance";

        if (inst.HasProperty("_MainTex"))
            inst.SetTexture("_MainTex", atlas);

        if (inst.HasProperty("_Color"))
            inst.SetColor("_Color", Color.white);

        if (inst.HasProperty("_Cull"))
            inst.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        else
            inst.SetInt("_CullMode", 0);

        var rend = GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = inst;

        // 🔧 NUEVO método no obsoleto
        Chunk[] chunks = Object.FindObjectsByType<Chunk>(FindObjectsSortMode.None);
        foreach (var c in chunks)
        {
            var mr = c.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = inst;
        }

        Debug.Log($"BlockMaterialSetup: material instanciado y atlas asignado ({atlas.width}x{atlas.height}).");
    }
}
