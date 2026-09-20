using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheRedDoor.Stage1;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Object = UnityEngine.Object;

namespace TheRedDoor.Stage1.Editor
{
    public static class Stage1UnitySetup
    {
        private const string Root = "Assets/TheRedDoor/Stage1";
        private const string Models = Root + "/Models/Environment";
        private const string Textures = Root + "/Textures/Environment/v11_UnityURP";
        private const string Materials = Root + "/Materials";
        private const string ScenePath = Root + "/Scenes/Stage1_Quest3_SmokeTest.unity";
        private const string XrSettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";

        private static readonly string[] MaterialLabels =
        {
            "Wall_Aged",
            "Seat_Worn",
            "Floor_Dirty",
            "Metal_Rusted",
            "RedDoor_Chipped",
            "Table_Stained",
        };

        private static readonly string[] ModelFiles =
        {
            "Stage1_Environment_Static_v11.fbx",
            "Stage1_Props_Static_v11.fbx",
            "Stage1_RedDoors_Interactive_v11.fbx",
            "Stage1_Markers_v11.fbx",
        };

        [Serializable]
        private sealed class SetupReport
        {
            public string version;
            public string createdUtc;
            public string unityVersion;
            public string projectPath;
            public string activeBuildTarget;
            public string scenePath;
            public bool editorImportValidated;
            public bool questRuntimeValidated;
            public bool openXrLoaderAssigned;
            public bool metaQuestFeatureEnabled;
            public bool oculusTouchProfileEnabled;
            public bool xrOriginPresent;
            public bool mainCameraPresent;
            public bool playerStartMarkerUsed;
            public int fbxCount;
            public int textureCount;
            public int generatedMaterialCount;
            public int rendererCount;
            public int meshFilterCount;
            public int redDoorObjectCount;
            public float[] combinedBoundsSize;
            public string[] packageVersions;
            public string[] modelAssets;
            public string[] warnings;
        }

        [MenuItem("The Red Door/Stage 1/Build Quest 3 Import Scene")]
        public static void Run()
        {
            Debug.Log("[The Red Door] Stage 1 Unity setup started.");
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigurePlayerSettings();
            ConfigureTextureImporters();
            ConfigureModelImporters();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool loaderAssigned = ConfigureOpenXr(
                out bool metaQuestEnabled,
                out bool touchProfileEnabled);

            Dictionary<string, Material> bakedMaterials = CreateBakedMaterials();
            Scene scene = CreateScene(
                bakedMaterials,
                out int generatedMaterialCount,
                out bool playerStartMarkerUsed);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            SetupReport report = BuildReport(
                loaderAssigned,
                metaQuestEnabled,
                touchProfileEnabled,
                generatedMaterialCount,
                playerStartMarkerUsed);
            WriteReport(report);

            if (!report.editorImportValidated)
                throw new InvalidOperationException("Stage 1 editor import validation failed. See the Unity log and report.");

            Debug.Log("V12_UNITY_SETUP_COMPLETE\n" + JsonUtility.ToJson(report, true));
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder("Assets", "TheRedDoor");
            EnsureAssetFolder("Assets/TheRedDoor", "Stage1");
            EnsureAssetFolder(Root, "Materials");
            EnsureAssetFolder(Materials, "GeneratedProps");
            EnsureAssetFolder(Root, "Scenes");
            EnsureAssetFolder("Assets", "XR");
            EnsureAssetFolder("Assets/XR", "Settings");
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigurePlayerSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Run setup with -buildTarget Android.");

            PlayerSettings.productName = "The Red Door — Stage 1";
            PlayerSettings.companyName = "The Red Door Research";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                "com.boruili.thereddoor.stage1");
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.Android,
                new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
        }

