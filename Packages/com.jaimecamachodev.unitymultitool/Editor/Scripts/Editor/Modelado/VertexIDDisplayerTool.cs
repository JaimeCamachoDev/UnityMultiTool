using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

        // Construye la interfaz de la herramienta (UI Toolkit)
        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Vertex ID Displayer") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Selecciona en la escena un objeto con MeshFilter para mostrar sus vértices.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                GameObject active = Selection.activeGameObject;
                if (active != selectedObject)
                {
                    selectedObject = active;

                    // Solo releer la malla y sus vértices cuando cambia el objeto seleccionado
                    MeshFilter changedMeshFilter = selectedObject != null ? selectedObject.GetComponent<MeshFilter>() : null;
                    selectedMesh = changedMeshFilter != null ? changedMeshFilter.sharedMesh : null;
                    vertices = selectedMesh != null ? selectedMesh.vertices : null;
                }

                if (selectedObject == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona un objeto de la escena para mostrar sus vértices.", HelpBoxMessageType.Warning));
                    return;
                }

                if (selectedMesh == null)
                {
                    contentContainer.Add(new HelpBox($"'{selectedObject.name}' no tiene un MeshFilter con una Mesh asignada.", HelpBoxMessageType.Warning));
                    return;
                }

                contentContainer.Add(new MTUIInfoLabel("Objeto seleccionado: " + selectedObject.name));

                var vertexIdField = new IntegerField("Enter Vertex ID to Display:") { value = vertexID };
                vertexIdField.RegisterValueChangedCallback(evt => vertexID = evt.newValue);
                contentContainer.Add(vertexIdField);

                var positionLabel = new Label { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };

                void RefreshPositionLabel()
                {
                    positionLabel.text = vertexID >= 0 && vertices != null && vertexID < vertices.Length
                        ? $"Vertex {vertexID} World Position:\nX: {vertexWorldPosition.x}\nY: {vertexWorldPosition.y}\nZ: {vertexWorldPosition.z}"
                        : string.Empty;
                }

                var displayButton = new MTUIActionButton("Display Vertex ID", () =>
                {
                    DisplayVertexID();
                    RefreshPositionLabel();
                });
                displayButton.style.marginTop = 6;
                contentContainer.Add(displayButton);

                RefreshPositionLabel();
                contentContainer.Add(positionLabel);
            }

            // Sigue la selección de la escena mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        // Método para mostrar la información del vértice seleccionado
        private static void DisplayVertexID()
        {
            if (vertexID >= 0 && vertices != null && vertexID < vertices.Length)
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
