using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OkamiTrailPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/OkamiTrailPrototype.unity";
    private const string GrassSourceMaterialPath = "Assets/ThirdParty/NiloCatGrass/Core/InstancedIndirectGrass.mat";
    private const string TrailGrassMaterialPath = "Assets/ThirdParty/NiloCatGrass/OkamiTrailGrass.mat";
    private const string CullingComputePath = "Assets/ThirdParty/NiloCatGrass/Core/CullingCompute.compute";
    private const string FlowerFbxPath = "Assets/ThirdParty/QuaterniusFlowers/Flowers.fbx";
    private const string FlowerTexturePath = "Assets/ThirdParty/QuaterniusFlowers/Flowers.png";
    private const string FlowerMaterialPath = "Assets/ThirdParty/QuaterniusFlowers/FlowersTrailToon.mat";
    private const string GroundMaterialPath = "Assets/ThirdParty/NiloCatGrass/NiloGrassTestGround.mat";
    private const string MarkerMaterialPath = "Assets/ThirdParty/QuaterniusFlowers/TrailProbe.mat";

    static OkamiTrailPrototypeSceneBuilder()
    {
        EditorApplication.delayCall += CreateSceneIfMissing;
    }

    [MenuItem("Tools/Okami/Rebuild Trail Grass Prototype Scene")]
    public static void RebuildScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            AssetDatabase.DeleteAsset(ScenePath);

        BuildScene();
    }

    private static void CreateSceneIfMissing()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateSceneIfMissing;
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            BuildScene();
    }

    private static void BuildScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        Material grassSourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassSourceMaterialPath);
        ComputeShader cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(CullingComputePath);
        GameObject flowerSource = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerFbxPath);
        Texture2D flowerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerTexturePath);
        Shader flowerShader = Shader.Find("Okami/TrailFlowerToon");

        if (grassSourceMaterial == null || cullingCompute == null || flowerSource == null || flowerTexture == null || flowerShader == null)
        {
            Debug.LogError("Trail prototype scene was not created because a grass or Quaternius flower asset is missing.");
            return;
        }

        Mesh[] flowerMeshes = CollectFlowerMeshes(flowerSource);
        if (flowerMeshes.Length == 0)
        {
            Debug.LogError("Trail prototype scene was not created because Flowers.fbx contains no readable meshes.");
            return;
        }

        Material grassMaterial = CreateOrUpdateTrailGrassMaterial(grassSourceMaterial);
        Material flowerMaterial = CreateOrUpdateFlowerMaterial(flowerShader, flowerTexture);
        Material markerMaterial = CreateOrUpdateMarkerMaterial();

        Scene previousActiveScene = SceneManager.GetActiveScene();
        bool hasSavedActiveScene = previousActiveScene.IsValid() &&
                                   previousActiveScene.isLoaded &&
                                   !string.IsNullOrEmpty(previousActiveScene.path);
        NewSceneMode creationMode = hasSavedActiveScene ? NewSceneMode.Additive : NewSceneMode.Single;
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, creationMode);
        SceneManager.SetActiveScene(testScene);

        try
        {
            CreateCamera();
            CreateLight();
            CreateGround();
            GameObject probe = CreateProbe(markerMaterial);
            CreateTrailSystem(probe.transform, grassMaterial, cullingCompute, flowerMeshes, flowerMaterial);

            EditorSceneManager.SaveScene(testScene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created isolated Okami trail prototype scene: " + ScenePath);
        }
        finally
        {
            if (hasSavedActiveScene && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);

            if (creationMode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(testScene, true);
        }
    }

    private static Mesh[] CollectFlowerMeshes(GameObject flowerSource)
    {
        MeshFilter[] filters = flowerSource.GetComponentsInChildren<MeshFilter>(true);
        List<Mesh> meshes = new List<Mesh>();
        for (int i = 0; i < filters.Length; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            if (mesh != null && !meshes.Contains(mesh))
                meshes.Add(mesh);
        }
        return meshes.ToArray();
    }

    private static Material CreateOrUpdateTrailGrassMaterial(Material sourceMaterial)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailGrassMaterialPath);
        if (material == null)
        {
            material = new Material(sourceMaterial) { name = "Okami Trail Grass" };
            AssetDatabase.CreateAsset(material, TrailGrassMaterialPath);
        }
        else
        {
            material.shader = sourceMaterial.shader;
            material.CopyPropertiesFromMaterial(sourceMaterial);
        }

        material.SetColor("_BaseColor", new Color(0.26f, 0.62f, 0.34f, 1f));
        material.SetColor("_GroundColor", new Color(0.07f, 0.28f, 0.14f, 1f));
        material.SetColor("_TipColor", new Color(0.46f, 0.75f, 0.50f, 1f));
        material.SetColor("_InkColor", new Color(0.018f, 0.028f, 0.012f, 1f));
        material.SetFloat("_InkEdgeWidth", 0.055f);
        material.SetFloat("_InkPixelWidth", 1.15f);
        material.SetFloat("_InkExpandPixels", 0.9f);
        material.SetFloat("_InkCoverage", 0.58f);
        material.SetFloat("_DryBrushStrength", 0.10f);
        material.SetFloat("_GrassWidth", 0.11f);
        material.SetFloat("_GrassHeight", 0.10f);
        material.SetFloat("_WindAFrequency", 1.4f);
        material.SetFloat("_WindAIntensity", 0.09f);
        material.SetFloat("_WindBFrequency", 2.6f);
        material.SetFloat("_WindBIntensity", 0.025f);
        material.SetFloat("_WindCFrequency", 5.2f);
        material.SetFloat("_WindCIntensity", 0.008f);
        material.SetFloat("_WindAIntensity", 0.14f);
        material.SetFloat("_WindBIntensity", 0.045f);
        material.SetFloat("_WindCIntensity", 0.02f);
        material.SetFloat("_UseAgeFade", 1f);
        material.SetFloat("_TrailGrowDuration", 0.7f);
        material.SetFloat("_TrailGrowStagger", 0.35f);
        material.SetFloat("_TrailLifetime", 10f);
        material.SetFloat("_TrailFadeDuration", 2f);
        material.SetFloat("_TrailSinkDepth", 0.12f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateFlowerMaterial(Shader shader, Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FlowerMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "Quaternius Flowers - Trail Toon" };
            AssetDatabase.CreateAsset(material, FlowerMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_ShadeColor", new Color(0.73f, 0.70f, 0.64f, 1f));
        material.SetFloat("_ShadeStep", 0.52f);
        material.SetFloat("_Cutoff", 0.12f);
        material.SetColor("_OutlineColor", new Color(0.028f, 0.021f, 0.015f, 1f));
        material.SetFloat("_OutlineWidth", 0.00021f);
        material.SetFloat("_InnerInkWidth", 2.2f);
        material.SetFloat("_WatercolorStrength", 0.82f);
        material.SetFloat("_EdgeBreakup", 0.07f);
        material.SetFloat("_ColorSteps", 4f);
        material.SetColor("_PaperColor", new Color(0.96f, 0.94f, 0.86f, 1f));
        material.SetFloat("_PigmentDensity", 0.68f);
        material.SetFloat("_Granulation", 0.55f);
        material.SetFloat("_EdgePooling", 0.75f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateMarkerMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (material == null)
        {
            material = new Material(shader) { name = "Trail Probe" };
            AssetDatabase.CreateAsset(material, MarkerMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Color markerColor = new Color(0.92f, 0.45f, 0.12f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", markerColor);
        else
            material.color = markerColor;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 13f, -17f);
        cameraObject.transform.rotation = Quaternion.Euler(34f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.70f, 0.78f, 0.76f, 1f);
        camera.fieldOfView = 48f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        cameraObject.AddComponent<AudioListener>();
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.82f, 1f);
        light.intensity = 1.05f;
        light.shadows = LightShadows.None;
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Trail Test Ground";
        ground.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
        Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (groundMaterial != null)
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    private static GameObject CreateProbe(Material markerMaterial)
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probe.name = "Moving Trail Probe (wolf placeholder)";
        probe.layer = 2; // Ignore Raycast, so ground projection does not hit the marker.
        probe.transform.position = new Vector3(0f, 0.55f, 0f);
        probe.transform.localScale = Vector3.one * 0.7f;
        probe.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;

        OkamiTrailDemoMover mover = probe.AddComponent<OkamiTrailDemoMover>();
        mover.center = new Vector3(0f, 0.55f, 0f);
        mover.radiusX = 7f;
        mover.radiusZ = 5f;
        mover.speed = 0.65f;
        return probe;
    }

    private static void CreateTrailSystem(
        Transform probe,
        Material grassMaterial,
        ComputeShader cullingCompute,
        Mesh[] flowerMeshes,
        Material flowerMaterial)
    {
        GameObject trailSystem = new GameObject("Okami Trail Prototype");

        InstancedIndirectGrassRenderer grassRenderer = trailSystem.AddComponent<InstancedIndirectGrassRenderer>();
        grassRenderer.drawDistance = 45f;
        grassRenderer.instanceMaterial = grassMaterial;
        grassRenderer.cullingComputeShader = cullingCompute;
        grassRenderer.showDebugGUI = false;
        grassRenderer.previewInSceneView = false;

        OkamiTrailFlowerRenderer flowerRenderer = trailSystem.AddComponent<OkamiTrailFlowerRenderer>();
        flowerRenderer.flowerMeshes = flowerMeshes;
        flowerRenderer.flowerMaterial = flowerMaterial;
        flowerRenderer.distanceReference = probe;
        flowerRenderer.visibleDistance = 7.5f;
        flowerRenderer.maxFlowers = 220;
        flowerRenderer.flowerGrowDuration = 0.6f;
        flowerRenderer.flowerLifetime = 5f;
        flowerRenderer.flowerFadeDuration = 1.5f;
        flowerRenderer.flowerSinkDepth = 0.35f;
        flowerRenderer.petalsPerFlower = 4;
        flowerRenderer.sourceModelScale = 145f;

        OkamiTrailInkBloomRenderer inkBloomRenderer = trailSystem.AddComponent<OkamiTrailInkBloomRenderer>();
        inkBloomRenderer.inkColor = new Color(0.08f, 0.10f, 0.055f, 0.46f);
        inkBloomRenderer.lifeColor = new Color(0.35f, 0.55f, 0.18f, 0.26f);
        inkBloomRenderer.bloomDuration = 0.55f;
        inkBloomRenderer.startRadius = 0.18f;
        inkBloomRenderer.endRadius = 1.35f;
        inkBloomRenderer.groundOffset = 0.025f;
        inkBloomRenderer.maxBlooms = 48;

        OkamiTrailGrassEmitter emitter = trailSystem.AddComponent<OkamiTrailGrassEmitter>();
        emitter.target = probe;
        emitter.grassRenderer = grassRenderer;
        emitter.flowerRenderer = flowerRenderer;
        emitter.inkBloomRenderer = inkBloomRenderer;
        emitter.sampleSpacing = 0.38f;
        emitter.trailHalfWidth = 1.35f;
        emitter.grassPerSample = 10;
        emitter.maxGrassInstances = 5000;
        emitter.emitOnlyWhenMoving = true;
        emitter.minimumMoveSpeed = 0.08f;
        emitter.spawnBehindDistance = 0.65f;
        emitter.grassGrowDuration = 0.7f;
        emitter.grassGrowStagger = 0.35f;
        emitter.grassLifetime = 10f;
        emitter.grassFadeDuration = 2f;
        emitter.grassSinkDepth = 0.12f;
        emitter.maxTrailLength = 30f;
        emitter.flowerEverySamples = 3;
        emitter.flowerChance = 0.85f;
        emitter.flowerLateralSpread = 1.05f;
        emitter.flowerClusterMin = 2;
        emitter.flowerClusterMax = 4;
        emitter.flowerClusterRadius = 0.38f;
        emitter.accentClusterEvery = 5;
        emitter.accentExtraFlowers = 2;
        emitter.flowerBloomDelay = 0.15f;
        emitter.flowerScaleRange = new Vector2(0.75f, 1.15f);
        emitter.groundMask = ~(1 << 2);
        emitter.rayStartHeight = 5f;
        emitter.rayDistance = 12f;
        emitter.groundOffset = 0.015f;
        emitter.randomSeed = 5660;
    }
}
