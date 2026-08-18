using System.IO;
using UnityEditor;
using UnityEngine;

public static class TextureGenerator
{
    [MenuItem("Tools/Generate Okami Sun")]
    public static void GenerateSunTextures()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color redColor = new Color(1f, 0.25f, 0.1f, 1f);
        Color clearColor = Color.clear;
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                bool isRay = Mathf.Cos(angle * 12f) > 0.3f;
                bool isInsideRing = distance <= size / 2f - 10f && distance >= size / 6f;

                texture.SetPixel(x, y, isRay && isInsideRing ? redColor : clearColor);
            }
        }

        texture.Apply();
        string path = Path.Combine(Application.dataPath, "OkamiSunRays.png");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();
        Debug.Log("Generated Okami sun texture at: " + path);
    }
}
