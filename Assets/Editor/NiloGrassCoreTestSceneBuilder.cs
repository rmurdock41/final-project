using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NiloGrassCoreTestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/NiloGrassCoreTest.unity";
    private const string GroundMaterialPath = "Assets/ThirdParty/NiloCatGrass/NiloGrassTestGround.mat";
    private const string GrassMaterialPath = "Assets/ThirdParty/NiloCatGrass/Core/InstancedIndirectGrass.mat";
    private const string CullingComputePath = "Assets/ThirdParty/NiloCatGrass/Core/CullingCompute.compute";

    static NiloGrassCoreTestSceneBuilder()
    {
        EditorApplication.delayCall += CreateSceneIfMissing;
    }

    [MenuItem("Tools/Okami/Rebuild Nilo Grass Core Test Scene")]
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

        Material grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);
        ComputeShader cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(CullingComputePath);
        if (grassMaterial == null || cullingCompute == null)
        {
            Debug.LogError("Nilo grass core test scene was not created because its material or compute shader is missing.");
            return;
        }

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
            CreateGrassPatch(grassMaterial, cullingCompute);

            EditorSceneManager.SaveScene(testScene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created isolated Nilo grass core test scene: " + ScenePath);
        }
        finally
        {
            if (hasSavedActiveScene && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);

            if (creationMode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(testScene, true);
        }
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 8f, -14f);
        cameraObject.transform.rotation = Quaternion.Euler(22f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.55f, 0.67f, 0.72f, 1f);
        camera.fieldOfView = 50f;
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
        light.color = new Color(1f, 0.96f, 0.84f, 1f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Test Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { name = "Nilo Grass Test Ground" };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(0.33f, 0.27f, 0.18f, 1f));
            else
                material.color = new Color(0.33f, 0.27f, 0.18f, 1f);

            AssetDatabase.CreateAsset(material, GroundMaterialPath);
        }

        ground.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CreateGrassPatch(Material grassMaterial, ComputeShader cullingCompute)
    {
        const int testInstanceCount = 8000;

        GameObject patch = new GameObject("NiloGrass_TestPatch");
        patch.transform.position = Vector3.zero;
        float patchScale = Mathf.Sqrt(testInstanceCount / 4f) * 0.5f;
        patch.transform.localScale = new Vector3(patchScale, 1f, patchScale);

        InstancedIndirectGrassRenderer renderer = patch.AddComponent<InstancedIndirectGrassRenderer>();
        renderer.drawDistance = 35f;
        renderer.instanceMaterial = grassMaterial;
        renderer.cullingComputeShader = cullingCompute;
        renderer.showDebugGUI = false;

        InstancedIndirectGrassPosDefine positions = patch.AddComponent<InstancedIndirectGrassPosDefine>();
        positions.instanceCount = testInstanceCount;
        positions.drawDistance = 35f;
        positions.showDebugGUI = false;
    }
}
