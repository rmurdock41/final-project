using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallAiFoxPlayer
{
    private const string LegacyRootFolder = "Assets/OkamiWolf/AI_Fox";
    private const string RootFolder = "Assets/OkamiWolf/AI_Wolf";
    private const string FbxPath = RootFolder + "/OkamiWolf_Walking.fbx";
    private const string BaseColorPath = RootFolder + "/OkamiWolf_BaseColor.png";
    private const string MetallicPath = RootFolder + "/OkamiWolf_Metallic.png";
    private const string NormalPath = RootFolder + "/OkamiWolf_Normal.png";
    private const string RoughnessPath = RootFolder + "/OkamiWolf_Roughness.png";
    private const string PackedMetallicPath = RootFolder + "/Materials/OkamiWolf_MetallicSmoothness.png";
    private const string MaterialPath = RootFolder + "/Materials/OkamiWolf_Toon.mat";
    private const string IdlePath = RootFolder + "/Animations/OkamiWolf_Idle.anim";
    private const string StableWalkPath = RootFolder + "/Animations/OkamiWolf_WalkStable.anim";
    private const string ControllerPath = RootFolder + "/Animations/OkamiWolf_Locomotion.controller";
    private const string PrefabPath = RootFolder + "/Prefabs/OkamiWolf_Player.prefab";
    private const string InkTemplatePath = "Assets/Materials/Tree/TreeMaterial.mat";
    private const string MainScenePath = "Assets/Scenes/MixAndJAm.unity";
    private const string TrailGrassMaterialPath = "Assets/ThirdParty/NiloCatGrass/OkamiTrailGrass.mat";
    private const string TrailComputePath = "Assets/ThirdParty/NiloCatGrass/Core/CullingCompute.compute";
    private const string TrailFlowerFbxPath = "Assets/ThirdParty/QuaterniusFlowers/Flowers.fbx";
    private const string TrailFlowerMaterialPath = "Assets/ThirdParty/QuaterniusFlowers/FlowersTrailToon.mat";
    private const float PlayerScaleMultiplier = 2.4f;
    private const float TargetModelHeight = 1.45f * PlayerScaleMultiplier;
    private const float InkOutlineWidth = 6f;
    private const float IdleDuration = 3.2f;

    [MenuItem("Tools/Okami/Install AI Wolf Player")]
    public static void Install()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MigrateWolfAssetNames();
        EnsureFolder(RootFolder, "Materials");
        EnsureFolder(RootFolder, "Animations");
        EnsureFolder(RootFolder, "Prefabs");

        ConfigureTexture(BaseColorPath, TextureImporterType.Default, true);
        ConfigureTexture(MetallicPath, TextureImporterType.Default, false);
        ConfigureTexture(RoughnessPath, TextureImporterType.Default, false);
        ConfigureTexture(NormalPath, TextureImporterType.NormalMap, false);
        BuildMetallicSmoothnessTexture();
        ConfigureTexture(PackedMetallicPath, TextureImporterType.Default, false);
        ConfigureModelImporter();

        AnimationClip walkClip = FindWalkClip();
        AssetDatabase.DeleteAsset(StableWalkPath);
        AnimationClip idleClip = CreateNaturalIdle(walkClip);
        Material material = CreateMaterial();
        AnimatorController controller = CreateController(idleClip, walkClip);
        GameObject playerPrefab = CreatePlayerPrefab(material, controller, idleClip, walkClip);
        ReplacePlayerInMainScene(playerPrefab);
        CleanupLegacyGeneratedAssets();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Installed Okami wolf player prefab and replaced Jammo in " + MainScenePath);
    }

    [MenuItem("Tools/Okami/Open Main Scene %#m")]
    public static void OpenMainScene()
    {
        SceneAsset mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
        if (mainScene == null)
        {
            throw new FileNotFoundException("Main scene is missing.", MainScenePath);
        }

        // The preview scene is intentionally temporary, so never let it become the
        // scene Unity restores the next time this project is opened.
        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Okami/Use Original Walk %#9")]
    public static void UseOriginalWalk()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureModelImporter();
        AnimationClip sourceWalk = FindWalkClip();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        BlendTree locomotion = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
            .OfType<BlendTree>()
            .FirstOrDefault();
        if (controller == null || locomotion == null)
        {
            throw new InvalidOperationException("Wolf locomotion controller is missing.");
        }

        ChildMotion[] children = locomotion.children;
        int walkIndex = Array.FindIndex(children, child => child.threshold > 0f);
        if (walkIndex < 0)
        {
            throw new InvalidOperationException("Wolf locomotion controller has no walk slot.");
        }
        children[walkIndex].motion = sourceWalk;
        locomotion.children = children;
        EditorUtility.SetDirty(locomotion);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        if (AssetDatabase.GetAssetPath(locomotion.children[walkIndex].motion) != FbxPath)
        {
            throw new InvalidOperationException("Animator failed to reference the original FBX walk clip.");
        }

        AssetDatabase.DeleteAsset(StableWalkPath);
        AssetDatabase.SaveAssets();
        Debug.Log("OKAMI_WOLF_ORIGINAL_WALK_OK " + FbxPath + " / " + sourceWalk.name);
    }

    private static void MigrateWolfAssetNames()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder) && AssetDatabase.IsValidFolder(LegacyRootFolder))
        {
            string error = AssetDatabase.MoveAsset(LegacyRootFolder, RootFolder);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        if (!AssetDatabase.IsValidFolder(RootFolder))
        {
            throw new InvalidOperationException("Wolf asset folder is missing: " + RootFolder);
        }

        MoveAssetIfNeeded(RootFolder + "/OkamiFox_Walking.fbx", FbxPath);
        MoveAssetIfNeeded(RootFolder + "/OkamiFox_BaseColor.png", BaseColorPath);
        MoveAssetIfNeeded(RootFolder + "/OkamiFox_Metallic.png", MetallicPath);
        MoveAssetIfNeeded(RootFolder + "/OkamiFox_Normal.png", NormalPath);
        MoveAssetIfNeeded(RootFolder + "/OkamiFox_Roughness.png", RoughnessPath);
        MoveAssetIfNeeded(RootFolder + "/Materials/OkamiFox_MetallicSmoothness.png", PackedMetallicPath);
    }

    private static void MoveAssetIfNeeded(string oldPath, string newPath)
    {
        if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null || AssetDatabase.LoadMainAssetAtPath(newPath) != null) return;
        string error = AssetDatabase.MoveAsset(oldPath, newPath);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }

    public static void RunBatch()
    {
        try
        {
            Install();
            ValidateIntegration();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void RenderPreviewBatch()
    {
        try
        {
            Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject player = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
            if (player == null) throw new InvalidOperationException("Unable to instantiate wolf preview.");
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.position = new Vector3(0f, -0.03f, 0f);
            floor.transform.localScale = new Vector3(14f, 0.06f, 14f);
            Material floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.SetColor("_BaseColor", new Color(0.36f, 0.38f, 0.40f, 1f));
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            GameObject lightObject = new GameObject("Preview Light");
            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f, 1f);

            GameObject cameraObject = new GameObject("Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 1.05f * PlayerScaleMultiplier;
            camera.nearClipPlane = 0.05f;
            camera.allowHDR = false;
            camera.transform.position = new Vector3(8f, 0.72f * PlayerScaleMultiplier, 0.15f * PlayerScaleMultiplier);
            camera.transform.LookAt(new Vector3(0f, 0.72f * PlayerScaleMultiplier, 0.15f * PlayerScaleMultiplier));

            string outputFolder = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../ArtSource/OkamiWolf/AI_Wolf_Review"
            ));
            Directory.CreateDirectory(outputFolder);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            WolfVisualGrounding grounding = player.GetComponent<WolfVisualGrounding>();
            RenderWolfFrame(camera, animator, grounding, 0f, 0.20f, Path.Combine(outputFolder, "Idle.png"));
            RenderWolfFrame(camera, animator, grounding, 1f, 0.00f, Path.Combine(outputFolder, "Walk_00.png"));
            RenderWolfFrame(camera, animator, grounding, 1f, 0.25f, Path.Combine(outputFolder, "Walk_25.png"));
            RenderWolfFrame(camera, animator, grounding, 1f, 0.50f, Path.Combine(outputFolder, "Walk_50.png"));
            RenderWolfFrame(camera, animator, grounding, 1f, 0.75f, Path.Combine(outputFolder, "Walk_75.png"));
            Debug.Log("OKAMI_WOLF_PREVIEW_OK " + outputFolder);
            OpenMainScene();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try
            {
                OpenMainScene();
            }
            catch (Exception restoreException)
            {
                Debug.LogException(restoreException);
            }
            EditorApplication.Exit(1);
        }
    }

    private static void RenderWolfFrame(
        Camera camera,
        Animator animator,
        WolfVisualGrounding grounding,
        float blend,
        float normalizedTime,
        string outputPath)
    {
        animator.Rebind();
        animator.SetFloat("Blend", blend);
        animator.Play("Locomotion", 0, normalizedTime);
        animator.Update(0f);
        grounding.ApplyGrounding();

        RenderTexture renderTexture = new RenderTexture(768, 512, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        camera.targetTexture = renderTexture;
        camera.Render();
        RenderTexture.active = renderTexture;
        Texture2D image = new Texture2D(768, 512, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 768, 512), 0, 0);
        image.Apply();
        File.WriteAllBytes(outputPath, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(renderTexture);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ConfigureTexture(string path, TextureImporterType type, bool sRgb)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Texture importer not found: " + path);
        }

        importer.textureType = type;
        importer.sRGBTexture = sRgb;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    private static void ConfigureModelImporter()
    {
        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("Model importer not found: " + FbxPath);
        }

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.globalScale = 100f;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.importMaterials = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.clipAnimations;
        }
        foreach (ModelImporterClipAnimation clip in clips)
        {
            clip.loopTime = true;
            clip.loopPose = false;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clip.lockRootRotation = false;
            clip.lockRootHeightY = false;
            clip.lockRootPositionXZ = false;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void BuildMetallicSmoothnessTexture()
    {
        Texture2D metallic = LoadPng(MetallicPath, true);
        Texture2D roughness = LoadPng(RoughnessPath, true);
        if (metallic.width != roughness.width || metallic.height != roughness.height)
        {
            throw new InvalidOperationException("Metallic and roughness texture dimensions differ.");
        }

        Color32[] metallicPixels = metallic.GetPixels32();
        Color32[] roughnessPixels = roughness.GetPixels32();
        Color32[] packedPixels = new Color32[metallicPixels.Length];
        for (int index = 0; index < packedPixels.Length; index++)
        {
            byte metal = metallicPixels[index].r;
            byte smoothness = (byte)(255 - roughnessPixels[index].r);
            packedPixels[index] = new Color32(metal, metal, metal, smoothness);
        }

        Texture2D packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
        packed.SetPixels32(packedPixels);
        packed.Apply(false, false);
        string absolutePath = AssetPathToAbsolute(PackedMetallicPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, packed.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(metallic);
        UnityEngine.Object.DestroyImmediate(roughness);
        UnityEngine.Object.DestroyImmediate(packed);
        AssetDatabase.ImportAsset(PackedMetallicPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    private static Texture2D LoadPng(string assetPath, bool linear)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
        if (!texture.LoadImage(File.ReadAllBytes(AssetPathToAbsolute(assetPath)), false))
        {
            throw new InvalidOperationException("Unable to decode texture: " + assetPath);
        }
        return texture;
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }

    private static AnimationClip FindWalkClip()
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip == null)
        {
            throw new InvalidOperationException("No animation clip found in " + FbxPath);
        }
        return clip;
    }

    private struct LocalPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    private static AnimationClip CreateNaturalIdle(AnimationClip walkClip)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        GameObject sample = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        if (sample == null)
        {
            throw new InvalidOperationException("Unable to instantiate the wolf while building its idle animation.");
        }

        HashSet<string> animatedPaths = new HashSet<string>(
            AnimationUtility.GetCurveBindings(walkClip)
                .Where(binding => binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path))
                .Select(binding => binding.path)
        );
        Transform[] transforms = sample.GetComponentsInChildren<Transform>(true)
            .Where(transform =>
                transform != sample.transform &&
                animatedPaths.Contains(AnimationUtility.CalculateTransformPath(transform, sample.transform)))
            .ToArray();
        // Use one complete source pose. Averaging two opposite walk poses makes the
        // legs over-extend and changes the model's vertical baseline dramatically.
        walkClip.SampleAnimation(sample, 0f);
        Dictionary<Transform, LocalPose> neutralPose = transforms.ToDictionary(
            bone => bone,
            CapturePose
        );

        AssetDatabase.DeleteAsset(IdlePath);
        AnimationClip idle = new AnimationClip
        {
            name = "OkamiWolf_Idle",
            frameRate = walkClip.frameRate,
            wrapMode = WrapMode.Loop
        };

        const int keyCount = 9;
        foreach (Transform bone in transforms)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, sample.transform);
            LocalPose pose = neutralPose[bone];
            Keyframe[] px = new Keyframe[keyCount];
            Keyframe[] py = new Keyframe[keyCount];
            Keyframe[] pz = new Keyframe[keyCount];
            Keyframe[] rx = new Keyframe[keyCount];
            Keyframe[] ry = new Keyframe[keyCount];
            Keyframe[] rz = new Keyframe[keyCount];
            Keyframe[] rw = new Keyframe[keyCount];

            for (int index = 0; index < keyCount; index++)
            {
                float time = IdleDuration * index / (keyCount - 1f);
                float phase = Mathf.PI * 2f * time / IdleDuration;
                Quaternion rotation = pose.rotation * GetIdleRotationOffset(bone.name, phase);

                px[index] = new Keyframe(time, pose.position.x);
                py[index] = new Keyframe(time, pose.position.y);
                pz[index] = new Keyframe(time, pose.position.z);
                rx[index] = new Keyframe(time, rotation.x);
                ry[index] = new Keyframe(time, rotation.y);
                rz[index] = new Keyframe(time, rotation.z);
                rw[index] = new Keyframe(time, rotation.w);
            }

            SetTransformCurve(idle, path, "m_LocalPosition.x", px);
            SetTransformCurve(idle, path, "m_LocalPosition.y", py);
            SetTransformCurve(idle, path, "m_LocalPosition.z", pz);
            SetTransformCurve(idle, path, "m_LocalRotation.x", rx);
            SetTransformCurve(idle, path, "m_LocalRotation.y", ry);
            SetTransformCurve(idle, path, "m_LocalRotation.z", rz);
            SetTransformCurve(idle, path, "m_LocalRotation.w", rw);
        }

        idle.EnsureQuaternionContinuity();
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(idle);
        settings.loopTime = true;
        settings.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings);
        AssetDatabase.CreateAsset(idle, IdlePath);
        UnityEngine.Object.DestroyImmediate(sample);
        return idle;
    }

    private static LocalPose CapturePose(Transform transform)
    {
        return new LocalPose
        {
            position = transform.localPosition,
            rotation = transform.localRotation,
            scale = transform.localScale
        };
    }

    private static Quaternion GetIdleRotationOffset(string boneName, float phase)
    {
        string lowerName = boneName.ToLowerInvariant();
        float breath = Mathf.Sin(phase);
        if (lowerName == "chest")
        {
            return Quaternion.Euler(0.7f * breath, 0f, 0.25f * Mathf.Sin(phase * 2f));
        }
        if (lowerName == "head")
        {
            return Quaternion.Euler(0.8f * breath, 1.8f * Mathf.Sin(phase * 0.5f), 0.45f * Mathf.Sin(phase * 2f));
        }
        if (lowerName.Contains("ear"))
        {
            float side = lowerName.StartsWith("r_") ? -1f : 1f;
            return Quaternion.Euler(0.6f * Mathf.Sin(phase * 2f), 0f, side * 1.2f * Mathf.Sin(phase));
        }
        if (lowerName.StartsWith("tail"))
        {
            int tailIndex = lowerName == "tail" ? 0 : lowerName == "tailstart" ? 1 :
                lowerName.EndsWith("1") ? 2 : lowerName.EndsWith("2") ? 3 : 4;
            float sway = Mathf.Sin(phase + tailIndex * 0.28f) * (2.5f + tailIndex * 0.65f);
            return Quaternion.Euler(0.35f * breath, sway * 0.35f, sway);
        }
        return Quaternion.identity;
    }

    private static void SetTransformCurve(AnimationClip clip, string path, string propertyName, Keyframe[] keys)
    {
        AnimationCurve curve = new AnimationCurve(keys);
        for (int index = 0; index < keys.Length; index++) curve.SmoothTangents(index, 0f);
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName),
            curve
        );
    }

    private static Material CreateMaterial()
    {
        AssetDatabase.DeleteAsset(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Toon");
        if (shader == null)
        {
            throw new InvalidOperationException("The project's Universal Toon shader is unavailable.");
        }

        Material template = AssetDatabase.LoadAssetAtPath<Material>(InkTemplatePath);
        Material material = template != null ? new Material(template) : new Material(shader);
        material.name = "OkamiWolf_Toon";
        material.shader = shader;
        material.enableInstancing = false;
        Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", baseColor);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseColor);
        if (material.HasProperty("_1st_ShadeMap")) material.SetTexture("_1st_ShadeMap", null);
        if (material.HasProperty("_2nd_ShadeMap")) material.SetTexture("_2nd_ShadeMap", null);
        if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
        if (material.HasProperty("_NormalMap")) material.SetTexture("_NormalMap", null);
        if (material.HasProperty("_Is_NormalMapToBase")) material.SetFloat("_Is_NormalMapToBase", 0f);
        if (material.HasProperty("_Outline_Width")) material.SetFloat("_Outline_Width", InkOutlineWidth);
        if (material.HasProperty("_Outline_Color")) material.SetColor("_Outline_Color", Color.black);
        if (material.HasProperty("_1st_ShadeColor")) material.SetColor("_1st_ShadeColor", Color.white);
        if (material.HasProperty("_2nd_ShadeColor")) material.SetColor("_2nd_ShadeColor", Color.white);
        if (material.HasProperty("_BaseColor_Step")) material.SetFloat("_BaseColor_Step", 0.5f);
        if (material.HasProperty("_BaseShade_Feather")) material.SetFloat("_BaseShade_Feather", 0.0001f);
        if (material.HasProperty("_ShadeColor_Step")) material.SetFloat("_ShadeColor_Step", 0f);
        if (material.HasProperty("_1st2nd_Shades_Feather")) material.SetFloat("_1st2nd_Shades_Feather", 0.0001f);
        if (material.HasProperty("_1st_ShadeColor_Step")) material.SetFloat("_1st_ShadeColor_Step", 0.5f);
        if (material.HasProperty("_1st_ShadeColor_Feather")) material.SetFloat("_1st_ShadeColor_Feather", 0.0001f);
        if (material.HasProperty("_2nd_ShadeColor_Step")) material.SetFloat("_2nd_ShadeColor_Step", 0f);
        if (material.HasProperty("_2nd_ShadeColor_Feather")) material.SetFloat("_2nd_ShadeColor_Feather", 0.0001f);
        if (material.HasProperty("_Unlit_Intensity")) material.SetFloat("_Unlit_Intensity", 1f);
        if (material.HasProperty("_Tweak_SystemShadowsLevel")) material.SetFloat("_Tweak_SystemShadowsLevel", 0f);
        material.SetShaderPassEnabled("SRPDefaultUnlit", true);
        if (material.HasProperty("_SPRDefaultUnlitColorMask")) material.SetInt("_SPRDefaultUnlitColorMask", 15);
        if (material.HasProperty("_SRPDefaultUnlitColMode")) material.SetInt("_SRPDefaultUnlitColMode", 1);
        material.DisableKeyword("_NORMALMAP");
        material.EnableKeyword("_OUTLINE_NML");
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static AnimatorController CreateController(AnimationClip idle, AnimationClip walk)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Blend", AnimatorControllerParameterType.Float);

        BlendTree tree = new BlendTree
        {
            name = "OkamiWolf_Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Blend",
            useAutomaticThresholds = false,
            minThreshold = 0f,
            maxThreshold = 1f
        };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.1f);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.AddState("Locomotion");
        state.motion = tree;
        state.writeDefaultValues = true;
        stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject CreatePlayerPrefab(
        Material material,
        RuntimeAnimatorController controller,
        AnimationClip idleClip,
        AnimationClip walkClip)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException("Unable to load model asset: " + FbxPath);
        }

        GameObject root = new GameObject("OkamiWolf_Player");
        GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
        if (visual == null)
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw new InvalidOperationException("Unable to instantiate model asset.");
        }
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, true);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        idleClip.SampleAnimation(visual, 0f);

        Transform hips = FindChild(visual.transform, "Hips");
        Transform head = FindChild(visual.transform, "Head");
        if (hips != null && head != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.position - hips.position, Vector3.up);
            if (forward.sqrMagnitude > 0.000001f)
            {
                visual.transform.rotation = Quaternion.FromToRotation(forward.normalized, Vector3.forward) * visual.transform.rotation;
            }
        }

        Bounds bounds = CalculateBounds(visual);
        if (bounds.size.y <= 0.000001f)
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw new InvalidOperationException("Imported model has invalid bounds.");
        }
        float scale = TargetModelHeight / bounds.size.y;
        visual.transform.localScale *= scale;
        bounds = CalculateBounds(visual);
        float modelBottomY = CalculateMeshBottomY(visual);
        visual.transform.position += Vector3.up * (root.transform.position.y - modelBottomY);
        bounds = CalculateBounds(visual);

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++) materials[index] = material;
            renderer.sharedMaterials = materials;
        }

        Animator animator = visual.GetComponentInChildren<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController characterController = root.AddComponent<CharacterController>();
        characterController.radius = Mathf.Clamp(
            bounds.size.x * 0.48f,
            0.20f * PlayerScaleMultiplier,
            0.34f * PlayerScaleMultiplier
        );
        characterController.height = Mathf.Max(
            characterController.radius * 2f,
            Mathf.Clamp(
                bounds.size.y * 0.78f,
                0.85f * PlayerScaleMultiplier,
                1.15f * PlayerScaleMultiplier
            )
        );
        Transform chest = FindChild(visual.transform, "chest");
        Vector3 bodyCenter = hips != null && chest != null
            ? root.transform.InverseTransformPoint(Vector3.Lerp(hips.position, chest.position, 0.55f))
            : root.transform.InverseTransformPoint(bounds.center);
        characterController.center = new Vector3(
            bodyCenter.x,
            characterController.height * 0.5f,
            bodyCenter.z
        );
        characterController.slopeLimit = 45f;
        characterController.stepOffset = Mathf.Min(0.22f * PlayerScaleMultiplier, characterController.height * 0.2f);
        characterController.skinWidth = Mathf.Min(0.04f * PlayerScaleMultiplier, characterController.radius * 0.12f);

        MovementInput movement = root.AddComponent<MovementInput>();
        movement.Velocity = 5f;
        movement.desiredRotationSpeed = 0.1f;
        movement.allowPlayerRotation = 0.1f;
        movement.anim = animator;

        AnimationCurve walkGroundingCurve = CreateStableGroundingCurve();
        WolfVisualGrounding grounding = root.AddComponent<WolfVisualGrounding>();
        grounding.Configure(visual.transform, animator, walkGroundingCurve);
        grounding.maxVerticalCorrection = 0f;
        grounding.idleGroundBias = 0f;
        grounding.walkGroundBias = 0f;
        grounding.cameraTargetOffset = new Vector3(
            0f,
            0.72f * PlayerScaleMultiplier,
            0.18f * PlayerScaleMultiplier
        );

        GameObject cameraTargetObject = new GameObject("CameraTarget");
        cameraTargetObject.transform.SetParent(root.transform, false);
        SmoothCameraTarget cameraTarget = cameraTargetObject.AddComponent<SmoothCameraTarget>();
        cameraTarget.source = root.transform;
        cameraTarget.localOffset = grounding.cameraTargetOffset;
        cameraTarget.verticalSmoothTime = 0.18f;
        cameraTarget.verticalDeadZone = 0.025f;
        cameraTarget.SnapToSource();

        CreateWalkingTrail(root.transform);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
        {
            throw new InvalidOperationException("Failed to save player prefab: " + PrefabPath);
        }
        return prefab;
    }

    private static void CreateWalkingTrail(Transform playerRoot)
    {
        Material grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrailGrassMaterialPath);
        ComputeShader cullingCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(TrailComputePath);
        GameObject flowerSource = AssetDatabase.LoadAssetAtPath<GameObject>(TrailFlowerFbxPath);
        Material flowerMaterial = AssetDatabase.LoadAssetAtPath<Material>(TrailFlowerMaterialPath);
        if (grassMaterial == null || cullingCompute == null || flowerSource == null || flowerMaterial == null)
            throw new InvalidOperationException("Walking trail assets are missing.");

        Mesh[] flowerMeshes = flowerSource
            .GetComponentsInChildren<MeshFilter>(true)
            .Select(filter => filter.sharedMesh)
            .Where(mesh => mesh != null)
            .Distinct()
            .ToArray();
        if (flowerMeshes.Length == 0)
            throw new InvalidOperationException("Walking trail flower source contains no meshes.");

        GameObject trailObject = new GameObject("Okami Walking Trail");
        trailObject.transform.SetParent(playerRoot, false);

        InstancedIndirectGrassRenderer grassRenderer = trailObject.AddComponent<InstancedIndirectGrassRenderer>();
        grassRenderer.drawDistance = 45f;
        grassRenderer.instanceMaterial = grassMaterial;
        grassRenderer.previewInSceneView = false;
        grassRenderer.useAgeFade = true;
        grassRenderer.grassGrowDuration = 0.7f;
        grassRenderer.grassGrowStagger = 0.35f;
        grassRenderer.grassLifetime = 10f;
        grassRenderer.grassFadeDuration = 2f;
        grassRenderer.grassSinkDepth = 0.12f;
        grassRenderer.cullingComputeShader = cullingCompute;

        OkamiTrailFlowerRenderer flowerRenderer = trailObject.AddComponent<OkamiTrailFlowerRenderer>();
        flowerRenderer.flowerMeshes = flowerMeshes;
        flowerRenderer.flowerMaterial = flowerMaterial;
        flowerRenderer.distanceReference = playerRoot;
        flowerRenderer.visibleDistance = 7.5f;
        flowerRenderer.maxFlowers = 180;
        flowerRenderer.flowerGrowDuration = 0.6f;
        flowerRenderer.flowerLifetime = 5f;
        flowerRenderer.flowerFadeDuration = 1.5f;
        flowerRenderer.flowerSinkDepth = 0.35f;
        flowerRenderer.petalsPerFlower = 4;
        flowerRenderer.sourceModelScale = 145f;

        OkamiTrailInkBloomRenderer inkBloomRenderer = trailObject.AddComponent<OkamiTrailInkBloomRenderer>();
        inkBloomRenderer.inkColor = new Color(0.08f, 0.10f, 0.055f, 0.46f);
        inkBloomRenderer.lifeColor = new Color(0.35f, 0.55f, 0.18f, 0.26f);
        inkBloomRenderer.bloomDuration = 0.55f;
        inkBloomRenderer.startRadius = 0.18f;
        inkBloomRenderer.endRadius = 1.35f;
        inkBloomRenderer.groundOffset = 0.025f;
        inkBloomRenderer.maxBlooms = 48;

        OkamiTrailGrassEmitter emitter = trailObject.AddComponent<OkamiTrailGrassEmitter>();
        emitter.target = playerRoot;
        emitter.grassRenderer = grassRenderer;
        emitter.flowerRenderer = flowerRenderer;
        emitter.inkBloomRenderer = inkBloomRenderer;
        emitter.sampleSpacing = 0.34f;
        emitter.trailHalfWidth = 1.15f;
        emitter.grassPerSample = 8;
        emitter.maxGrassInstances = 4000;
        emitter.emitOnlyWhenMoving = true;
        emitter.minimumMoveSpeed = 0.08f;
        emitter.spawnBehindDistance = 0.65f;
        emitter.grassGrowDuration = 0.7f;
        emitter.grassGrowStagger = 0.35f;
        emitter.grassLifetime = 10f;
        emitter.grassFadeDuration = 2f;
        emitter.grassSinkDepth = 0.12f;
        emitter.maxTrailLength = 30f;
        emitter.flowerEverySamples = 4;
        emitter.flowerChance = 0.75f;
        emitter.flowerLateralSpread = 0.95f;
        emitter.flowerClusterMin = 2;
        emitter.flowerClusterMax = 4;
        emitter.flowerClusterRadius = 0.38f;
        emitter.accentClusterEvery = 5;
        emitter.accentExtraFlowers = 2;
        emitter.flowerBloomDelay = 0.15f;
        emitter.flowerScaleRange = new Vector2(0.75f, 1.1f);
        emitter.groundMask = ~(1 << 2);
        emitter.rayStartHeight = 5f;
        emitter.rayDistance = 12f;
        emitter.groundOffset = 0.015f;
        emitter.randomSeed = 5660;
    }

    private static void ReplacePlayerInMainScene(GameObject playerPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject oldPlayer = FindPlayerRoot(scene);
        if (oldPlayer == null)
        {
            throw new InvalidOperationException("Jammo_Player was not found in " + MainScenePath);
        }

        Vector3 position = oldPlayer.transform.position;
        Quaternion rotation = oldPlayer.transform.rotation;
        int siblingIndex = oldPlayer.transform.GetSiblingIndex();
        CinemachineVirtualCameraBase thirdPersonCamera = Resources
            .FindObjectsOfTypeAll<CinemachineVirtualCameraBase>()
            .FirstOrDefault(camera =>
                camera.gameObject.scene == scene &&
                camera.gameObject.name == "ThirdPersonCamera");

        UnityEngine.Object.DestroyImmediate(oldPlayer);
        GameObject newPlayer = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
        if (newPlayer == null)
        {
            throw new InvalidOperationException("Unable to instantiate the new player prefab.");
        }
        newPlayer.name = "OkamiWolf_Player";
        newPlayer.transform.position = position;
        newPlayer.transform.rotation = rotation;
        newPlayer.transform.localScale = Vector3.one;
        newPlayer.transform.SetSiblingIndex(siblingIndex);

        if (thirdPersonCamera == null)
        {
            throw new InvalidOperationException("ThirdPersonCamera was not found in " + MainScenePath);
        }
        Transform cameraTarget = newPlayer.transform.Find("CameraTarget");
        if (cameraTarget == null)
        {
            throw new InvalidOperationException("The new wolf player has no CameraTarget.");
        }
        thirdPersonCamera.Follow = cameraTarget;
        thirdPersonCamera.LookAt = cameraTarget;
        foreach (CinemachineVirtualCameraBase camera in Resources.FindObjectsOfTypeAll<CinemachineVirtualCameraBase>())
        {
            if (camera == thirdPersonCamera || camera.gameObject.scene != scene) continue;
            if (camera.transform.IsChildOf(thirdPersonCamera.transform))
            {
                camera.Follow = null;
                camera.LookAt = null;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Failed to save scene: " + MainScenePath);
        }
    }

    private static bool ReferencesPlayer(Transform target, Transform player)
    {
        return target != null && (target == player || target.IsChildOf(player));
    }

    private static void CleanupLegacyGeneratedAssets()
    {
        string[] legacyPaths =
        {
            RootFolder + "/Materials/OkamiFox_URP.mat",
            RootFolder + "/Animations/OkamiFox_Idle.anim",
            RootFolder + "/Animations/OkamiFox_Locomotion.controller",
            RootFolder + "/Prefabs/OkamiFox_Player.prefab"
        };
        foreach (string path in legacyPaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        }
    }

    private static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
        return bounds;
    }

    private static float CalculateMeshBottomY(GameObject root)
    {
        float bottomY = float.PositiveInfinity;
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh bakedMesh = new Mesh();
            renderer.BakeMesh(bakedMesh);
            foreach (Vector3 vertex in bakedMesh.vertices)
            {
                bottomY = Mathf.Min(bottomY, renderer.transform.TransformPoint(vertex).y);
            }
            UnityEngine.Object.DestroyImmediate(bakedMesh);
        }
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                bottomY = Mathf.Min(bottomY, filter.transform.TransformPoint(vertex).y);
            }
        }
        if (float.IsPositiveInfinity(bottomY))
        {
            throw new InvalidOperationException("Unable to calculate the wolf mesh bottom.");
        }
        return bottomY;
    }

    private static AnimationCurve CreateStableGroundingCurve()
    {
        AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        curve.preWrapMode = WrapMode.Loop;
        curve.postWrapMode = WrapMode.Loop;
        return curve;
    }

    private static void ValidateIntegration()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameObject player = FindPlayerRoot(scene);
        if (player == null) throw new InvalidOperationException("Validated scene has no Okami wolf player root.");

        MovementInput movement = player.GetComponent<MovementInput>();
        CharacterController characterController = player.GetComponent<CharacterController>();
        WolfVisualGrounding grounding = player.GetComponent<WolfVisualGrounding>();
        SmoothCameraTarget cameraTarget = player.GetComponentInChildren<SmoothCameraTarget>(true);
        Animator animator = player.GetComponentInChildren<Animator>(true);
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        OkamiTrailGrassEmitter trailEmitter = player.GetComponentInChildren<OkamiTrailGrassEmitter>(true);
        OkamiTrailFlowerRenderer trailFlowers = player.GetComponentInChildren<OkamiTrailFlowerRenderer>(true);
        OkamiTrailInkBloomRenderer trailInkBloom = player.GetComponentInChildren<OkamiTrailInkBloomRenderer>(true);
        InstancedIndirectGrassRenderer trailGrass = player.GetComponentInChildren<InstancedIndirectGrassRenderer>(true);
        if (movement == null || characterController == null || grounding == null || cameraTarget == null ||
            animator == null || renderers.Length == 0 || trailEmitter == null || trailFlowers == null ||
            trailInkBloom == null || trailGrass == null)
        {
            throw new InvalidOperationException("New player is missing movement, collision, grounding, rendering, or walking trail components.");
        }
        if (trailEmitter.target != player.transform || !trailEmitter.emitOnlyWhenMoving ||
            trailEmitter.spawnBehindDistance <= 0f || trailEmitter.inkBloomRenderer != trailInkBloom ||
            trailFlowers.distanceReference != player.transform)
            throw new InvalidOperationException("Walking trail is not bound to the wolf movement root.");
        if (animator.runtimeAnimatorController == null || animator.runtimeAnimatorController.name != "OkamiWolf_Locomotion")
        {
            throw new InvalidOperationException("New player is not using the Okami wolf locomotion controller.");
        }
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        if (idleClip == null || AnimationUtility.GetCurveBindings(idleClip).Any(binding =>
                string.IsNullOrEmpty(binding.path) || binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Wolf idle animation must not animate the model root or any transform scale.");
        }
        Vector3 authoredScale = animator.transform.localScale;
        Vector3 authoredPosition = animator.transform.localPosition;
        animator.Rebind();
        animator.SetFloat("Blend", 0f);
        animator.Update(0.1f);
        Vector3 idleScale = animator.transform.localScale;
        Vector3 idlePosition = animator.transform.localPosition;
        animator.SetFloat("Blend", 1f);
        animator.Update(0.1f);
        Vector3 walkScale = animator.transform.localScale;
        Vector3 walkPosition = animator.transform.localPosition;
        if ((idleScale - authoredScale).sqrMagnitude > 0.000001f ||
            (walkScale - authoredScale).sqrMagnitude > 0.000001f ||
            (idlePosition - authoredPosition).sqrMagnitude > 0.000001f ||
            (walkPosition - authoredPosition).sqrMagnitude > 0.000001f)
        {
            throw new InvalidOperationException(
                "Idle/Walk transition changes the wolf model root transform. " +
                "Authored scale=" + authoredScale + ", idle=" + idleScale + ", walk=" + walkScale
            );
        }
        float walkGroundingCorrection = grounding.ApplyGrounding();

        Bounds bounds = CalculateBounds(player);
        if (bounds.size.y < 1.2f * PlayerScaleMultiplier ||
            bounds.size.y > 1.8f * PlayerScaleMultiplier)
        {
            throw new InvalidOperationException("New player height is outside the expected range: " + bounds.size.y);
        }
        foreach (Renderer renderer in renderers)
        {
            if (renderer.sharedMaterials.Any(material => material == null || material.shader == null))
            {
                throw new InvalidOperationException("New player has a missing material or shader.");
            }
            if (renderer.sharedMaterials.Any(material => material.shader.name != "Universal Render Pipeline/Toon"))
            {
                throw new InvalidOperationException("New player is not using the project's Universal Toon shader.");
            }
        }
        Material inkMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (inkMaterial == null ||
            !inkMaterial.GetShaderPassEnabled("SRPDefaultUnlit") ||
            inkMaterial.IsKeywordEnabled("_NORMALMAP") ||
            inkMaterial.GetFloat("_Is_NormalMapToBase") > 0.001f ||
            inkMaterial.GetFloat("_Unlit_Intensity") < 0.99f ||
            inkMaterial.GetFloat("_Outline_Width") < InkOutlineWidth - 0.01f ||
            inkMaterial.GetFloat("_BaseShade_Feather") > 0.001f ||
            inkMaterial.GetFloat("_1st2nd_Shades_Feather") > 0.001f ||
            inkMaterial.GetTexture("_1st_ShadeMap") != null ||
            inkMaterial.GetTexture("_2nd_ShadeMap") != null)
        {
            throw new InvalidOperationException("Wolf material is not using the flat ink toon settings.");
        }

        float walkBottomY = CalculateMeshBottomY(player);
        if (Mathf.Abs(walkBottomY - player.transform.position.y) > 0.20f * PlayerScaleMultiplier)
        {
            throw new InvalidOperationException("Wolf walk mesh is not aligned to the ground plane: " + walkBottomY);
        }
        animator.SetFloat("Blend", 0f);
        animator.Update(0.1f);
        float idleGroundingCorrection = grounding.ApplyGrounding();
        float idleBottomY = CalculateMeshBottomY(player);
        float idleBottomOffset = idleBottomY - player.transform.position.y;
        if (Mathf.Abs(idleBottomOffset) > 0.035f * PlayerScaleMultiplier)
        {
            throw new InvalidOperationException("Wolf idle mesh is not aligned to the ground plane: " + idleBottomY);
        }
        float controllerBottom = characterController.center.y - characterController.height * 0.5f;
        if (Mathf.Abs(controllerBottom) > 0.01f)
        {
            throw new InvalidOperationException("CharacterController bottom is not aligned to the paw ground plane.");
        }
        foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
            {
                throw new InvalidOperationException("New player contains a missing script on " + child.name);
            }
        }

        CinemachineVirtualCameraBase thirdPersonCamera = Resources
            .FindObjectsOfTypeAll<CinemachineVirtualCameraBase>()
            .FirstOrDefault(camera => camera.gameObject.scene == scene && camera.gameObject.name == "ThirdPersonCamera");
        int followingCameras = thirdPersonCamera != null && thirdPersonCamera.Follow == cameraTarget.transform ? 1 : 0;
        if (followingCameras == 0 || thirdPersonCamera.LookAt != cameraTarget.transform)
        {
            throw new InvalidOperationException("ThirdPersonCamera does not use the wolf's smoothed CameraTarget.");
        }

        ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Generic ||
            !importer.clipAnimations.Any(clip => clip.loopTime))
        {
            throw new InvalidOperationException("FBX importer is not configured as a looping Generic animation.");
        }

        Debug.LogFormat(
            "OKAMI_WOLF_VALIDATION_OK height={0:F3} controllerHeight={1:F3} walkBottomY={2:F3} idleBottomY={3:F3} walkGrounding={4:F3} idleGrounding={5:F3} followingCameras={6}",
            bounds.size.y,
            characterController.height,
            walkBottomY - player.transform.position.y,
            idleBottomOffset,
            walkGroundingCorrection,
            idleGroundingCorrection,
            followingCameras
        );
    }

    private static GameObject FindPlayerRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        return roots.FirstOrDefault(root => root.name == "OkamiWolf_Player") ??
               roots.FirstOrDefault(root => root.name == "Jammo_Player") ??
               roots.FirstOrDefault(root => root.GetComponent<MovementInput>() != null);
    }
}
