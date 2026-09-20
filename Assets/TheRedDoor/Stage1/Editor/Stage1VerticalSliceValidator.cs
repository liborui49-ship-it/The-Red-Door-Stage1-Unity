using System;
using System.IO;
using System.Linq;
using TheRedDoor.Stage1;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TheRedDoor.Stage1.Editor
{
    public static class Stage1VerticalSliceValidator
    {
        [Serializable]
        private sealed class ValidationReport
        {
            public string validatedUtc;
            public string unityVersion;
            public bool passed;
            public bool sceneReopened;
            public bool sourceSceneStillExists;
            public bool verticalSliceIsFirstBuildScene;
            public bool xrOriginPresent;
            public bool characterControllerPresent;
            public bool locomotionPresent;
            public bool locomotionHasCamera;
            public bool firstRedDoorControllerPresent;
            public bool firstRedDoorColliderPresent;
            public bool firstRedDoorPassageClearWhenOpen;
            public bool centerAisleClear;
            public string[] firstRedDoorBlockers;
            public string[] centerAisleBlockers;
            public int colliderCount;
            public int meshColliderCount;
            public int boxColliderCount;
            public int lightCount;
            public int lightProbeCount;
            public int reflectionProbeCount;
            public int missingMaterialCount;
            public int errorShaderMaterialCount;
            public bool questRuntimeValidated;
            public string note;
        }

        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(
                Stage1VerticalSliceSetup.ScenePath,
                OpenSceneMode.Single);

            XROrigin origin = Object.FindFirstObjectByType<XROrigin>(
                FindObjectsInactive.Include);
            CharacterController character = origin != null
                ? origin.GetComponent<CharacterController>()
                : null;
            Stage1LocomotionController locomotion = origin != null
                ? origin.GetComponent<Stage1LocomotionController>()
                : null;
            Stage1RedDoorController door =
                Object.FindFirstObjectByType<Stage1RedDoorController>(
                    FindObjectsInactive.Include);
            BoxCollider firstDoorCollider = FindSceneObject("INT_RD1")?
                .GetComponent<BoxCollider>();
            Collider[] colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            LightProbeGroup probes = Object.FindFirstObjectByType<LightProbeGroup>(
                FindObjectsInactive.Include);
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int missingMaterials = 0;
            int errorShaders = 0;
            foreach (Renderer renderer in renderers)
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    missingMaterials++;
                    continue;
                }
                if (material.shader == null
                    || material.shader.name.Contains("InternalErrorShader"))
                    errorShaders++;
            }

            bool sourceSceneExists = File.Exists(Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                Stage1VerticalSliceSetup.SourceScenePath));
            bool buildSceneCorrect = EditorBuildSettings.scenes.Length > 0
                && EditorBuildSettings.scenes[0].enabled
                && EditorBuildSettings.scenes[0].path == Stage1VerticalSliceSetup.ScenePath;
            int lightCount = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            int reflectionProbeCount = Object.FindObjectsByType<ReflectionProbe>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;

            bool doorPassageClear = false;
            bool centerAisleClear = false;
            string[] doorBlockers = Array.Empty<string>();
            string[] aisleBlockers = Array.Empty<string>();
            if (door != null)
            {
                Quaternion originalRotation = door.transform.localRotation;
                bool originalColliderEnabled = firstDoorCollider != null
                    && firstDoorCollider.enabled;
                door.transform.localRotation = originalRotation
                    * Quaternion.Euler(0f, door.OpenAngle, 0f);
                if (firstDoorCollider != null)
                    firstDoorCollider.enabled = false;
                Physics.SyncTransforms();

                doorBlockers = Physics.RaycastAll(
                        new Vector3(-6.35f, 1f, 0f),
                        Vector3.right,
                        1.55f)
                    .Where(hit => IsBlocking(hit.collider, character))
                    .Select(hit => hit.collider.name)
                    .Distinct()
                    .ToArray();
                doorPassageClear = doorBlockers.Length == 0;

                aisleBlockers = Physics.CapsuleCastAll(
                        new Vector3(-5.25f, 0.27f, 0f),
                        new Vector3(-5.25f, 1.68f, 0f),
                        0.2f,
                        Vector3.right,
                        10.3f)
                    .Where(hit => IsBlocking(hit.collider, character))
                    .Select(hit => hit.collider.name)
                    .Distinct()
                    .ToArray();
                centerAisleClear = aisleBlockers.Length == 0;

                door.transform.localRotation = originalRotation;
                if (firstDoorCollider != null)
                    firstDoorCollider.enabled = originalColliderEnabled;
                Physics.SyncTransforms();
            }

            bool passed = scene.IsValid()
                && sourceSceneExists
                && buildSceneCorrect
                && origin != null
                && origin.Camera != null
                && character != null
                && locomotion != null
                && locomotion.ViewCamera == origin.Camera
                && door != null
                && door.OpenAngle < 0f
                && firstDoorCollider != null
                && door.BlockingCollider == firstDoorCollider
                && doorPassageClear
                && centerAisleClear
                && colliders.Length >= 25
                && lightCount == 5
                && probes != null && probes.probePositions.Length >= 24
                && reflectionProbeCount == 2
                && missingMaterials == 0
                && errorShaders == 0;

            var report = new ValidationReport
            {
                validatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                passed = passed,
                sceneReopened = scene.IsValid(),
                sourceSceneStillExists = sourceSceneExists,
                verticalSliceIsFirstBuildScene = buildSceneCorrect,
                xrOriginPresent = origin != null,
                characterControllerPresent = character != null,
                locomotionPresent = locomotion != null,
                locomotionHasCamera = locomotion != null
                    && origin != null
                    && locomotion.ViewCamera == origin.Camera,
                firstRedDoorControllerPresent = door != null,
                firstRedDoorColliderPresent = firstDoorCollider != null,
                firstRedDoorPassageClearWhenOpen = doorPassageClear,
                centerAisleClear = centerAisleClear,
                firstRedDoorBlockers = doorBlockers,
                centerAisleBlockers = aisleBlockers,
                colliderCount = colliders.Length,
                meshColliderCount = colliders.OfType<MeshCollider>().Count(),
                boxColliderCount = colliders.OfType<BoxCollider>().Count(),
                lightCount = lightCount,
                lightProbeCount = probes != null ? probes.probePositions.Length : 0,
                reflectionProbeCount = reflectionProbeCount,
                missingMaterialCount = missingMaterials,
                errorShaderMaterialCount = errorShaders,
                questRuntimeValidated = false,
                note = "Fresh Unity process reopened and validated the vertical-slice scene. No APK was built or run on Quest 3.",
            };

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            File.WriteAllText(
                Path.Combine(projectRoot, "Documentation/Stage1_VerticalSlice_v01_Validation.json"),
                JsonUtility.ToJson(report, true));
            Debug.Log("V13_VERTICAL_SLICE_VALIDATION_COMPLETE\n"
                + JsonUtility.ToJson(report, true));

            if (!passed)
                throw new InvalidOperationException("Vertical-slice validation failed.");
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

        private static bool IsBlocking(
            Collider collider,
            CharacterController playerController)
        {
            return collider != null
                && collider.enabled
                && !collider.isTrigger
                && collider != playerController;
        }
    }
}
