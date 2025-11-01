using UnityEngine;

[System.Serializable]
public class Biome
{
    public string name;
    public float temperature;  // 0 = frío, 1 = cálido
    public float humidity;     // 0 = seco, 1 = húmedo
    public Color grassTint;
    public byte topBlock;      // capa superior (ej: pasto)
    public byte fillerBlock;   // capa interna (ej: tierra)
    public byte stoneBlock;    // capa más profunda

    public Biome(string name, float temperature, float humidity, Color tint,
                 byte top, byte filler, byte stone)
    {
        this.name = name;
        this.temperature = temperature;
        this.humidity = humidity;
        this.grassTint = tint;
        this.topBlock = top;
        this.fillerBlock = filler;
        this.stoneBlock = stone;
    }
}
