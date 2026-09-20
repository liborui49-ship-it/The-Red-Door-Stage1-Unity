using System;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using Object = UnityEngine.Object;

namespace TheRedDoor.Stage1.Editor
{
    public static class Stage1UnityValidator
    {
        private const string ScenePath = "Assets/TheRedDoor/Stage1/Scenes/Stage1_Quest3_SmokeTest.unity";
        private const string TextureRoot = "Assets/TheRedDoor/Stage1/Textures/Environment/v11_UnityURP";

        [Serializable]
        private sealed class ValidationReport
        {
            public string validatedUtc;
            public string unityVersion;
            public bool passed;
            public bool sceneReopened;
            public bool xrOriginPresent;
            public bool mainCameraPresent;
            public bool openXrLoaderAssigned;
            public bool metaQuestFeatureEnabled;
            public bool oculusTouchProfileEnabled;
            public bool androidIsActiveTarget;
            public int textureCount;
            public int rendererCount;
            public int meshFilterCount;
            public int redDoorMeshCount;
            public int missingMaterialCount;
            public int errorShaderMaterialCount;
            public float[] boundsSize;
            public bool questRuntimeValidated;
            public string note;
        }

        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            XROrigin origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            Camera camera = Camera.main;
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject doors = GameObject.Find("RedDoors_Interactive_TODO");
            int redDoorMeshCount = doors != null ? doors.GetComponentsInChildren<MeshFilter>(true).Length : 0;
            int textureCount = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureRoot }).Length;

            int missingMaterials = 0;
            int errorShaders = 0;
            foreach (Renderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        missingMaterials++;
                        continue;
                    }
                    if (material.shader == null || material.shader.name.Contains("InternalErrorShader"))
                        errorShaders++;
                }
            }

            Bounds combined = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            XRGeneralSettings general =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            bool loaderAssigned = general != null
                && general.Manager != null
                && general.Manager.activeLoaders.Any(loader => loader is OpenXRLoader);
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var metaQuest = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                MetaQuestFeature.featureId);
            var touch = FeatureHelpers.GetFeatureWithIdForBuildTarget(
                BuildTargetGroup.Android,
                OculusTouchControllerProfile.featureId);

            bool passed = scene.IsValid()
                && origin != null
                && camera != null
                && loaderAssigned
                && metaQuest != null && metaQuest.enabled
                && touch != null && touch.enabled
                && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android
                && textureCount == 24
                && redDoorMeshCount == 5
                && missingMaterials == 0
                && errorShaders == 0
                && renderers.Length == 235
                && meshFilters.Length == 235;

            var report = new ValidationReport
            {
                validatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                passed = passed,
                sceneReopened = scene.IsValid(),
                xrOriginPresent = origin != null,
                mainCameraPresent = camera != null,
                openXrLoaderAssigned = loaderAssigned,
                metaQuestFeatureEnabled = metaQuest != null && metaQuest.enabled,
                oculusTouchProfileEnabled = touch != null && touch.enabled,
                androidIsActiveTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                textureCount = textureCount,
                rendererCount = renderers.Length,
                meshFilterCount = meshFilters.Length,
                redDoorMeshCount = redDoorMeshCount,
                missingMaterialCount = missingMaterials,
                errorShaderMaterialCount = errorShaders,
                boundsSize = new[] { combined.size.x, combined.size.y, combined.size.z },
                questRuntimeValidated = false,
                note = "Fresh Unity process reopened and validated the scene. No APK was built or run on Quest 3.",
            };

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            File.WriteAllText(
                Path.Combine(projectRoot, "Documentation/Stage1_UnityValidation_v12.json"),
                JsonUtility.ToJson(report, true));
            Debug.Log("V12_UNITY_VALIDATION_COMPLETE\n" + JsonUtility.ToJson(report, true));

            if (!passed)
                throw new InvalidOperationException("Fresh-process Unity scene validation failed.");
        }
    }
}
