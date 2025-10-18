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

        StartCoroutine(WaitForBlockDatabase());
    }

    private IEnumerator WaitForBlockDatabase()
    {
        yield return new WaitUntil(() =>
            BlockDatabase.Instance != null &&
            BlockDatabase.Instance.blocks != null);

        BuildAtlas();
    }

    private void BuildAtlas()
    {
        List<Texture2D> textures = new();
        HashSet<string> used = new();

        foreach (var kvp in BlockDatabase.Instance.blocks)
        {
            var texs = kvp.Value.textures;
            AddTextureIfExists(texs.all, used, textures);
            AddTexturesIfExist(texs.top, used, textures);
            AddTexturesIfExist(texs.bottom, used, textures);
            AddTexturesIfExist(texs.side, used, textures);
        }

        atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);
        Rect[] rects = atlas.PackTextures(textures.ToArray(), padding, atlasSize);

        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Repeat;

        uvRects = new Dictionary<string, Rect>();
        for (int i = 0; i < textures.Count; i++)
        {
            string name = textures[i].name;
            uvRects[name] = rects[i];
        }

        sharedMaterial.mainTexture = atlas;
        sharedMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;
    }

    private void AddTextureIfExists(string id, HashSet<string> used, List<Texture2D> list)
    {
        if (string.IsNullOrEmpty(id) || used.Contains(id)) return;
        Texture2D tex = Resources.Load<Texture2D>(id);
        if (tex == null)
        {
            Debug.LogWarning($"❌ No encontré textura: {id}");
            return;
        }

        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        used.Add(id);
        list.Add(tex);
    }

    private void AddTexturesIfExist(string[] ids, HashSet<string> used, List<Texture2D> list)
    {
        if (ids == null) return;
        foreach (var id in ids)
            AddTextureIfExists(id, used, list);
    }

    public Rect GetUV(string name)
    {
        if (uvRects == null || !uvRects.ContainsKey(name))
        {
            Debug.LogWarning($"⚠ UV no encontrado: {name}");
            return new Rect(0, 0, 1, 1);
        }
        return uvRects[name];
    }

    public Material GetSharedMaterial() => sharedMaterial;

    // ✅ 🔽 Este método faltaba 🔽
    public Texture2D GetAtlasTexture()
    {
        return atlas;
    }
}
