using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Copies a rejected brush stroke onto the gameplay camera, lets the ink pool
/// briefly, then erodes it with a paper-like noise field. The successful brush
/// effects do not use this component.
/// </summary>
public sealed class FailedInkDissolve : MonoBehaviour
{
    private const string ShaderResourcePath = "FailedInk/FailedInkAbsorb";
    private const string ParticleShaderResourcePath = "FailedInk/FailedInkParticle";
    private const string ParticleMaskResourcePath = "FailedInk/brush059";

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int InkColorId = Shader.PropertyToID("_InkColor");
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int BloomId = Shader.PropertyToID("_Bloom");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int TipDrynessId = Shader.PropertyToID("_TipDryness");
    private static readonly int TipLengthId = Shader.PropertyToID("_TipLength");

    private static readonly string[] SourceTextureProperties =
    {
        "Texture2D_8CDD6257",
        "_BaseMap",
        "_MainTex"
    };

    private readonly List<Material> ownedMaterials = new List<Material>();

    private LineRenderer strokeRenderer;
    private Material strokeMaterial;
    private ParticleSystem inkParticles;
    private Vector3[] strokePositions;
    private float originalWidthMultiplier;
    private float sourceStrokeWidth;
    private float totalStrokeLength;
    private float elapsed;
    private int particleBudget;
    private int particlesEmitted;

    private const float EffectDuration = 0.72f;
    private const float EffectLifetime = 1.62f;
    private const float HoldDuration = 0.08f;
    private const float BloomDuration = 0.18f;
    private const float ParticleEmissionStart = 0f;
    private const float ParticleEmissionEnd = 0.72f;
    private const float InitialParticleCoverage = 0.07f;
    private const int ParticlesAcrossStroke = 3;

    private static readonly Color WetInk = new Color(0.018f, 0.014f, 0.010f, 0.98f);
    private static readonly Color DryInk = new Color(0.17f, 0.15f, 0.125f, 0.78f);

    public static void Play(LineRenderer source, Camera camera)
    {
        if (source == null || camera == null || source.positionCount < 2)
            return;

        Shader inkShader = Resources.Load<Shader>(ShaderResourcePath);
        if (inkShader == null)
            inkShader = Shader.Find("Okami/Failed Ink Absorb");

        if (inkShader == null)
        {
            Debug.LogError("Failed ink shader could not be loaded.", source);
            return;
        }

        GameObject effectObject = new GameObject("Failed Ink Dissolve");
        effectObject.layer = LayerMask.NameToLayer("Default");
        effectObject.transform.SetParent(camera.transform, false);

        FailedInkDissolve effect = effectObject.AddComponent<FailedInkDissolve>();
        effect.Initialize(source, camera, inkShader);
    }

    private void Initialize(LineRenderer source, Camera camera, Shader inkShader)
    {
        strokeRenderer = gameObject.AddComponent<LineRenderer>();
        CopyStroke(source, camera, strokeRenderer);

        strokeMaterial = CreateInkMaterial(inkShader, ResolveSourceTexture(source));
        strokeRenderer.material = strokeMaterial;
        originalWidthMultiplier = source.widthMultiplier;
        sourceStrokeWidth = Mathf.Max(0.08f, Mathf.Max(source.startWidth, source.endWidth));

        CreateParticleTrail(source);

        UpdateVisuals();
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        UpdateVisuals();
        EmitInkParticles();

        if (elapsed >= EffectDuration && strokeRenderer != null)
            strokeRenderer.enabled = false;

        if (elapsed >= EffectLifetime)
            Destroy(gameObject);
    }

