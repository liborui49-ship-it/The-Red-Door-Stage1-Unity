using System;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TheRedDoor.Stage1.Editor
{
    public static class Stage1SceneAudit
    {
        private const string ScenePath =
            "Assets/TheRedDoor/Stage1/Scenes/Stage1_Quest3_SmokeTest.unity";

        [Serializable]
        private sealed class SceneItem
        {
            public string path;
            public string mesh;
            public float[] position;
            public float[] rotation;
            public float[] boundsCenter;
            public float[] boundsSize;
        }

        [Serializable]
        private sealed class AuditReport
        {
            public string createdUtc;
            public string unityVersion;
            public string scenePath;
            public int rendererCount;
            public int colliderCount;
            public int lightCount;
            public float[] xrOriginPosition;
            public float[] combinedBoundsCenter;
            public float[] combinedBoundsSize;
            public SceneItem[] environmentRenderers;
            public SceneItem[] propRenderers;
            public SceneItem[] doorRenderers;
            public SceneItem[] markers;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Bounds combined = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            for (int index = 1; index < renderers.Length; index++)
                combined.Encapsulate(renderers[index].bounds);

            XROrigin xrOrigin = Object.FindFirstObjectByType<XROrigin>(
                FindObjectsInactive.Include);
            GameObject environment = FindSceneObject("Environment_Static_v11");
            GameObject props = FindSceneObject("Props_Static_v11");
            GameObject doors = FindSceneObject("RedDoors_Interactive_TODO");
            GameObject markerRoot = FindSceneObject("Markers_v11");

            var report = new AuditReport
            {
                createdUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                scenePath = ScenePath,
                rendererCount = renderers.Length,
                colliderCount = Object.FindObjectsByType<Collider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                lightCount = Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                xrOriginPosition = Vector(xrOrigin != null
                    ? xrOrigin.transform.position
                    : Vector3.zero),
                combinedBoundsCenter = Vector(combined.center),
                combinedBoundsSize = Vector(combined.size),
                environmentRenderers = DescribeRenderers(environment),
                propRenderers = DescribeRenderers(props),
                doorRenderers = DescribeRenderers(doors),
                markers = DescribeTransforms(markerRoot),
            };

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string output = Path.Combine(
                projectRoot,
                "Documentation/Stage1_SceneAudit_v13.json");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("V13_SCENE_AUDIT_COMPLETE\n" + JsonUtility.ToJson(report, true));
        }

        private static SceneItem[] DescribeRenderers(GameObject root)
        {
            if (root == null)
                return Array.Empty<SceneItem>();

            return root.GetComponentsInChildren<Renderer>(true)
                .OrderBy(renderer => HierarchyPath(renderer.transform))
                .Select(renderer =>
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    return new SceneItem
                    {
                        path = HierarchyPath(renderer.transform),
                        mesh = filter != null && filter.sharedMesh != null
                            ? filter.sharedMesh.name
                            : string.Empty,
                        position = Vector(renderer.transform.position),
                        rotation = Vector(renderer.transform.eulerAngles),
                        boundsCenter = Vector(renderer.bounds.center),
                        boundsSize = Vector(renderer.bounds.size),
                    };
                })
                .ToArray();
        }

        private static SceneItem[] DescribeTransforms(GameObject root)
        {
            if (root == null)
                return Array.Empty<SceneItem>();

            return root.GetComponentsInChildren<Transform>(true)
                .OrderBy(transform => HierarchyPath(transform))
                .Select(transform => new SceneItem
                {
                    path = HierarchyPath(transform),
                    mesh = string.Empty,
                    position = Vector(transform.position),
                    rotation = Vector(transform.eulerAngles),
                    boundsCenter = Array.Empty<float>(),
                    boundsSize = Array.Empty<float>(),
                })
                .ToArray();
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
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

        private static float[] Vector(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }
    }
}