        private static void ConfigureTextureImporters()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Textures });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    continue;

                string name = Path.GetFileNameWithoutExtension(path);
                bool isNormal = name.EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
                bool isBaseColor = name.EndsWith("_BaseColor", StringComparison.OrdinalIgnoreCase);

                importer.textureType = isNormal
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                importer.sRGBTexture = isBaseColor;
                importer.mipmapEnabled = true;
                importer.alphaSource = name.EndsWith("_MetallicSmoothness", StringComparison.OrdinalIgnoreCase)
                    ? TextureImporterAlphaSource.FromInput
                    : importer.alphaSource;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.maxTextureSize = name.StartsWith("Table_Stained", StringComparison.OrdinalIgnoreCase)
                    ? 1024
                    : 2048;

                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = importer.maxTextureSize;
                android.format = TextureImporterFormat.ASTC_6x6;
                android.compressionQuality = 50;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureModelImporters()
        {
            foreach (string file in ModelFiles)
            {
                string path = Models + "/" + file;
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    throw new FileNotFoundException("Missing imported FBX", path);

                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Low;
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.generateSecondaryUV = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.SaveAndReimport();
            }
        }

        private static bool ConfigureOpenXr(
            out bool metaQuestEnabled,
            out bool touchProfileEnabled)
        {
            XRGeneralSettingsPerBuildTarget perBuild =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
            if (perBuild == null)
            {
                perBuild = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBuild, XrSettingsPath);
                EditorBuildSettings.AddConfigObject(
                    XRGeneralSettings.k_SettingsKey,
                    perBuild,
                    true);
            }

            if (!perBuild.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perBuild.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            XRGeneralSettings general = perBuild.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);

            XRManagerSettings manager = perBuild.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            bool loaderAssigned = XRPackageMetadataStore.AssignLoader(
                manager,
                typeof(OpenXRLoader).FullName,
                BuildTargetGroup.Android);

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
                throw new InvalidOperationException("OpenXR settings were not created for Android.");

            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(settings);

            var metaQuest = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                MetaQuestFeature.featureId);
            var touchProfile = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                OculusTouchControllerProfile.featureId);
            var simpleProfile = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                KHRSimpleControllerProfile.featureId);

            if (metaQuest == null || touchProfile == null)
                throw new InvalidOperationException("Required Meta Quest OpenXR features were not found.");

            metaQuest.enabled = true;
            touchProfile.enabled = true;
            if (simpleProfile != null)
                simpleProfile.enabled = true;

            EditorUtility.SetDirty(metaQuest);
            EditorUtility.SetDirty(touchProfile);
            if (simpleProfile != null)
                EditorUtility.SetDirty(simpleProfile);
            AssetDatabase.SaveAssets();

            metaQuestEnabled = metaQuest.enabled;
            touchProfileEnabled = touchProfile.enabled;
            return loaderAssigned && manager.activeLoaders.Any(loader => loader is OpenXRLoader);
        }

        private static Dictionary<string, Material> CreateBakedMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader is unavailable.");

            var result = new Dictionary<string, Material>();
            foreach (string label in MaterialLabels)
            {
                string folder = Textures + "/" + label;
                Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + "/" + label + "_BaseColor.png");
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + "/" + label + "_Normal.png");
                Texture2D metallicSmoothness = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + "/" + label + "_MetallicSmoothness.png");
                Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    folder + "/" + label + "_AO.png");

                if (baseColor == null || normal == null || metallicSmoothness == null || ao == null)
                    throw new FileNotFoundException("Incomplete Unity texture set for " + label);

                string materialPath = Materials + "/MAT_URP_" + label + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "MAT_URP_" + label };
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.shader = shader;
                material.SetTexture("_BaseMap", baseColor);
                material.SetTexture("_BumpMap", normal);
                material.SetTexture("_MetallicGlossMap", metallicSmoothness);
                material.SetTexture("_OcclusionMap", ao);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.SetFloat("_BumpScale", 1f);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                EditorUtility.SetDirty(material);
                result[label] = material;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static Scene CreateScene(
            Dictionary<string, Material> bakedMaterials,
            out int generatedMaterialCount,
            out bool playerStartMarkerUsed)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Stage1_Quest3_SmokeTest";

            GameObject stageRoot = new GameObject("The Red Door — Stage 1 v12");
            stageRoot.AddComponent<Quest3SmokeTestBootstrap>();

            GameObject environment = InstantiateModel(ModelFiles[0], "Environment_Static_v11", stageRoot.transform);
            GameObject props = InstantiateModel(ModelFiles[1], "Props_Static_v11", stageRoot.transform);
            GameObject doors = InstantiateModel(ModelFiles[2], "RedDoors_Interactive_TODO", stageRoot.transform);
            GameObject markers = InstantiateModel(ModelFiles[3], "Markers_v11", stageRoot.transform);

            SetStaticRecursively(environment, true);
            SetStaticRecursively(props, true);
            SetStaticRecursively(doors, false);

            generatedMaterialCount = ReplaceMaterials(stageRoot, bakedMaterials);

            Transform playerStart = FindChild(markers.transform, "PLAYER_START");
            GameObject xrOriginObject = CreateXrOrigin();
            xrOriginObject.transform.SetParent(stageRoot.transform, true);
            if (playerStart != null)
            {
                xrOriginObject.transform.SetPositionAndRotation(
                    playerStart.position,
                    Quaternion.Euler(0f, playerStart.eulerAngles.y, 0f));
                playerStartMarkerUsed = true;
            }
            else
            {
                xrOriginObject.transform.position = new Vector3(4.72f, 0f, 0f);
                xrOriginObject.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                playerStartMarkerUsed = false;
            }

            markers.SetActive(false);
            CreateSmokeTestCube(stageRoot.transform, xrOriginObject.transform);
            CreateTemporaryLighting(stageRoot.transform);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.085f, 0.095f, 1f);
            return scene;
        }

        private static GameObject InstantiateModel(string file, string instanceName, Transform parent)
        {
            string path = Models + "/" + file;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new FileNotFoundException("Unity could not load FBX", path);
            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + file);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static GameObject CreateXrOrigin()
        {
            GameObject originObject = new GameObject("XR Origin (Quest 3 OpenXR)");
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObject.transform, false);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraOffset.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            cameraObject.AddComponent<AudioListener>();

            TrackedPoseDriver driver = cameraObject.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;
            driver.positionInput = new InputActionProperty(new InputAction(
                name: "HMD Position",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction(
                name: "HMD Rotation",
                type: InputActionType.Value,
                binding: "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction(
                name: "HMD Tracking State",
                type: InputActionType.Value,
                binding: "<XRHMD>/trackingState",
                expectedControlType: "Integer"));

            XROrigin origin = originObject.AddComponent<XROrigin>();
            origin.Origin = originObject;
            origin.CameraFloorOffsetObject = cameraOffset;
            origin.Camera = camera;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0f;
            return originObject;
        }

        private static void CreateSmokeTestCube(Transform parent, Transform origin)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Quest3_SmokeTest_Cube";
            cube.transform.SetParent(parent, true);
            cube.transform.position = origin.TransformPoint(new Vector3(0f, 1.35f, 2f));
            cube.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            string path = Materials + "/MAT_Quest3_SmokeTestCube.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "MAT_Quest3_SmokeTestCube";
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.9f, 0.08f, 0.05f, 1f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.35f);
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateTemporaryLighting(Transform parent)
        {
            GameObject lightRoot = new GameObject("TEMP_Lighting_SmokeTest");
            lightRoot.transform.SetParent(parent, false);
            float[] xPositions = { -4f, -1.5f, 1.5f, 4.5f };
            foreach (float x in xPositions)
            {
                GameObject lightObject = new GameObject("TEMP_Ceiling_" + x.ToString("0.0"));
                lightObject.transform.SetParent(lightRoot.transform, false);
                lightObject.transform.position = new Vector3(x, 2.25f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.86f, 0.68f, 1f);
                light.intensity = 650f;
                light.range = 4f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
            }
        }

        private static int ReplaceMaterials(
            GameObject root,
            Dictionary<string, Material> bakedMaterials)
        {
            var generated = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < slots.Length; index++)
                {
                    Material source = slots[index];
                    if (source == null)
                        continue;

                    Material replacement = FindBakedReplacement(source.name, bakedMaterials);
                    if (replacement == null && !source.shader.name.Contains("Universal Render Pipeline"))
                    {
                        if (!generated.TryGetValue(source.name, out replacement))
                        {
                            replacement = CreateFallbackUrpMaterial(source);
                            generated[source.name] = replacement;
                        }
                    }

                    if (replacement != null && replacement != source)
                    {
                        slots[index] = replacement;
                        changed = true;
                    }
                }
                if (changed)
                    renderer.sharedMaterials = slots;
            }
            return generated.Count;
        }

        private static Material FindBakedReplacement(
            string sourceName,
            Dictionary<string, Material> bakedMaterials)
        {
            foreach (KeyValuePair<string, Material> pair in bakedMaterials)
            {
                if (sourceName.IndexOf("MAT_Baked_" + pair.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return pair.Value;
            }
            return null;
        }

        private static Material CreateFallbackUrpMaterial(Material source)
        {
            string safeName = SanitizeFileName(source.name);
            string path = Materials + "/GeneratedProps/MAT_URP_Fallback_" + safeName + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "MAT_URP_Fallback_" + safeName;
                AssetDatabase.CreateAsset(material, path);
            }

            Color color = Color.gray;
            if (source.HasProperty("_BaseColor"))
                color = source.GetColor("_BaseColor");
            else if (source.HasProperty("_Color"))
                color = source.GetColor("_Color");

            bool transparent = source.name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0
                || source.name.IndexOf("Lens", StringComparison.OrdinalIgnoreCase) >= 0
                || source.name.IndexOf("Drink", StringComparison.OrdinalIgnoreCase) >= 0
                || source.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
            if (transparent)
                color.a = Mathf.Min(color.a <= 0f ? 0.28f : color.a, 0.4f);

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
            material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.35f);
            if (transparent)
                SetTransparent(material);
            else
                SetOpaque(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void SetOpaque(Material material)
        {
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Geometry;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_').Replace('.', '_');
        }

        private static Transform FindChild(Transform root, string childName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name.Equals(childName, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetStaticRecursively(GameObject root, bool value)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.isStatic = value;
        }

        private static SetupReport BuildReport(
            bool loaderAssigned,
            bool metaQuestEnabled,
            bool touchProfileEnabled,
            int generatedMaterialCount,
            bool playerStartMarkerUsed)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            Camera mainCamera = Camera.main;
            bool hasBounds = renderers.Length > 0;
            Bounds combined = hasBounds ? renderers[0].bounds : new Bounds();
            for (int index = 1; index < renderers.Length; index++)
                combined.Encapsulate(renderers[index].bounds);

            int redDoorObjectCount = GameObject.Find("RedDoors_Interactive_TODO")?
                .GetComponentsInChildren<MeshFilter>(true).Length ?? 0;
            int textureCount = AssetDatabase.FindAssets("t:Texture2D", new[] { Textures }).Length;
            bool sceneExists = File.Exists(Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                ScenePath));
            bool valid = sceneExists
                && ModelFiles.All(file => AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + file) != null)
                && textureCount == 24
                && xrOrigin != null
                && mainCamera != null
                && loaderAssigned
                && metaQuestEnabled
                && touchProfileEnabled
                && redDoorObjectCount == 5;

            return new SetupReport
            {
                version = "v12",
                createdUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                projectPath = Directory.GetParent(Application.dataPath)!.FullName,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                scenePath = ScenePath,
                editorImportValidated = valid,
                questRuntimeValidated = false,
                openXrLoaderAssigned = loaderAssigned,
                metaQuestFeatureEnabled = metaQuestEnabled,
                oculusTouchProfileEnabled = touchProfileEnabled,
                xrOriginPresent = xrOrigin != null,
                mainCameraPresent = mainCamera != null,
                playerStartMarkerUsed = playerStartMarkerUsed,
                fbxCount = ModelFiles.Length,
                textureCount = textureCount,
                generatedMaterialCount = generatedMaterialCount + MaterialLabels.Length + 1,
                rendererCount = renderers.Length,
                meshFilterCount = meshFilters.Length,
                redDoorObjectCount = redDoorObjectCount,
                combinedBoundsSize = new[] { combined.size.x, combined.size.y, combined.size.z },
                packageVersions = GetPackageVersions(),
                modelAssets = ModelFiles.Select(file => Models + "/" + file).ToArray(),
                warnings = new[]
                {
                    "Unity Editor asset import and scene assembly are validated; Quest 3 runtime is not yet validated.",
                    "Lighting is temporary smoke-test lighting, not final Stage 1 lighting.",
                    "Small lifestyle props use generated URP fallback materials and can receive dedicated PBR maps later.",
                    "Red doors remain separate and contain no interaction scripts in this iteration.",
                    "Passenger rigs, particle effects, FMOD, and gameplay logic are not imported in this iteration.",
                },
            };
        }

        private static string[] GetPackageVersions()
        {
            string[] names =
            {
                "com.unity.render-pipelines.universal",
                "com.unity.inputsystem",
                "com.unity.xr.management",
                "com.unity.xr.openxr",
                "com.unity.xr.interaction.toolkit",
            };
            var versions = new List<string>();
            foreach (string packageName in names)
            {
                UnityEditor.PackageManager.PackageInfo info =
                    UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                        "Packages/" + packageName + "/package.json");
                versions.Add(packageName + "@" + (info != null ? info.version : "missing"));
            }
            return versions.ToArray();
        }

        private static void WriteReport(SetupReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string documentation = Path.Combine(projectRoot, "Documentation");
            Directory.CreateDirectory(documentation);
            File.WriteAllText(
                Path.Combine(documentation, "Stage1_UnityImport_v12_Report.json"),
                JsonUtility.ToJson(report, true));
            File.WriteAllText(
                Path.Combine(documentation, "README_Stage1_Unity_v12.txt"),
                "The Red Door — Stage 1 Unity v12\n\n"
                + "Open Assets/TheRedDoor/Stage1/Scenes/Stage1_Quest3_SmokeTest.unity.\n"
                + "The scene contains the v11 carriage, props, independent red doors, markers, an XR Origin, and a smoke-test cube.\n"
                + "This iteration is validated in the Unity Editor only. It has not been built or run on Quest 3.\n"
                + "Temporary lights are clearly named TEMP_Lighting_SmokeTest and may be replaced later.\n"
                + "No particles, FMOD, passenger rigs, door interaction, or gameplay logic were added.\n");
        }
    }
}
