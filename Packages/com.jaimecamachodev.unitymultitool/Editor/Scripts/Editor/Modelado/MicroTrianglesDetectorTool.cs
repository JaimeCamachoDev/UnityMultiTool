using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VZ_Optizone
{
    public static class MicroTrianglesDetectorTool
    {
        private static GameObject sceneObject;
        private static List<(Vector3 v1, Vector3 v2, Vector3 v3)> problematicTriangles = new List<(Vector3, Vector3, Vector3)>();
        private static int selectedTriangleIndex = -1;

        private static float minAreaThreshold = 0.01f;
        private static float maxEdgeRatioThreshold = 10f;

        public static void DrawTool()
        {
            GUILayout.Label("Micro Triangle Detector", EditorStyles.boldLabel);

            sceneObject = (GameObject)EditorGUILayout.ObjectField("Scene Object", sceneObject, typeof(GameObject), true);

            GUILayout.Label("Triangle Detection Settings", EditorStyles.boldLabel);
            minAreaThreshold = EditorGUILayout.FloatField("Min Area Threshold", minAreaThreshold);
            maxEdgeRatioThreshold = EditorGUILayout.FloatField("Max Edge Ratio Threshold", maxEdgeRatioThreshold);

            GUILayout.Label("Set Thresholds Based on Distance", EditorStyles.boldLabel);
            if (GUILayout.Button("1 cm (Close)")) SetThresholdsForDistance(0.01f);
            if (GUILayout.Button("10 cm (Near)")) SetThresholdsForDistance(0.1f);
            if (GUILayout.Button("1 m (Mid-range)")) SetThresholdsForDistance(1f);
            if (GUILayout.Button("10 m (Far)")) SetThresholdsForDistance(10f);
            if (GUILayout.Button("100 m (Very Far)")) SetThresholdsForDistance(100f);

            GUILayout.Space(10);
            if (GUILayout.Button("Analyze")) Analyze();

            if (problematicTriangles.Count > 0)
            {
                GUILayout.Label($"Found {problematicTriangles.Count} problematic triangles", EditorStyles.boldLabel);
                for (int i = 0; i < problematicTriangles.Count; i++)
                {
                    if (GUILayout.Button($"Triangle {i + 1}"))
                    {
                        selectedTriangleIndex = i;
                        FocusOnTriangle(problematicTriangles[i]);
                    }
                }
            }
            else
            {
                GUILayout.Label("No problematic triangles found or scan not performed.", EditorStyles.helpBox);
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
            Vector3 center = (triangle.v1 + triangle.v2 + triangle.v3) / 3f;
            SceneView.lastActiveSceneView.pivot = center;
            SceneView.lastActiveSceneView.size = 1f;
            SceneView.lastActiveSceneView.Repaint();
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
