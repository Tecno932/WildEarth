using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Estructura de texturas del bloque.
/// </summary>
[System.Serializable]
public class BlockTextureInfo
{
    public string[] top;
    public string[] bottom;
    public string[] side;
    public string[] overlay;
    public string all;
}

/// <summary>
/// Estructura completa de datos de un bloque, extendida.
/// </summary>
[System.Serializable]
public class BlockInfo
{
    public string name;
    public string displayName;
    public string material = "Default";   // 🔹 Material asignado (ej: "Default", "GrassTint")
    public string category = "Generic";   // 🔹 Tipo de bloque: Soil, Rock, Wood, etc.
    public bool solid = true;
    public bool isTransparent = false;
    public bool isFlammable = false;
    public bool isSolid = true;
    public float hardness = 1.0f;         // 🔹 Cuánto tarda en romperse
    public string tool = "Any";           // 🔹 Herramienta ideal
    public string[] drops;                // 🔹 Qué ítems suelta
    public float lightEmission = 0f;      // 🔹 Luz emitida (0–1)
    public BlockTextureInfo textures;
}

/// <summary>
/// Base de datos de todos los bloques cargados desde BlockData.json
/// </summary>
public class BlockDatabase : MonoBehaviour
{
    public static BlockDatabase Instance;
    public Dictionary<string, BlockInfo> blocks;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        LoadBlockData();
    }

    private void LoadBlockData()
    {
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

    /// <summary>
    /// Obtiene el material adecuado según el tipo definido en el JSON.
    /// </summary>
    public Material GetMaterialForBlock(string blockId)
    {
        if (!blocks.TryGetValue(blockId, out var info))
            return AtlasBuilder.Instance.GetSharedMaterial();

        string matType = info.material?.ToLower() ?? "default";

        // ⚙️ Delegamos la elección del material al AtlasBuilder (por si en el futuro se amplía)
        return AtlasBuilder.Instance.GetMaterialForBlock(info.material);
    }
}



///| Campo           | Tipo            | Ejemplo                                | Descripción                              |
///| :-------------- | :-------------- | :------------------------------------- | :--------------------------------------- |
///| `id`            | `int`           | 2                                      | Identificador numérico único             |
///| `displayName`   | `string`        | `"Grass Block"`                        | Nombre visible en UI                     |
///| `material`      | `string`        | `"GrassTint"`                          | Nombre del material/shader asignado      |
///| `category`      | `string`        | `"Soil"`, `"Rock"`, `"Wood"`           | Clasificación general                    |
///| `hardness`      | `float`         | `1.5`                                  | Cuánto tarda en romperse                 |
///| `tool`          | `string`        | `"Pickaxe"`, `"Axe"`, `"Shovel"`       | Herramienta óptima para romperlo         |
///| `drops`         | `string[]`      | `["dirt"]`                             | Bloques o ítems que suelta al romperse   |
///| `lightEmission` | `float`         | `0.8`                                  | Luz emitida (0–1)                        |
///| `isTransparent` | `bool`          | `false`                                | Si se renderiza transparente o no        |
///| `isSolid`       | `bool`          | `true`                                 | Si colisiona o no                        |
///| `isFlammable`   | `bool`          | `true`                                 | Si puede prenderse fuego                 |
///| `textures`      | `BlockTextures` | `{ "top": [...], "side": [...], ... }` | Lo que ya tenés, mantiene compatibilidad |

///soundType → "stone", "wood", "grass" (para sonidos de pisar/romper)
///resistance → resistencia a explosiones
///lightOpacity → cuánta luz bloquea
///renderMode → "Opaque", "Cutout", "Transparent"