using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class BlockTextureInfo
{
    public string[] top;
    public string bottom;
    public string side;
    public string all;
}

[System.Serializable]
public class BlockInfo
{
    public string name;
    public BlockTextureInfo textures;
    public bool solid;
}

public class BlockDatabase : MonoBehaviour
{
    public static BlockDatabase Instance;
    public Dictionary<string, BlockInfo> blocks;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        Debug.Log("📂 BlockDatabase → Cargando BlockData.json...");
        TextAsset json = Resources.Load<TextAsset>("BlockData");

        if (json == null)
        {
            Debug.LogError("❌ No encontré BlockData.json en Assets/Resources/");
            return;
        }

        try
        {
            blocks = JsonConvert.DeserializeObject<Dictionary<string, BlockInfo>>(json.text);
            Debug.Log($"✅ BlockDatabase → cargados {blocks.Count} bloques desde JSON");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Error al parsear BlockData.json: " + e.Message);
        }
    }

    public BlockInfo GetBlock(string id)
    {
        if (blocks == null)
        {
            Debug.LogError("❌ BlockDatabase no está inicializado");
            return null;
        }

        if (blocks.ContainsKey(id))
        {
            Debug.Log($"🔍 BlockDatabase → devolviendo bloque: {id}");
            return blocks[id];
        }

        Debug.LogWarning($"⚠️ Block {id} no encontrado en BlockData.json");
        return null;
    }
}
