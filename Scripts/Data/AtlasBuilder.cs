using UnityEngine;
using System.Collections.Generic;

public class AtlasBuilder : MonoBehaviour
{
    public static AtlasBuilder Instance;

    [Header("Atlas Manual")]
    public Texture2D manualAtlas;      // Asignalo desde el inspector (atlas.png)
    public int cellSize = 16;          // Tamaño de cada bloque en píxeles
    public int columns = 30;           // Número de columnas del atlas (30x30)

    [Header("Materials")]
    public Material defaultMaterial;
    public Material grassTintMaterial;

    private readonly Dictionary<string, Rect> uvRects = new();
    private readonly Dictionary<string, Material> materials = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        BuildManualAtlas();
        BuildMaterials();
    }

    // ============================================================
    // 🧩 GENERACIÓN DE UVS
    // ============================================================
    private void BuildManualAtlas()
    {
        uvRects.Clear();

        if (manualAtlas == null)
            manualAtlas = Resources.Load<Texture2D>("Atlas/atlas");

        if (manualAtlas == null)
        {
            Debug.LogError("❌ No se encontró atlas en Resources/Atlas/atlas.png");
            return;
        }

        // ✅ Configuración pixel-perfect
        manualAtlas.filterMode = FilterMode.Point;
        manualAtlas.wrapMode = TextureWrapMode.Clamp;

        float atlasWidth = manualAtlas.width;
        float atlasHeight = manualAtlas.height;

        // 🟩 UV exactos sin separación
        float uvW = cellSize / atlasWidth;
        float uvH = cellSize / atlasHeight;

        // --- Registro de texturas ---
        uvRects["dirt"] = GetUVRect(0, 0, uvW, uvH);
        uvRects["dirt1"] = GetUVRect(1, 0, uvW, uvH);
        uvRects["dirt2"] = GetUVRect(2, 0, uvW, uvH);
        uvRects["dirt3"] = GetUVRect(3, 0, uvW, uvH);

        uvRects["grass_side"] = GetUVRect(4, 0, uvW, uvH);
        uvRects["grass_side_overlay"] = GetUVRect(6, 0, uvW, uvH);

        uvRects["grass_top"] = GetUVRect(0, 1, uvW, uvH);
        uvRects["grass_top1"] = GetUVRect(1, 1, uvW, uvH);
        uvRects["grass_top2"] = GetUVRect(2, 1, uvW, uvH);
        uvRects["grass_top3"] = GetUVRect(3, 1, uvW, uvH);
        uvRects["grass_top4"] = GetUVRect(4, 1, uvW, uvH);
        uvRects["grass_top5"] = GetUVRect(5, 1, uvW, uvH);
        uvRects["grass_top6"] = GetUVRect(6, 1, uvW, uvH);

        uvRects["gravel"] = GetUVRect(0, 2, uvW, uvH);
        uvRects["gravel1"] = GetUVRect(1, 2, uvW, uvH);
        uvRects["gravel2"] = GetUVRect(2, 2, uvW, uvH);
        uvRects["gravel3"] = GetUVRect(3, 2, uvW, uvH);

        uvRects["sand"] = GetUVRect(4, 2, uvW, uvH);
        uvRects["sand1"] = GetUVRect(5, 2, uvW, uvH);
        uvRects["sand2"] = GetUVRect(6, 2, uvW, uvH);

        uvRects["stone"] = GetUVRect(0, 3, uvW, uvH);
        uvRects["stone1"] = GetUVRect(1, 3, uvW, uvH);
        uvRects["limestone"] = GetUVRect(4, 3, uvW, uvH);
        uvRects["andesite"] = GetUVRect(5, 3, uvW, uvH);
        uvRects["granite"] = GetUVRect(6, 3, uvW, uvH);

        Debug.Log($"✅ Atlas manual cargado correctamente ({atlasWidth}x{atlasHeight}) con {uvRects.Count} texturas.");
    }

    private Rect GetUVRect(int col, int row, float uvW, float uvH)
    {
        float u = col * uvW;
        float v = 1f - (row + 1) * uvH; // Invertido verticalmente (Unity)
        return new Rect(u, v, uvW, uvH);
    }

    // ============================================================
    // 🎨 MATERIALES
    // ============================================================
    private void BuildMaterials()
    {
        materials.Clear();

        if (manualAtlas == null)
        {
            Debug.LogError("❌ No se encontró el atlas para asignar materiales.");
            return;
        }

        // === Material base ===
        if (defaultMaterial != null)
        {
            Material mat = new(defaultMaterial);
            mat.name = "DefaultAtlasMat";
            mat.mainTexture = manualAtlas;
            mat.SetTexture("_BaseMap", manualAtlas);
            mat.mainTexture.filterMode = FilterMode.Point;
            mat.mainTexture.wrapMode = TextureWrapMode.Clamp;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);

            materials["default"] = mat;
        }
        else
        {
            Debug.LogWarning("⚠ No se asignó Default Material en el AtlasBuilder.");
        }

        // === Material con tinte de pasto ===
        if (grassTintMaterial != null)
        {
            Material mat = new(grassTintMaterial);
            mat.name = "GrassTintAtlasMat";
            mat.mainTexture = manualAtlas;
            mat.SetTexture("_BaseMap", manualAtlas);
            mat.mainTexture.filterMode = FilterMode.Point;
            mat.mainTexture.wrapMode = TextureWrapMode.Clamp;
            mat.EnableKeyword("_BASEMAP");

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_TintStrength"))
                mat.SetFloat("_TintStrength", 1.0f);
            if (mat.HasProperty("_AlphaCutoff"))
                mat.SetFloat("_AlphaCutoff", 0.3f);

            materials["grasstint"] = mat;
        }
        else
        {
            Debug.LogWarning("⚠ No se asignó GrassTint Material en el AtlasBuilder.");
        }

        Debug.Log("✅ Materiales del atlas listos: " + string.Join(", ", materials.Keys));
    }

    // ============================================================
    // 🔎 UTILIDADES
    // ============================================================
    public Rect GetUV(string name)
    {
        if (uvRects.TryGetValue(name, out Rect rect))
            return rect;

        Debug.LogWarning($"⚠ UV no encontrado: {name}");
        return new Rect(0, 0, 1, 1);
    }

    public Material GetMaterialForBlock(string materialType)
    {
        if (string.IsNullOrEmpty(materialType))
            return materials["default"];

        string key = materialType.ToLower();

        if (materials.ContainsKey(key))
            return materials[key];

        return materials["default"];
    }

    // 🔹 Requeridos por tus otros scripts
    public Texture2D GetAtlasTexture() => manualAtlas;
    public Material GetSharedMaterial() =>
        materials.ContainsKey("default") ? materials["default"] : null;
}
