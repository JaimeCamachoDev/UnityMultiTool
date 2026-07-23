using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
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
            root.Add(new HelpBox("Selecciona en la escena un objeto con MeshFilter para analizar sus triángulos.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                sceneObject = Selection.activeGameObject;

                if (sceneObject == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona un objeto de la escena para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                MeshFilter filter = sceneObject.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    contentContainer.Add(new HelpBox($"'{sceneObject.name}' no tiene un MeshFilter con una Mesh asignada.", HelpBoxMessageType.Warning));
                    return;
                }

                var selectionPanel = new MTUIPanel("Objeto seleccionado");
                selectionPanel.Add(new MTUIInfoLabel(sceneObject.name));
                contentContainer.Add(selectionPanel);

                var thresholdsPanel = new MTUIPanel("Umbrales de detección") { style = { marginTop = 10 } };

                var minAreaField = new FloatField("Min Area Threshold") { value = minAreaThreshold };
                minAreaField.RegisterValueChangedCallback(evt => minAreaThreshold = evt.newValue);
                thresholdsPanel.Add(minAreaField);

                var maxEdgeField = new FloatField("Max Edge Ratio Threshold") { value = maxEdgeRatioThreshold };
                maxEdgeField.RegisterValueChangedCallback(evt => maxEdgeRatioThreshold = evt.newValue);
                thresholdsPanel.Add(maxEdgeField);

                thresholdsPanel.Add(new MTUIInfoLabel("Ajustar según distancia a cámara:") { style = { marginTop = 8, marginBottom = 4 } });

                var distanceRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };

                void AddDistanceButton(string label, float distance)
                {
                    var button = new MTUIActionButton(label, () =>
                    {
                        SetThresholdsForDistance(distance);
                        minAreaField.SetValueWithoutNotify(minAreaThreshold);
                        maxEdgeField.SetValueWithoutNotify(maxEdgeRatioThreshold);
                    }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                    button.style.marginRight = 4;
                    distanceRow.Add(button);
                }

                AddDistanceButton("1 cm", 0.01f);
                AddDistanceButton("10 cm", 0.1f);
                AddDistanceButton("1 m", 1f);
                AddDistanceButton("10 m", 10f);
                AddDistanceButton("100 m", 100f);

                thresholdsPanel.Add(distanceRow);
                contentContainer.Add(thresholdsPanel);

                var resultsPanel = new MTUIPanel("Resultados") { style = { marginTop = 10 } };
                var resultsContainer = new VisualElement();
                resultsPanel.Add(resultsContainer);

                var analyzeButton = new MTUIActionButton("Analyze", () =>
                {
                    Analyze();
                    RefreshResults(resultsContainer);
                });
                analyzeButton.style.marginTop = 10;
                contentContainer.Add(analyzeButton);

                contentContainer.Add(resultsPanel);
                RefreshResults(resultsContainer);
            }

            // Sigue la selección de la escena mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
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
