using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class OkamiTrailFlowerRenderer : MonoBehaviour
{
    [Serializable]
    private struct FlowerInstance
    {
        public Vector3 position;
        public Quaternion rotation;
        public float scale;
        public int meshIndex;
        public float spawnTime;
        public float forcedFadeStartTime;
        public bool petalsEmitted;
    }

    [Header("Assets")]
    public Mesh[] flowerMeshes;
    public Material flowerMaterial;

    [Header("Distance LOD")]
    public Transform distanceReference;
    public float visibleDistance = 7.5f;
    public int maxFlowers = 220;

    [Header("Lifetime")]
    public float flowerGrowDuration = 0.6f;
    public float flowerLifetime = 5f;
    public float flowerFadeDuration = 1.5f;
    public float flowerSinkDepth = 0.35f;
    public int petalsPerFlower = 4;
    public Color petalColorA = new Color(1f, 0.58f, 0.76f, 1f);
    public Color petalColorB = new Color(1f, 0.86f, 0.42f, 1f);

    [Header("Source Model")]
    [Tooltip("The Quaternius FBX stores centimetre-scale meshes under x75/x100 child transforms.")]
    public float sourceModelScale = 100f;

    private const int MaxInstancesPerDraw = 1023;
    private readonly List<FlowerInstance> flowers = new List<FlowerInstance>();
    private List<Matrix4x4>[] visibleMatricesByMesh;
    private readonly Matrix4x4[] drawBatch = new Matrix4x4[MaxInstancesPerDraw];
    private System.Random petalRandom;
    private GameObject runtimePetalObject;
    private ParticleSystem runtimePetalParticles;
    private Mesh runtimePetalMesh;
    private Material runtimePetalMaterial;

    public int FlowerCount
    {
        get { return flowers.Count; }
    }

    private void OnEnable()
    {
        EnsureGroups();
        petalRandom = new System.Random(5661);
    }

    private void OnDisable()
    {
        DestroyRuntimeObject(runtimePetalObject);
        DestroyRuntimeObject(runtimePetalMaterial);
        DestroyRuntimeObject(runtimePetalMesh);
        runtimePetalObject = null;
        runtimePetalParticles = null;
        runtimePetalMaterial = null;
        runtimePetalMesh = null;
    }

    public void AddFlower(Vector3 position, Quaternion rotation, float scale, int meshIndex, float spawnDelay = 0f)
    {
        if (flowerMeshes == null || flowerMeshes.Length == 0)
            return;

        FlowerInstance instance = new FlowerInstance
        {
            position = position,
            rotation = rotation * Quaternion.Euler(-90f, 0f, 0f),
            scale = Mathf.Max(0.05f, scale),
            meshIndex = ((meshIndex % flowerMeshes.Length) + flowerMeshes.Length) % flowerMeshes.Length,
            spawnTime = Application.isPlaying ? Time.time + Mathf.Max(0f, spawnDelay) : 0f,
            forcedFadeStartTime = -1f,
            petalsEmitted = false
        };
        flowers.Add(instance);

        int overflow = flowers.Count - Mathf.Max(1, maxFlowers);
        if (overflow > 0)
            flowers.RemoveRange(0, overflow);
    }

    public void ClearFlowers()
    {
        flowers.Clear();
        if (runtimePetalParticles != null)
            runtimePetalParticles.Clear(true);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        Camera camera = Camera.main;
        RenderNow(camera);
    }

    public void RenderNow(Camera camera)
    {
        if (flowerMaterial == null || flowerMeshes == null || flowerMeshes.Length == 0)
            return;

        if (camera == null)
            return;

        EnsureGroups();
        for (int i = 0; i < visibleMatricesByMesh.Length; i++)
            visibleMatricesByMesh[i].Clear();

        Vector3 referencePosition = distanceReference != null
            ? distanceReference.position
            : camera.transform.position;
        float visibleDistanceSquared = visibleDistance * visibleDistance;
        float modelScale = Mathf.Max(0.001f, sourceModelScale);
        float currentTime = Application.isPlaying ? Time.time : 0f;
        float growDuration = Mathf.Max(0.01f, flowerGrowDuration);
        float life = Mathf.Max(0f, flowerLifetime);
        float fadeDuration = Mathf.Max(0.01f, flowerFadeDuration);
        float fadeStart = growDuration + life;

        for (int i = flowers.Count - 1; i >= 0; i--)
        {
            FlowerInstance flower = flowers[i];
            float age = Mathf.Max(0f, currentTime - flower.spawnTime);
            bool isInsideNearRange = (flower.position - referencePosition).sqrMagnitude <= visibleDistanceSquared;
            if (Application.isPlaying && !isInsideNearRange && flower.forcedFadeStartTime < 0f)
            {
                if (!flower.petalsEmitted)
                    EmitPetals(flower.position, flower.scale);
                flower.petalsEmitted = true;
                flower.forcedFadeStartTime = currentTime;
                flowers[i] = flower;
            }

            if (Application.isPlaying && age >= fadeStart && !flower.petalsEmitted)
            {
                if (isInsideNearRange)
                    EmitPetals(flower.position, flower.scale);
                flower.petalsEmitted = true;
                flowers[i] = flower;
            }

            float fadeElapsed = flower.forcedFadeStartTime >= 0f
                ? Mathf.Max(0f, currentTime - flower.forcedFadeStartTime)
                : Mathf.Max(0f, age - fadeStart);
            if (Application.isPlaying && fadeElapsed >= fadeDuration)
            {
                flowers.RemoveAt(i);
                continue;
            }

            if (!Application.isPlaying && !isInsideNearRange)
                continue;

            float growScale = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / growDuration));
            float lifeScale = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeElapsed / fadeDuration));
            float finalScale = modelScale * flower.scale * growScale * lifeScale;
            Vector3 renderPosition = flower.position - Vector3.up * ((1f - lifeScale) * Mathf.Max(0f, flowerSinkDepth));
            visibleMatricesByMesh[flower.meshIndex].Add(Matrix4x4.TRS(
                renderPosition,
                flower.rotation,
                new Vector3(finalScale, finalScale, finalScale)));
        }

        for (int meshIndex = 0; meshIndex < flowerMeshes.Length; meshIndex++)
            DrawMeshGroup(camera, flowerMeshes[meshIndex], visibleMatricesByMesh[meshIndex]);
    }

    private void EnsureGroups()
    {
        int meshCount = flowerMeshes != null ? flowerMeshes.Length : 0;
        if (visibleMatricesByMesh != null && visibleMatricesByMesh.Length == meshCount)
            return;

        visibleMatricesByMesh = new List<Matrix4x4>[meshCount];
        for (int i = 0; i < meshCount; i++)
            visibleMatricesByMesh[i] = new List<Matrix4x4>();
    }

    private void DrawMeshGroup(Camera camera, Mesh mesh, List<Matrix4x4> matrices)
    {
        if (mesh == null || matrices == null || matrices.Count == 0)
            return;

        for (int start = 0; start < matrices.Count; start += MaxInstancesPerDraw)
        {
            int count = Mathf.Min(MaxInstancesPerDraw, matrices.Count - start);
            matrices.CopyTo(start, drawBatch, 0, count);
            Graphics.DrawMeshInstanced(
                mesh,
                0,
                flowerMaterial,
                drawBatch,
                count,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                camera,
                LightProbeUsage.Off,
                null);
        }
    }

    private void EmitPetals(Vector3 position, float flowerScale)
    {
        EnsurePetalSystem();
        if (runtimePetalParticles == null)
            return;

        int count = Mathf.Max(0, petalsPerFlower);
        for (int i = 0; i < count; i++)
        {
            float angle = RandomRange(0f, Mathf.PI * 2f);
            float horizontalSpeed = RandomRange(0.18f, 0.48f);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            emit.position = position + Vector3.up * RandomRange(0.18f, 0.42f) * flowerScale;
            emit.velocity = new Vector3(
                Mathf.Cos(angle) * horizontalSpeed,
                RandomRange(0.35f, 0.75f),
                Mathf.Sin(angle) * horizontalSpeed);
            emit.startLifetime = RandomRange(0.9f, 1.5f);
            emit.startSize = RandomRange(0.08f, 0.15f) * flowerScale;
            emit.startColor = Color.Lerp(petalColorA, petalColorB, Random01());
            emit.rotation = RandomRange(0f, Mathf.PI * 2f);
            runtimePetalParticles.Emit(emit, 1);
        }
    }

    private void EnsurePetalSystem()
    {
        if (runtimePetalParticles != null)
            return;

        Shader shader = Shader.Find("Okami/PetalParticle");
        if (shader == null)
            return;

        runtimePetalObject = new GameObject("Trail Petal Particles (Runtime)");
        runtimePetalObject.transform.SetParent(transform, false);
        runtimePetalParticles = runtimePetalObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = runtimePetalParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        main.startSpeed = 0f;
        main.startLifetime = 1.2f;
        main.startSize = 0.12f;
        main.gravityModifier = 0.12f;

        ParticleSystem.EmissionModule emission = runtimePetalParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = runtimePetalParticles.shape;
        shape.enabled = false;
        ParticleSystem.NoiseModule noise = runtimePetalParticles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.16f;
        noise.frequency = 0.65f;
        noise.scrollSpeed = 0.25f;

        runtimePetalMesh = CreatePetalMesh();
        runtimePetalMaterial = new Material(shader) { name = "Trail Petal Runtime Material" };
        runtimePetalMaterial.enableInstancing = true;
        ParticleSystemRenderer particleRenderer = runtimePetalObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = runtimePetalMesh;
        particleRenderer.sharedMaterial = runtimePetalMaterial;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        runtimePetalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        runtimePetalParticles.Play(true);
    }

    private static Mesh CreatePetalMesh()
    {
        Mesh mesh = new Mesh { name = "Trail Petal" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0f, 0.28f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(0f, -0.28f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(0.5f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float Random01()
    {
        if (petalRandom == null)
            petalRandom = new System.Random(5661);
        return (float)petalRandom.NextDouble();
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, Random01());
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
