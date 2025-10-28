using UnityEngine;

public class CrosshairUI : MonoBehaviour
{
    private void OnGUI()
    {
        float size = 8f;
        float x = (Screen.width - size) / 2;
        float y = (Screen.height - size) / 2;

        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
    }
}
