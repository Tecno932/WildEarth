using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AtlasBuilder : MonoBehaviour
{
    public static AtlasBuilder Instance;

    public Material sharedMaterial;
    public int atlasSize = 2048;
    public int padding = 2;

    private Texture2D atlas;
    private Dictionary<string, Rect> uvRects;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 👇 Ahora esperamos a que el BlockDatabase esté cargado
        StartCoroutine(WaitForBlockDatabase());
    }

    private IEnumerator WaitForBlockDatabase()
    {
        Debug.Log("⏳ Esperando que BlockDatabase esté listo...");
        yield return new WaitUntil(() => BlockDatabase.Instance != null && BlockDatabase.Instance.blocks != null);
        Debug.Log("✅ BlockDatabase listo, construyendo atlas...");
        BuildAtlas();
    }

    private void BuildAtlas()
    {
        Debug.Log("🎨 AtlasBuilder → construyendo atlas de texturas...");

        List<Texture2D> texList = new List<Texture2D>();
        HashSet<string> loadedIds = new HashSet<string>();

        foreach (var kvp in BlockDatabase.Instance.blocks)
        {
            var block = kvp.Value;
            List<string> ids = new List<string>();

            if (block.textures.all != null) ids.Add(block.textures.all);
            if (block.textures.top != null) ids.AddRange(block.textures.top);
            if (block.textures.bottom != null) ids.Add(block.textures.bottom);
            if (block.textures.side != null) ids.Add(block.textures.side);

            foreach (var id in ids)
            {
                if (loadedIds.Contains(id)) continue;

                Texture2D tex = Resources.Load<Texture2D>(id);
                if (tex != null)
                {
                    texList.Add(tex);
                    loadedIds.Add(id);
                    Debug.Log($"✅ Cargada textura {id}");
                }
                else
                {
                    Debug.LogError($"❌ No encontré textura '{id}' en Resources/");
                }
            }
        }

        if (texList.Count == 0)
        {
            Debug.LogError("❌ No se cargó ninguna textura. Revisa los nombres en BlockData.json y en Resources/");
            return;
        }

        atlas = new Texture2D(atlasSize, atlasSize);
        Rect[] rects = atlas.PackTextures(texList.ToArray(), padding, atlasSize);

        uvRects = new Dictionary<string, Rect>();
        for (int i = 0; i < texList.Count; i++)
        {
            string name = texList[i].name;
            uvRects[name] = rects[i];
            Debug.Log($"📌 Registrando UVs para {name}");
        }

        sharedMaterial.mainTexture = atlas;
        Debug.Log("🎉 AtlasBuilder → atlas generado con éxito.");
    }

    public Rect GetUV(string texName)
    {
        if (uvRects == null)
        {
            Debug.LogError("❌ uvRects está vacío, atlas no se construyó.");
            return new Rect(0, 0, 1, 1);
        }

        if (uvRects.ContainsKey(texName))
            return uvRects[texName];

        Debug.LogError($"❌ UV no encontrado para textura '{texName}'");
        return new Rect(0, 0, 1, 1);
    }

    public Material GetSharedMaterial()
    {
        if (sharedMaterial == null)
            Debug.LogWarning("⚠ sharedMaterial es null.");
        return sharedMaterial;
    }

    public Texture2D GetAtlasTexture()
    {
        if (atlas == null)
            Debug.LogWarning("⚠ GetAtlasTexture llamado antes de generar atlas.");
        return atlas;
    }
}
