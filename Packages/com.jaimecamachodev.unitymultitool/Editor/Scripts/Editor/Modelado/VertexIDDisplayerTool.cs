using UnityEngine;
using UnityEditor;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class VertexIDDisplayerTool
    {
        // Variables para almacenar el objeto seleccionado, la malla, y la posición del vértice
        private static GameObject selectedObject;
        private static Mesh selectedMesh;
        private static Vector3[] vertices;
        private static int vertexID = -1;
        private static Vector3 vertexWorldPosition;

        // Método para dibujar la herramienta en el Editor
        public static void DrawTool()
        {
            GUILayout.Label("Vertex ID Displayer", EditorStyles.boldLabel);

            // Campo para seleccionar el GameObject
            GameObject newSelectedObject = (GameObject)EditorGUILayout.ObjectField("GameObject", selectedObject, typeof(GameObject), true);

            // Solo releer la malla y sus vértices cuando cambia el objeto seleccionado
            if (newSelectedObject != selectedObject)
            {
                selectedObject = newSelectedObject;

                MeshFilter changedMeshFilter = selectedObject != null ? selectedObject.GetComponent<MeshFilter>() : null;
                selectedMesh = changedMeshFilter != null ? changedMeshFilter.sharedMesh : null;
                vertices = selectedMesh != null ? selectedMesh.vertices : null;
            }

            if (selectedObject != null)
            {
                if (selectedMesh != null)
                {
                    // Campo para ingresar el ID del vértice
                    GUILayout.Label("Enter Vertex ID to Display:");
                    vertexID = EditorGUILayout.IntField(vertexID);

                    // Botón para mostrar el vértice seleccionado
                    if (GUILayout.Button("Display Vertex ID"))
                    {
                        DisplayVertexID();
                    }

                    GUILayout.Space(10);

                    // Mostrar las coordenadas del vértice en el mundo
                    if (vertexID >= 0 && vertexID < vertices.Length)
                    {
                        GUILayout.Label($"Vertex {vertexID} World Position:");
                        GUILayout.Label($"X: {vertexWorldPosition.x}");
                        GUILayout.Label($"Y: {vertexWorldPosition.y}");
                        GUILayout.Label($"Z: {vertexWorldPosition.z}");
                    }
                }
                else
                {
                    GUILayout.Label("Selected GameObject does not have a MeshFilter with a mesh.", EditorStyles.helpBox);
                }
            }
        }

        // Método para mostrar la información del vértice seleccionado
        private static void DisplayVertexID()
        {
            if (vertexID >= 0 && vertexID < vertices.Length)
            {
                Transform objectTransform = selectedObject.transform;
                vertexWorldPosition = objectTransform.TransformPoint(vertices[vertexID]);
                SceneView.RepaintAll();
            }
            else
            {
                Debug.LogError("Vertex ID out of range.");
            }
        }

        // Método para manejar la escena y dibujar el vértice
        public static void OnSceneGUI(SceneView sceneView)
        {
            if (selectedMesh == null || vertices == null || vertexID < 0 || vertexID >= vertices.Length)
            {
                return;
            }

            Handles.color = Color.green;

            // Dibujar el vértice especificado por el ID
            Handles.Label(vertexWorldPosition, vertexID.ToString());
            Handles.SphereHandleCap(0, vertexWorldPosition, Quaternion.identity, 0.05f, EventType.Repaint);
        }

        // Métodos para gestionar el evento de la escena
        public static void EnableSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void DisableSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
    }
}
