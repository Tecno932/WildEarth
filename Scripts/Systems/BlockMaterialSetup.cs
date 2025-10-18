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

        // Instanciamos el material para no modificar el asset original
        Material inst = Instantiate(targetMaterial);
        inst.name = targetMaterial.name + "_instance";

        // Asignar atlas como textura principal
        if (inst.HasProperty("_MainTex"))
            inst.SetTexture("_MainTex", atlas);

        // Forzar color blanco (albedo) si existe la propiedad
        if (inst.HasProperty("_Color"))
            inst.SetColor("_Color", Color.white);

        // Intentamos desactivar culling para debug (funciona con shaders que exponen _Cull)
        if (inst.HasProperty("_Cull"))
            inst.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        else
        {
            // fallback: intentar cambiar modo de renderizado (algunos shaders no usan _Cull)
            inst.SetInt("_CullMode", 0);
        }

        // Aplicar el material instanciado donde haga falta: al GameObject que tenga este script lo aplicamos
        var rend = GetComponent<Renderer>();
        if (rend != null)
            rend.sharedMaterial = inst;

        // También podemos aplicarlo a todos los chunks existentes para debug:
        Chunk[] chunks = FindObjectsOfType<Chunk>();
        foreach (var c in chunks)
        {
            var mr = c.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = inst;
        }

        Debug.Log($"BlockMaterialSetup: material instanciado y atlas asignado ({atlas.width}x{atlas.height}).");
    }
}