    private void CopyStroke(LineRenderer source, Camera camera, LineRenderer destination)
    {
        destination.useWorldSpace = false;
        destination.alignment = LineAlignment.View;
        destination.textureMode = LineTextureMode.Stretch;
        destination.numCornerVertices = Mathf.Max(2, source.numCornerVertices);
        // A rounded LineRenderer cap reads as a circular ink blob. The failed
        // stroke needs a dry, fibrous entry/exit, so leave both ends uncapped.
        destination.numCapVertices = 0;
        destination.widthMultiplier = source.widthMultiplier;
        // Preserve the authored silhouette. The tip character comes from
        // broken ink fibres in the shader, not from narrowing the geometry.
        destination.widthCurve = new AnimationCurve(source.widthCurve.keys);
        destination.startColor = Color.white;
        destination.endColor = Color.white;
        destination.shadowCastingMode = ShadowCastingMode.Off;
        destination.receiveShadows = false;
        destination.lightProbeUsage = LightProbeUsage.Off;
        destination.reflectionProbeUsage = ReflectionProbeUsage.Off;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder + 20;

        int pointCount = source.positionCount;
        strokePositions = new Vector3[pointCount];
        totalStrokeLength = 0f;
        destination.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            Vector3 localPosition = camera.transform.InverseTransformPoint(source.GetPosition(i));
            localPosition.z -= 0.025f;
            strokePositions[i] = localPosition;
            destination.SetPosition(i, localPosition);

            if (i > 0)
                totalStrokeLength += Vector3.Distance(strokePositions[i - 1], localPosition);
        }
    }

    private void CreateParticleTrail(LineRenderer source)
    {
        Shader particleShader = Resources.Load<Shader>(ParticleShaderResourcePath);
        if (particleShader == null)
            particleShader = Shader.Find("Okami/Failed Ink Particle");

        if (particleShader == null)
        {
            Debug.LogError("Failed ink particle shader could not be loaded.", this);
            return;
        }

        Texture2D particleMask = Resources.Load<Texture2D>(ParticleMaskResourcePath);
        Material particleMaterial = new Material(particleShader)
        {
            name = "Failed Ink Particles (Runtime)",
            renderQueue = 4001
        };
        particleMaterial.SetTexture(MainTexId, particleMask != null ? particleMask : Texture2D.whiteTexture);
        particleMaterial.SetColor(InkColorId, WetInk);
        ownedMaterials.Add(particleMaterial);

        GameObject particleObject = new GameObject("Ink Fragments");
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(transform, false);

        inkParticles = particleObject.AddComponent<ParticleSystem>();
        inkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float particleDensity = Random.Range(10.5f, 15f);
        particleBudget = Mathf.Clamp(Mathf.RoundToInt(totalStrokeLength * particleDensity), 48, 150);

        ParticleSystem.MainModule main = inkParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = EffectLifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.68f, 1.02f);
        main.startSpeed = 0f;
        main.startSize = sourceStrokeWidth * 0.62f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = particleBudget;
        main.useUnscaledTime = true;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = inkParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = inkParticles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = inkParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.52f, 0.47f, 0.40f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.82f, 0.42f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = inkParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve shrinkCurve = new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.68f, 0.72f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, shrinkCurve);

        ParticleSystem.NoiseModule noise = inkParticles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.22f;
        noise.frequency = 0.68f;
        noise.scrollSpeed = 0.36f;

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = inkParticles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-3.8f, 3.8f);

        ParticleSystemRenderer particleRenderer = inkParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = particleMaterial;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.lightProbeUsage = LightProbeUsage.Off;
        particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        particleRenderer.sortingLayerID = source.sortingLayerID;
        particleRenderer.sortingOrder = source.sortingOrder + 30;

        inkParticles.Play(true);
        EmitInkParticles();
    }

    private void EmitInkParticles()
    {
        if (inkParticles == null || strokePositions == null || strokePositions.Length < 2)
            return;

        float emissionProgress = Mathf.InverseLerp(
            ParticleEmissionStart,
            ParticleEmissionEnd,
            elapsed);
        float coveredProgress = Mathf.Lerp(InitialParticleCoverage, 1f, emissionProgress);
        int targetParticleCount = Mathf.FloorToInt(particleBudget * coveredProgress);

        while (particlesEmitted < targetParticleCount)
        {
            int pathSlotCount = Mathf.CeilToInt((float)particleBudget / ParticlesAcrossStroke);
            int pathSlot = particlesEmitted / ParticlesAcrossStroke;
            int acrossSlot = particlesEmitted % ParticlesAcrossStroke;
            // Randomize inside each ordered path cell. Cells themselves never
            // swap, so the emission still travels from the first point to the
            // final point without producing a mechanical dotted spacing.
            float pathProgress = pathSlotCount > 1
                ? (pathSlot + Random.Range(0.06f, 0.94f)) / pathSlotCount
                : Random.value;
            pathProgress = Mathf.Clamp01(pathProgress);
            Vector3 position;
            Vector3 tangent;
            GetStrokeSample(pathProgress, out position, out tangent);

            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            float acrossProgress = ParticlesAcrossStroke > 1
                ? (float)acrossSlot / (ParticlesAcrossStroke - 1)
                : 0.5f;
            float acrossOffset = Mathf.Lerp(-0.43f, 0.43f, acrossProgress);
            acrossOffset += Random.Range(-0.23f, 0.23f);
            if (Random.value < 0.24f)
                acrossOffset = Random.Range(-0.55f, 0.55f);
            acrossOffset = Mathf.Clamp(acrossOffset, -0.56f, 0.56f);
            position += normal * sourceStrokeWidth * acrossOffset;
            position.z -= 0.035f;

            float sideDirection = Random.value < 0.5f ? -1f : 1f;
            Vector3 velocity = normal * sideDirection * Random.Range(0.16f, 0.36f);
            velocity += tangent * Random.Range(0.035f, 0.13f);
            velocity += Vector3.up * Random.Range(0.12f, 0.30f);

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
            emitParams.position = position;
            emitParams.velocity = velocity;
            float sizeRoll = Random.value;
            float sizeMultiplier;
            if (sizeRoll < 0.2f)
                sizeMultiplier = Random.Range(1.2f, 1.75f);
            else if (sizeRoll < 0.48f)
                sizeMultiplier = Random.Range(0.76f, 1.28f);
            else
                sizeMultiplier = Random.Range(0.46f, 0.98f);

            emitParams.startLifetime = Random.Range(0.58f, 1.13f) + sizeMultiplier * 0.06f;
            emitParams.startSize = Mathf.Clamp(
                sourceStrokeWidth * sizeMultiplier,
                0.10f,
                0.54f);
            emitParams.startColor = new Color(1f, 1f, 1f, Random.Range(0.58f, 1f));
            emitParams.rotation = Random.Range(0f, Mathf.PI * 2f);
            inkParticles.Emit(emitParams, 1);
            particlesEmitted++;
        }
    }

    private void GetStrokeSample(float normalizedDistance, out Vector3 position, out Vector3 tangent)
    {
        if (totalStrokeLength <= 0.0001f)
        {
            position = strokePositions[0];
            tangent = Vector3.right;
            return;
        }

        float targetDistance = Mathf.Clamp01(normalizedDistance) * totalStrokeLength;
        float travelled = 0f;
        for (int i = 1; i < strokePositions.Length; i++)
        {
            Vector3 segment = strokePositions[i] - strokePositions[i - 1];
            float segmentLength = segment.magnitude;
            if (travelled + segmentLength >= targetDistance && segmentLength > 0.0001f)
            {
                float segmentProgress = (targetDistance - travelled) / segmentLength;
                position = Vector3.Lerp(strokePositions[i - 1], strokePositions[i], segmentProgress);
                tangent = segment / segmentLength;
                return;
            }

            travelled += segmentLength;
        }

        position = strokePositions[strokePositions.Length - 1];
        tangent = (strokePositions[strokePositions.Length - 1] -
                   strokePositions[strokePositions.Length - 2]).normalized;
    }

    private Material CreateInkMaterial(Shader shader, Texture texture)
    {
        Material material = new Material(shader)
        {
            name = "Failed Ink (Runtime)",
            renderQueue = 4000
        };

        material.SetTexture(MainTexId, texture != null ? texture : Texture2D.whiteTexture);
        material.SetColor(InkColorId, WetInk);
        material.SetFloat(ProgressId, 0f);
        material.SetFloat(OpacityId, 1f);
        material.SetFloat(BloomId, 0f);
        material.SetFloat(SeedId, Random.Range(0.5f, 100f));
        material.SetFloat(EdgeSoftnessId, 0.085f);
        material.SetFloat(TipDrynessId, 0.92f);
        material.SetFloat(TipLengthId, 0.22f);
        ownedMaterials.Add(material);
        return material;
    }

    private static Texture ResolveSourceTexture(LineRenderer source)
    {
        Material sourceMaterial = source.sharedMaterial;
        if (sourceMaterial == null)
            return Texture2D.whiteTexture;

        foreach (string propertyName in SourceTextureProperties)
        {
            if (!sourceMaterial.HasProperty(propertyName))
                continue;

            Texture texture = sourceMaterial.GetTexture(propertyName);
            if (texture != null)
                return texture;
        }

        return sourceMaterial.mainTexture != null
            ? sourceMaterial.mainTexture
            : Texture2D.whiteTexture;
    }

    private void UpdateVisuals()
    {
        float bloomTime = Mathf.Clamp01(elapsed / BloomDuration);
        float bloom = Mathf.SmoothStep(0f, 1f, bloomTime);
        float dissolve = Mathf.InverseLerp(HoldDuration, EffectDuration, elapsed);
        Color currentInk = Color.Lerp(WetInk, DryInk, dissolve);

        if (strokeRenderer != null)
            strokeRenderer.widthMultiplier = originalWidthMultiplier * Mathf.Lerp(1f, 1.11f, bloom);

        ApplyMaterialState(strokeMaterial, currentInk, dissolve, 1f, bloom * (1f - dissolve * 0.65f));

    }

    private static void ApplyMaterialState(
        Material material,
        Color inkColor,
        float progress,
        float opacity,
        float bloom)
    {
        if (material == null)
            return;

        material.SetColor(InkColorId, inkColor);
        material.SetFloat(ProgressId, progress);
        material.SetFloat(OpacityId, opacity);
        material.SetFloat(BloomId, bloom);
    }

    private void OnDestroy()
    {
        foreach (Material material in ownedMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        ownedMaterials.Clear();
    }
}
