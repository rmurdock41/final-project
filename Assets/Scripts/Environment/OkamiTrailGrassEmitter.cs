using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OkamiTrailGrassEmitter : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public InstancedIndirectGrassRenderer grassRenderer;
    public OkamiTrailFlowerRenderer flowerRenderer;
    public OkamiTrailInkBloomRenderer inkBloomRenderer;

    [Header("Grass Trail")]
    public float sampleSpacing = 0.38f;
    public float trailHalfWidth = 1.35f;
    public int grassPerSample = 10;
    public int maxGrassInstances = 5000;

    [Header("Movement Trigger")]
    public bool emitOnlyWhenMoving = true;
    public float minimumMoveSpeed = 0.08f;
    public float spawnBehindDistance = 0.65f;

    [Header("Grass Lifetime")]
    public float grassGrowDuration = 0.7f;
    public float grassGrowStagger = 0.35f;
    public float grassLifetime = 10f;
    public float grassFadeDuration = 2f;
    public float grassSinkDepth = 0.12f;
    public float maxTrailLength = 30f;

    [Header("Flowers")]
    public int flowerEverySamples = 3;
    [Range(0f, 1f)] public float flowerChance = 0.85f;
    public float flowerLateralSpread = 1.05f;
    public int flowerClusterMin = 2;
    public int flowerClusterMax = 4;
    public float flowerClusterRadius = 0.38f;
    public int accentClusterEvery = 5;
    public int accentExtraFlowers = 2;
    public float flowerBloomDelay = 0.15f;
    public Vector2 flowerScaleRange = new Vector2(0.75f, 1.15f);

    [Header("Ground Projection")]
    public LayerMask groundMask = ~0;
    public float rayStartHeight = 5f;
    public float rayDistance = 12f;
    public float groundOffset = 0.015f;

    [Header("Determinism")]
    public int randomSeed = 5660;

    private readonly List<Vector3> grassPositions = new List<Vector3>();
    private readonly List<float> grassSpawnTimes = new List<float>();
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private System.Random random;
    private Vector3 lastSamplePosition;
    private Vector3 lastObservedTargetPosition;
    private bool initialized;
    private int emittedSampleCount;
    private int emittedFlowerClusterCount;
    private bool grassDataDirty;
    private int forcedFadeGrassCount;

    private void Start()
    {
        ResetTrail();
    }

    private void LateUpdate()
    {
        if (target == null || grassRenderer == null)
            return;

        if (!initialized)
            ResetTrail();

        ApplyGrassFadeSettings();
        PruneExpiredGrass();

        Vector3 targetPosition = target.position;
        Vector3 frameDelta = targetPosition - lastObservedTargetPosition;
        frameDelta.y = 0f;
        float moveSpeed = frameDelta.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        lastObservedTargetPosition = targetPosition;
        if (emitOnlyWhenMoving && moveSpeed < Mathf.Max(0f, minimumMoveSpeed))
        {
            // Discard sub-threshold jitter and any leftover spacing while idle so
            // resuming movement never creates a burst underneath the wolf.
            lastSamplePosition = targetPosition;
            SyncGrassRendererIfNeeded();
            return;
        }

        Vector3 flatDelta = targetPosition - lastSamplePosition;
        flatDelta.y = 0f;

        float spacing = Mathf.Max(0.05f, sampleSpacing);
        int safety = 0;
        while (flatDelta.magnitude >= spacing && safety < 64)
        {
            Vector3 direction = flatDelta.normalized;
            Vector3 samplePosition = lastSamplePosition + direction * spacing;
            samplePosition.y = targetPosition.y;
            Vector3 trailPosition = samplePosition - direction * Mathf.Max(0f, spawnBehindDistance);
            EmitSample(trailPosition, direction);
            lastSamplePosition = samplePosition;

            flatDelta = targetPosition - lastSamplePosition;
            flatDelta.y = 0f;
            safety++;
        }

        SyncGrassRendererIfNeeded();
    }

    [ContextMenu("Reset Trail")]
    public void ResetTrail()
    {
        grassPositions.Clear();
        grassSpawnTimes.Clear();
        if (flowerRenderer != null)
            flowerRenderer.ClearFlowers();
        if (inkBloomRenderer != null)
            inkBloomRenderer.ClearBlooms();

        random = new System.Random(randomSeed);
        emittedSampleCount = 0;
        emittedFlowerClusterCount = 0;
        forcedFadeGrassCount = 0;
        initialized = target != null;
        if (!initialized)
        {
            grassDataDirty = true;
            SyncGrassRendererIfNeeded();
            return;
        }

        ApplyGrassFadeSettings();

        lastSamplePosition = target.position;
        lastObservedTargetPosition = target.position;
        Vector3 initialDirection = target.forward;
        initialDirection.y = 0f;
        if (initialDirection.sqrMagnitude < 0.001f)
            initialDirection = Vector3.forward;
        if (!emitOnlyWhenMoving)
            EmitSample(lastSamplePosition, initialDirection.normalized);
        SyncGrassRendererIfNeeded();
    }

    private void EmitSample(Vector3 center, Vector3 forward)
    {
        if (random == null)
            random = new System.Random(randomSeed);

        Vector3 right = new Vector3(forward.z, 0f, -forward.x).normalized;
        int count = Mathf.Max(1, grassPerSample);
        float spawnTime = Application.isPlaying ? Time.time : 0f;
        for (int i = 0; i < count; i++)
        {
            // A triangular distribution places more blades near the path centre
            // while naturally thinning both edges without changing instance count.
            float lateral = (Random01() + Random01() - 1f) * Mathf.Max(0f, trailHalfWidth);
            float longitudinal = RandomRange(-sampleSpacing * 0.45f, sampleSpacing * 0.45f);
            Vector3 candidate = center + right * lateral + forward * longitudinal;
            grassPositions.Add(ProjectToGround(candidate));
            grassSpawnTimes.Add(spawnTime);
        }

        int configuredMaxGrass = Mathf.Max(count, maxGrassInstances);
        int lengthLimitedMaxGrass = maxTrailLength > 0f
            ? Mathf.Max(count, Mathf.CeilToInt(maxTrailLength / Mathf.Max(0.05f, sampleSpacing)) * count)
            : configuredMaxGrass;
        int maxGrass = Mathf.Min(configuredMaxGrass, lengthLimitedMaxGrass);
        ForceOldestGrassToFade(maxGrass, spawnTime);

        grassDataDirty = true;

        emittedSampleCount++;
        int flowerInterval = Mathf.Max(1, flowerEverySamples);
        if (flowerRenderer != null &&
            flowerRenderer.flowerMeshes != null &&
            flowerRenderer.flowerMeshes.Length > 0 &&
            emittedSampleCount % flowerInterval == 0 &&
            Random01() <= flowerChance)
        {
            EmitFlowerCluster(center, forward, right);
        }
    }

    private void EmitFlowerCluster(Vector3 center, Vector3 forward, Vector3 right)
    {
        emittedFlowerClusterCount++;

        int minimum = Mathf.Max(1, Mathf.Min(flowerClusterMin, flowerClusterMax));
        int maximum = Mathf.Max(minimum, Mathf.Max(flowerClusterMin, flowerClusterMax));
        int flowerCount = random.Next(minimum, maximum + 1);
        bool isAccentCluster = accentClusterEvery > 0 &&
                               emittedFlowerClusterCount % accentClusterEvery == 0;
        if (isAccentCluster)
            flowerCount += Mathf.Max(0, accentExtraFlowers);

        float side = emittedFlowerClusterCount % 2 == 0 ? -1f : 1f;
        float spread = Mathf.Max(0f, flowerLateralSpread);
        float clusterLateral = spread > 0f
            ? side * RandomRange(spread * 0.45f, spread * 0.9f)
            : 0f;
        Vector3 clusterCenter = center + right * clusterLateral;
        float radius = Mathf.Max(0f, flowerClusterRadius);
        float minimumScale = Mathf.Min(flowerScaleRange.x, flowerScaleRange.y);
        float maximumScale = Mathf.Max(flowerScaleRange.x, flowerScaleRange.y);
        float accentScale = isAccentCluster ? 1.08f : 1f;

        if (inkBloomRenderer != null)
        {
            Vector3 bloomPosition = ProjectToGround(clusterCenter);
            Quaternion bloomRotation = forward.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(forward, Vector3.up)
                : Quaternion.identity;
            inkBloomRenderer.AddBloom(bloomPosition, bloomRotation, accentScale, isAccentCluster);
        }

        for (int i = 0; i < flowerCount; i++)
        {
            float angle = RandomRange(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(Random01()) * radius;
            Vector3 flowerPosition = clusterCenter +
                                     right * (Mathf.Cos(angle) * distance) +
                                     forward * (Mathf.Sin(angle) * distance);
            flowerPosition = ProjectToGround(flowerPosition);
            Quaternion rotation = Quaternion.Euler(0f, RandomRange(0f, 360f), 0f);
            float scale = RandomRange(minimumScale, maximumScale) * accentScale;
            int meshIndex = random.Next(0, Mathf.Max(1, flowerRenderer.flowerMeshes.Length));
            flowerRenderer.AddFlower(
                flowerPosition,
                rotation,
                scale,
                meshIndex,
                Mathf.Max(0f, flowerBloomDelay));
        }
    }

    private void ForceOldestGrassToFade(int maxActiveGrass, float currentTime)
    {
        int activeGrassCount = grassPositions.Count - forcedFadeGrassCount;
        int forceCount = activeGrassCount - Mathf.Max(1, maxActiveGrass);
        if (forceCount <= 0)
            return;

        int forceEnd = Mathf.Min(grassSpawnTimes.Count, forcedFadeGrassCount + forceCount);
        float fadeStartSpawnTime = currentTime -
                                   Mathf.Max(0.01f, grassGrowDuration) -
                                   Mathf.Max(0f, grassGrowStagger) -
                                   Mathf.Max(0f, grassLifetime);
        for (int i = forcedFadeGrassCount; i < forceEnd; i++)
            grassSpawnTimes[i] = Mathf.Min(grassSpawnTimes[i], fadeStartSpawnTime);

        forcedFadeGrassCount = forceEnd;
    }

    private void ApplyGrassFadeSettings()
    {
        if (grassRenderer == null)
            return;

        grassRenderer.useAgeFade = true;
        grassRenderer.grassGrowDuration = Mathf.Max(0.01f, grassGrowDuration);
        grassRenderer.grassGrowStagger = Mathf.Max(0f, grassGrowStagger);
        grassRenderer.grassLifetime = Mathf.Max(0f, grassLifetime);
        grassRenderer.grassFadeDuration = Mathf.Max(0.01f, grassFadeDuration);
        grassRenderer.grassSinkDepth = Mathf.Max(0f, grassSinkDepth);
    }

    private void PruneExpiredGrass()
    {
        if (!Application.isPlaying || grassSpawnTimes.Count == 0)
            return;

        float totalLifetime = Mathf.Max(0.01f, grassGrowDuration) +
                              Mathf.Max(0f, grassGrowStagger) +
                              Mathf.Max(0f, grassLifetime) +
                              Mathf.Max(0.01f, grassFadeDuration);
        float cutoff = Time.time - totalLifetime;
        int removeCount = 0;
        while (removeCount < grassSpawnTimes.Count && grassSpawnTimes[removeCount] <= cutoff)
            removeCount++;

        if (removeCount == 0)
            return;

        grassPositions.RemoveRange(0, removeCount);
        grassSpawnTimes.RemoveRange(0, removeCount);
        forcedFadeGrassCount = Mathf.Max(0, forcedFadeGrassCount - removeCount);
        grassDataDirty = true;
    }

    private void SyncGrassRendererIfNeeded()
    {
        if (!grassDataDirty || grassRenderer == null)
            return;

        grassRenderer.SetGrassInstances(grassPositions, grassSpawnTimes);
        grassDataDirty = false;
    }

    private Vector3 ProjectToGround(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * Mathf.Max(0.1f, rayStartHeight);
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            Mathf.Max(0.1f, rayDistance),
            groundMask,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.MaxValue;
        RaycastHit nearestHit = new RaycastHit();
        bool foundGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.transform == null || IsTargetHierarchy(hit.transform) || hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestHit = hit;
            foundGround = true;
        }
        if (foundGround)
            return nearestHit.point + nearestHit.normal * groundOffset;

        position.y = target != null ? target.position.y : position.y;
        return position;
    }

    private bool IsTargetHierarchy(Transform candidate)
    {
        if (target == null || candidate == null)
            return false;

        return candidate == target || candidate.IsChildOf(target) || target.IsChildOf(candidate);
    }

    private float Random01()
    {
        return (float)random.NextDouble();
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, Random01());
    }
}
