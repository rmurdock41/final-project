using UnityEngine;
using UnityEditor;
using System.IO;

public class TextureGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Generate Okami Sun")] // 在Unity顶部菜单栏创建按钮
    public static void GenerateSunTextures()
    {
        // 1. 设置：生成 512x512 的贴图
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        
        // 颜色：大神的朱红色
        Color redColor = new Color(1f, 0.25f, 0.1f, 1f); 
        Color clearColor = new Color(0, 0, 0, 0);

        Vector2 center = new Vector2(size / 2, size / 2);

        // 2. 逐像素绘制
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                // 核心逻辑：生成12道光束
                // 使用 Cos 函数来决定画光束还是透明
                float rayWidth = Mathf.Cos(angle * Mathf.Deg2Rad * 12); 
                
                // 边缘清晰度 (Cel-shading 风格)
                bool isRay = rayWidth > 0.3f; 

                // 圆形遮罩 (不让光束无限长，限制在圆内)
                float alpha = 1f;
                if (dist > size / 2 - 10) alpha = 0; // 最外圈透明
                else if (dist < size / 6) alpha = 0; // 中间挖空（给圆盘留位置）

                if (isRay && alpha > 0)
                {
                    tex.SetPixel(x, y, redColor);
                }
                else
                {
                    tex.SetPixel(x, y, clearColor);
                }
            }
        }

        // 3. 保存文件
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        string path = Application.dataPath + "/OkamiSunRays.png";
        File.WriteAllBytes(path, bytes);
        
        Debug.Log("贴图已生成！位置: " + path);
        AssetDatabase.Refresh(); // 刷新资源窗口
    }
#endif
}