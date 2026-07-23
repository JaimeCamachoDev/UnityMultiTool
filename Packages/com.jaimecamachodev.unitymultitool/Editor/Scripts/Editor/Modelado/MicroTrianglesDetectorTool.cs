using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class MicroTrianglesDetectorTool
    {
        private static GameObject sceneObject;
        private static readonly List<(Vector3 v1, Vector3 v2, Vector3 v3)> problematicTriangles = new List<(Vector3, Vector3, Vector3)>();
        private static int selectedTriangleIndex = -1;

        private static float minAreaThreshold = 0.01f;
        private static float maxEdgeRatioThreshold = 10f;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Micro Triangle Detector") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });

            var statusContainer = new VisualElement();
            var analyzeButtonContainer = new VisualElement { style = { marginTop = 10 } };
            var resultsContainer = new VisualElement { style = { marginTop = 6 } };

            var objectField = new ObjectField("Scene Object") { objectType = typeof(GameObject), allowSceneObjects = true, value = sceneObject };
            objectField.RegisterValueChangedCallback(evt =>
            {
                sceneObject = evt.newValue as GameObject;
                RefreshStatus(statusContainer);
                RefreshAnalyzeButton(analyzeButtonContainer, resultsContainer);
            });
            root.Add(objectField);
            root.Add(statusContainer);

            // Sigue la selección de la escena mientras la herramienta esté abierta, sin bloquear
            // el arrastre manual (igual que Advanced Mesh Combiner reacciona a la selección).
            void SyncFromSelection()
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject != sceneObject)
                {
                    sceneObject = Selection.activeGameObject;
                    objectField.SetValueWithoutNotify(sceneObject);
                    RefreshStatus(statusContainer);
                    RefreshAnalyzeButton(analyzeButtonContainer, resultsContainer);
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += SyncFromSelection);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= SyncFromSelection);

            root.Add(new Label("Triangle Detection Settings") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });

            var minAreaField = new FloatField("Min Area Threshold") { value = minAreaThreshold };
            minAreaField.RegisterValueChangedCallback(evt => minAreaThreshold = evt.newValue);
            root.Add(minAreaField);

            var maxEdgeField = new FloatField("Max Edge Ratio Threshold") { value = maxEdgeRatioThreshold };
            maxEdgeField.RegisterValueChangedCallback(evt => maxEdgeRatioThreshold = evt.newValue);
            root.Add(maxEdgeField);

            root.Add(new Label("Set Thresholds Based on Distance") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });

            void AddDistanceButton(string label, float distance)
            {
                root.Add(new MTUIActionButton(label, () =>
                {
                    SetThresholdsForDistance(distance);
                    minAreaField.SetValueWithoutNotify(minAreaThreshold);
                    maxEdgeField.SetValueWithoutNotify(maxEdgeRatioThreshold);
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));
            }

            AddDistanceButton("1 cm (Close)", 0.01f);
            AddDistanceButton("10 cm (Near)", 0.1f);
            AddDistanceButton("1 m (Mid-range)", 1f);
            AddDistanceButton("10 m (Far)", 10f);
            AddDistanceButton("100 m (Very Far)", 100f);

            root.Add(analyzeButtonContainer);
            root.Add(resultsContainer);

            RefreshStatus(statusContainer);
            RefreshAnalyzeButton(analyzeButtonContainer, resultsContainer);
            RefreshResults(resultsContainer);

            return root;
        }

        private static void RefreshStatus(VisualElement container)
        {
            container.Clear();

            if (sceneObject == null)
            {
                container.Add(new HelpBox("Arrastra un objeto de la escena con MeshFilter para poder analizarlo.", HelpBoxMessageType.Info));
            }
            else if (sceneObject.GetComponent<MeshFilter>() == null || sceneObject.GetComponent<MeshFilter>().sharedMesh == null)
            {
                container.Add(new HelpBox("El objeto seleccionado no tiene un MeshFilter con una Mesh asignada.", HelpBoxMessageType.Warning));
            }
        }

        private static void RefreshAnalyzeButton(VisualElement container, VisualElement resultsContainer)
        {
            container.Clear();

            bool canAnalyze = sceneObject != null && sceneObject.GetComponent<MeshFilter>() != null && sceneObject.GetComponent<MeshFilter>().sharedMesh != null;
            var analyzeButton = new MTUIActionButton("Analyze", () =>
            {
                Analyze();
                RefreshResults(resultsContainer);
            });
            analyzeButton.SetAvailable(canAnalyze);
            container.Add(analyzeButton);
        }

        private static void RefreshResults(VisualElement container)
        {
            container.Clear();

            if (problematicTriangles.Count > 0)
            {
                container.Add(new Label($"Found {problematicTriangles.Count} problematic triangles") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });

                var scroll = new ScrollView { style = { maxHeight = 240 } };
                for (int i = 0; i < problematicTriangles.Count; i++)
                {
                    int index = i;
                    scroll.Add(new MTUIActionButton($"Triangle {i + 1}", () =>
                    {
                        selectedTriangleIndex = index;
                        FocusOnTriangle(problematicTriangles[index]);
                    }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));
                }
                container.Add(scroll);
            }
            else
            {
                container.Add(new HelpBox("No problematic triangles found or scan not performed.", HelpBoxMessageType.None));
            }
        }

        private static void SetThresholdsForDistance(float distance)
        {
            minAreaThreshold = Mathf.Lerp(0.000001f, 0.01f, Mathf.Log10(distance + 1) / 2f);
            maxEdgeRatioThreshold = Mathf.Lerp(50f, 2f, Mathf.Log10(distance + 1) / 2f);
        }

        private static void Analyze()
        {
            problematicTriangles.Clear();
            selectedTriangleIndex = -1;

            if (sceneObject == null)
            {
                Debug.LogWarning("No object selected! Please drag a scene object.");
                return;
            }

            MeshFilter meshFilter = sceneObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning("Selected object does not have a MeshFilter or a Mesh.");
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v1 = vertices[triangles[i]];
                Vector3 v2 = vertices[triangles[i + 1]];
                Vector3 v3 = vertices[triangles[i + 2]];

                if (IsMicroTriangle(v1, v2, v3))
                {
                    problematicTriangles.Add((sceneObject.transform.TransformPoint(v1),
                                              sceneObject.transform.TransformPoint(v2),
                                              sceneObject.transform.TransformPoint(v3)));
                }
            }

            Debug.Log(problematicTriangles.Count > 0
                ? $"Found {problematicTriangles.Count} problematic triangles."
                : "No problematic triangles detected.");
        }

        private static bool IsMicroTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            float edge1 = Vector3.Distance(v1, v2);
            float edge2 = Vector3.Distance(v2, v3);
            float edge3 = Vector3.Distance(v3, v1);

            float perimeter = edge1 + edge2 + edge3;
            float semiPerimeter = perimeter / 2;
            float area = Mathf.Sqrt(semiPerimeter * (semiPerimeter - edge1) * (semiPerimeter - edge2) * (semiPerimeter - edge3));

            return area < minAreaThreshold && Mathf.Max(edge1, edge2, edge3) > maxEdgeRatioThreshold * Mathf.Min(edge1, edge2, edge3);
        }

        private static void FocusOnTriangle((Vector3 v1, Vector3 v2, Vector3 v3) triangle)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("No Scene view is open. Open a Scene view and try again.");
                return;
            }

            Vector3 center = (triangle.v1 + triangle.v2 + triangle.v3) / 3f;
            sceneView.pivot = center;
            sceneView.size = 1f;
            sceneView.Repaint();
            SceneView.RepaintAll();
        }

        public static void OnSceneGUI(SceneView sceneView)
        {
            if (selectedTriangleIndex >= 0 && selectedTriangleIndex < problematicTriangles.Count)
            {
                var tri = problematicTriangles[selectedTriangleIndex];
                Handles.color = Color.red;
                Handles.DrawAAPolyLine(5f, tri.v1, tri.v2, tri.v3, tri.v1);
                Handles.SphereHandleCap(0, tri.v1, Quaternion.identity, 0.001f, EventType.Repaint);
                Handles.SphereHandleCap(0, tri.v2, Quaternion.identity, 0.001f, EventType.Repaint);
                Handles.SphereHandleCap(0, tri.v3, Quaternion.identity, 0.001f, EventType.Repaint);
            }
        }

        public static void EnableSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        public static void DisableSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
    }
}
