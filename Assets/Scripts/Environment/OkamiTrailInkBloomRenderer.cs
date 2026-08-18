using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class OkamiTrailInkBloomRenderer : MonoBehaviour
{
    [Serializable]
    private struct BloomInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public float spawnTime;
        public float scale;
        public float seed;
        public bool accent;
    }

    [Header("Appearance")]
    public Material bloomMaterial;
    public Color inkColor = new Color(0.08f, 0.10f, 0.055f, 0.46f);
    public Color lifeColor = new Color(0.35f, 0.55f, 0.18f, 0.26f);
    public float bloomDuration = 0.55f;
    public float startRadius = 0.18f;
    public float endRadius = 1.35f;
    public float groundOffset = 0.025f;
    public int maxBlooms = 48;

    private const int MaxInstancesPerDraw = 1023;
    private readonly List<BloomInstance> blooms = new List<BloomInstance>();
    private readonly List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();
    private readonly List<Vector4> visibleBloomData = new List<Vector4>();
    private readonly Matrix4x4[] matrixBatch = new Matrix4x4[MaxInstancesPerDraw];
    private readonly Vector4[] dataBatch = new Vector4[MaxInstancesPerDraw];
    private MaterialPropertyBlock propertyBlock;
    private Material runtimeMaterial;
    private Mesh runtimeMesh;
    private System.Random random;

    public int BloomCount
    {
        get { return blooms.Count; }
    }

    private void OnEnable()
    {
        random = new System.Random(5662);
    }

    private void OnDisable()
    {
        DestroyRuntimeObject(runtimeMaterial);
        DestroyRuntimeObject(runtimeMesh);
        runtimeMaterial = null;
        runtimeMesh = null;
        propertyBlock = null;
    }

    public void AddBloom(Vector3 position, Quaternion rotation, float scale, bool accent)
    {
        if (!Application.isPlaying)
            return;

        if (random == null)
            random = new System.Random(5662);

        blooms.Add(new BloomInstance
        {
            position = position,
            rotation = rotation,
            spawnTime = Time.time,
            scale = Mathf.Max(0.05f, scale),
            seed = (float)random.NextDouble(),
            accent = accent
        });

        int overflow = blooms.Count - Mathf.Max(1, maxBlooms);
        if (overflow > 0)
            blooms.RemoveRange(0, overflow);
    }

    public void ClearBlooms()
    {
        blooms.Clear();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        RenderNow(Camera.main);
    }

    public void RenderNow(Camera camera)
    {
        if (camera == null || !EnsureResources())
            return;

        visibleMatrices.Clear();
        visibleBloomData.Clear();

        float duration = Mathf.Max(0.05f, bloomDuration);
        float currentTime = Time.time;
        for (int i = blooms.Count - 1; i >= 0; i--)
        {
            BloomInstance bloom = blooms[i];
            float progress = Mathf.Max(0f, currentTime - bloom.spawnTime) / duration;
            if (progress >= 1f)
            {
                blooms.RemoveAt(i);
                continue;
            }

            float easedExpansion = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);
            float radius = Mathf.Lerp(
                Mathf.Max(0.01f, startRadius),
                Mathf.Max(0.01f, Mathf.Max(startRadius, endRadius)),
                easedExpansion);
            radius *= bloom.scale * (bloom.accent ? 1.15f : 1f);

            float alpha = progress < 0.16f
                ? Mathf.SmoothStep(0f, 1f, progress / 0.16f)
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 1f, progress));
            Vector3 position = bloom.position + Vector3.up * Mathf.Max(0f, groundOffset);
            visibleMatrices.Add(Matrix4x4.TRS(
                position,
                bloom.rotation,
                new Vector3(radius * 2f, 1f, radius * 2f)));
            visibleBloomData.Add(new Vector4(alpha, bloom.seed, bloom.accent ? 1f : 0f, progress));
        }

        Material material = bloomMaterial != null ? bloomMaterial : runtimeMaterial;
        material.enableInstancing = true;
        material.SetColor("_InkColor", inkColor);
        material.SetColor("_LifeColor", lifeColor);

        for (int start = 0; start < visibleMatrices.Count; start += MaxInstancesPerDraw)
        {
            int count = Mathf.Min(MaxInstancesPerDraw, visibleMatrices.Count - start);
            visibleMatrices.CopyTo(start, matrixBatch, 0, count);
            visibleBloomData.CopyTo(start, dataBatch, 0, count);
            propertyBlock.Clear();
            propertyBlock.SetVectorArray("_BloomData", dataBatch);
            Graphics.DrawMeshInstanced(
                runtimeMesh,
                0,
                material,
                matrixBatch,
                count,
                propertyBlock,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                camera,
                LightProbeUsage.Off,
                null);
        }
    }

    private bool EnsureResources()
    {
        if (runtimeMesh == null)
            runtimeMesh = CreateGroundQuad();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (bloomMaterial == null && runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Okami/TrailInkBloom");
            if (shader == null)
                return false;

            runtimeMaterial = new Material(shader) { name = "Trail Ink Bloom Runtime Material" };
            runtimeMaterial.enableInstancing = true;
        }

        return runtimeMesh != null && (bloomMaterial != null || runtimeMaterial != null);
    }

    private static Mesh CreateGroundQuad()
    {
        Mesh mesh = new Mesh { name = "Trail Ink Bloom Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0.5f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
