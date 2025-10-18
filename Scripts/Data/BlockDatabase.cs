using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class BlockTextureInfo
{
    public string[] top;
    public string[] bottom;
    public string[] side;
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

        TextAsset json = Resources.Load<TextAsset>("BlockData");
        if (json == null)
        {
            Debug.LogError("❌ Falta BlockData.json en Resources/");
            return;
        }

        try
        {
            blocks = JsonConvert.DeserializeObject<Dictionary<string, BlockInfo>>(json.text);
            Debug.Log($"✅ Cargados {blocks.Count} bloques desde JSON");
        }
        catch (System.Exception e)
        {
            Debug.LogError("❌ Error al leer JSON: " + e.Message);
        }
    }

    public BlockInfo GetBlock(string id)
    {
        if (blocks == null || !blocks.ContainsKey(id))
        {
            Debug.LogWarning($"⚠ Bloque '{id}' no encontrado");
            return null;
        }
        return blocks[id];
    }
}
