using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheRedDoor.Stage1;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TheRedDoor.Stage1.Editor
{
    public static class Stage1VerticalSliceSetup
    {
        public const string SourceScenePath =
            "Assets/TheRedDoor/Stage1/Scenes/Stage1_Quest3_SmokeTest.unity";
        public const string ScenePath =
            "Assets/TheRedDoor/Stage1/Scenes/Stage1_VerticalSlice_v01.unity";

        private static readonly string[] ArchitectureColliderNames =
        {
            "GEO_Floor",
            "GEO_Vestibule_Floor",
            "GEO_Wall_Left",
            "GEO_Wall_Right",
            "GEO_Vestibule_Wall_Left",
            "GEO_Vestibule_Wall_Right",
            "GEO_Vestibule_RearFrame",
            "GEO_EndWall_RD1",
            "GEO_EndWall_RD2",
        };

        private static readonly string[] BoxColliderPrefixes =
        {
            "GEO_Seat_Row_",
            "GEO_Table_Left_",
            "GEO_Table_Right_",
            "GEO_Luggage_",
            "PROP_v08_Row01_Left_SideCase",
            "PROP_v08_Row01_Right_SideCase",
            "PROP_v08_Row06_Left_SideCase",
            "PROP_v08_Row06_Right_SideCase",
            "PROP_Vestibule_TrashBin_Body",
        };

        [Serializable]
        private sealed class SetupReport
        {
            public string createdUtc;
            public string unityVersion;
            public string sourceScene;
            public string scenePath;
            public bool sourceScenePreserved;
            public bool xrOriginPresent;
            public bool locomotionConfigured;
            public bool firstRedDoorConfigured;
            public bool firstRedDoorOpensAwayFromStart;
            public bool smokeTestCubeRemovedFromVerticalSlice;
            public int colliderCount;
            public int meshColliderCount;
            public int boxColliderCount;
            public int baselineLightCount;
            public int lightProbeCount;
            public int reflectionProbeCount;
            public bool questRuntimeValidated;
            public string[] notes;
        }

        [MenuItem("The Red Door/Stage 1/Build Vertical Slice v01")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ScenePath, true))
                throw new InvalidOperationException("Could not create the vertical-slice scene copy.");

            GameObject stageRoot = FindSceneObject("The Red Door — Stage 1 v12");
            if (stageRoot == null)
                throw new InvalidOperationException("Stage 1 root was not found.");
            stageRoot.name = "The Red Door — Stage 1 Vertical Slice v01";

            Quest3SmokeTestBootstrap oldBootstrap =
                stageRoot.GetComponent<Quest3SmokeTestBootstrap>();
            if (oldBootstrap != null)
                Object.DestroyImmediate(oldBootstrap);
            stageRoot.AddComponent<Stage1VerticalSliceBootstrap>();

            GameObject smokeCube = FindSceneObject("Quest3_SmokeTest_Cube");
            if (smokeCube != null)
                Object.DestroyImmediate(smokeCube);
            GameObject temporaryLighting = FindSceneObject("TEMP_Lighting_SmokeTest");
            if (temporaryLighting != null)
                Object.DestroyImmediate(temporaryLighting);

            XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>(
                FindObjectsInactive.Include);
            if (xrOrigin == null || xrOrigin.Camera == null)
                throw new InvalidOperationException("XR Origin or its camera is missing.");

            ConfigureLocomotion(xrOrigin);
            ConfigureColliders();
            ConfigureFirstRedDoor(stageRoot.transform, xrOrigin.Camera.transform);
            ConfigureBaselineLighting(stageRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(SourceScenePath, false),
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            WriteReport(BuildReport());
            Debug.Log("V13_VERTICAL_SLICE_SETUP_COMPLETE");
        }

        private static void ConfigureLocomotion(XROrigin xrOrigin)
        {
            CharacterController controller =
                xrOrigin.GetComponent<CharacterController>();
            if (controller == null)
                controller = xrOrigin.gameObject.AddComponent<CharacterController>();
            controller.radius = 0.24f;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.18f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;

            Stage1LocomotionController locomotion =
                xrOrigin.GetComponent<Stage1LocomotionController>();
            if (locomotion == null)
                locomotion = xrOrigin.gameObject.AddComponent<Stage1LocomotionController>();
            locomotion.Configure(xrOrigin.Camera, 1.2f, 30f);
        }

        private static void ConfigureColliders()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (string objectName in ArchitectureColliderNames)
            {
                GameObject target = transforms
                    .FirstOrDefault(transform => transform.name == objectName)?
                    .gameObject;
                if (target == null)
                    throw new InvalidOperationException("Missing collision mesh: " + objectName);
                AddMeshCollider(target);
            }

            foreach (Transform transform in transforms)
            {
                if (BoxColliderPrefixes.Any(prefix =>
                        transform.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    AddBoxCollider(transform.gameObject);
            }

            GameObject firstDoor = FindSceneObject("INT_RD1");
            GameObject secondDoor = FindSceneObject("INT_RD2");
            AddBoxCollider(firstDoor);
            AddBoxCollider(secondDoor);
        }

        private static void AddMeshCollider(GameObject target)
        {
            MeshFilter filter = target != null ? target.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                throw new InvalidOperationException("No mesh found for " + target?.name);
            MeshCollider collider = target.GetComponent<MeshCollider>();
            if (collider == null)
                collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }

        private static void AddBoxCollider(GameObject target)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot add a collider to a missing object.");
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;
            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
                collider = target.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
        }

        private static void ConfigureFirstRedDoor(Transform stageRoot, Transform viewer)
        {
            GameObject door = FindSceneObject("INT_RD1");
            if (door == null)
                throw new InvalidOperationException("INT_RD1 was not found.");

            Bounds bounds = CombinedBounds(door.GetComponentsInChildren<Renderer>(true));
            GameObject pivot = new GameObject("RD1_InteractionPivot");
            pivot.transform.SetParent(stageRoot, true);
            pivot.transform.position = new Vector3(
                bounds.center.x,
                bounds.min.y,
                bounds.max.z);
            door.transform.SetParent(pivot.transform, true);
            SetStaticRecursively(door, false);

            Stage1RedDoorController controller =
                pivot.AddComponent<Stage1RedDoorController>();
            controller.Configure(
                viewer,
                1.25f,
                -95f,
                0.9f,
                true,
                door.GetComponent<BoxCollider>());
        }

        private static void ConfigureBaselineLighting(Transform stageRoot)
        {
            GameObject lightRoot = new GameObject("Stage1_Lighting_Baseline_v01");
            lightRoot.transform.SetParent(stageRoot, false);

            float[] xPositions = { -4.2f, -1.4f, 1.4f, 4.2f };
            foreach (float x in xPositions)
            {
                GameObject lightObject = new GameObject("LGT_Ceiling_" + x.ToString("0.0"));
                lightObject.transform.SetParent(lightRoot.transform, false);
                lightObject.transform.position = new Vector3(x, 2.32f, 0f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.83f, 0.62f, 1f);
                light.intensity = 420f;
                light.range = 3.2f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Mixed;
            }

            GameObject accentObject = new GameObject("LGT_RD1_RedAccent");
            accentObject.transform.SetParent(lightRoot.transform, false);
            accentObject.transform.position = new Vector3(-5.05f, 2.12f, 0f);
            accentObject.transform.LookAt(new Vector3(-5.56f, 1.05f, 0f));
            Light accent = accentObject.AddComponent<Light>();
            accent.type = LightType.Spot;
            accent.color = new Color(0.88f, 0.08f, 0.045f, 1f);
            accent.intensity = 260f;
            accent.range = 3f;
            accent.spotAngle = 58f;
            accent.innerSpotAngle = 32f;
            accent.shadows = LightShadows.None;
            accent.lightmapBakeType = LightmapBakeType.Mixed;

            GameObject probesObject = new GameObject("LightProbes_Aisle");
            probesObject.transform.SetParent(lightRoot.transform, false);
            LightProbeGroup probeGroup = probesObject.AddComponent<LightProbeGroup>();
            var positions = new List<Vector3>();
            foreach (float x in new[] { -6.6f, -5.0f, -2.5f, 0f, 2.5f, 5f })
            foreach (float y in new[] { 0.25f, 1.25f, 2.15f })
            foreach (float z in new[] { -0.72f, 0.72f })
                positions.Add(new Vector3(x, y, z));
            probeGroup.probePositions = positions.ToArray();

            CreateReflectionProbe(
                lightRoot.transform,
                "REFL_Carriage",
                new Vector3(0f, 1.3f, 0f),
                new Vector3(10.8f, 2.5f, 2.85f));
            CreateReflectionProbe(
                lightRoot.transform,
                "REFL_Vestibule",
                new Vector3(-6.62f, 1.25f, 0f),
                new Vector3(2f, 2.45f, 2.65f));

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.06f, 0.075f, 1f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.7f;
        }

        private static void CreateReflectionProbe(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 size)
        {
            GameObject probeObject = new GameObject(name);
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.position = position;
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = size;
            probe.boxProjection = true;
            probe.hdr = true;
            probe.intensity = 0.8f;
        }

        private static SetupReport BuildReport()
        {
            Collider[] colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Stage1LocomotionController locomotion =
                Object.FindFirstObjectByType<Stage1LocomotionController>(
                    FindObjectsInactive.Include);
            Stage1RedDoorController door =
                Object.FindFirstObjectByType<Stage1RedDoorController>(
                    FindObjectsInactive.Include);
            LightProbeGroup probes = Object.FindFirstObjectByType<LightProbeGroup>(
                FindObjectsInactive.Include);

            return new SetupReport
            {
                createdUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                sourceScene = SourceScenePath,
                scenePath = ScenePath,
                sourceScenePreserved = File.Exists(Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    SourceScenePath)),
                xrOriginPresent = Object.FindFirstObjectByType<XROrigin>(
                    FindObjectsInactive.Include) != null,
                locomotionConfigured = locomotion != null
                    && locomotion.ViewCamera != null,
                firstRedDoorConfigured = door != null,
                firstRedDoorOpensAwayFromStart = door != null && door.OpenAngle < 0f,
                smokeTestCubeRemovedFromVerticalSlice =
                    FindSceneObject("Quest3_SmokeTest_Cube") == null,
                colliderCount = colliders.Length,
                meshColliderCount = colliders.OfType<MeshCollider>().Count(),
                boxColliderCount = colliders.OfType<BoxCollider>().Count(),
                baselineLightCount = Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                lightProbeCount = probes != null ? probes.probePositions.Length : 0,
                reflectionProbeCount = Object.FindObjectsByType<ReflectionProbe>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                questRuntimeValidated = false,
                notes = new[]
                {
                    "The original smoke-test scene remains unchanged and disabled in Build Settings.",
                    "Locomotion uses the left controller stick; snap turning uses the right controller stick.",
                    "WASD and Q/E are available for editor-side input checks.",
                    "The first red door opens once by proximity; grab interaction is deferred.",
                    "Lighting is a mixed-light baseline. Final baking and Quest performance tuning are deferred.",
                    "No passenger animation, smoke particles, FMOD, or final audio was added.",
                    "Quest 3 runtime is not yet validated.",
                },
            };
        }

        private static void WriteReport(SetupReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            File.WriteAllText(
                Path.Combine(projectRoot, "Documentation/Stage1_VerticalSlice_v01_Report.json"),
                JsonUtility.ToJson(report, true));
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            if (renderers.Length == 0)
                throw new InvalidOperationException("No renderer bounds were found.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            return Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(transform => transform.gameObject.scene.IsValid())
                .Select(transform => transform.gameObject)
                .FirstOrDefault(gameObject => gameObject.name == objectName);
        }

        private static void SetStaticRecursively(GameObject root, bool value)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.isStatic = value;
        }
    }
}
