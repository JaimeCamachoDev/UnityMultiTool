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

                var selectionPanel = new MTUIPanel("Objeto seleccionado");
                selectionPanel.Add(new MTUIInfoLabel($"{selectedObject.name} · {vertices.Length} vértices"));
                contentContainer.Add(selectionPanel);

                var queryPanel = new MTUIPanel("Consultar vértice") { style = { marginTop = 10 } };

                var vertexIdField = new IntegerField("Vertex ID") { value = vertexID };
                vertexIdField.RegisterValueChangedCallback(evt => vertexID = evt.newValue);
                queryPanel.Add(vertexIdField);

                var resultBox = new MTUIPanel(null) { style = { marginTop = 8 } };
                var positionLabel = new MTUIInfoLabel { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 0 } };
                resultBox.Add(positionLabel);

                void RefreshPositionLabel()
                {
                    positionLabel.text = vertexID >= 0 && vertices != null && vertexID < vertices.Length
                        ? $"Vertex {vertexID} — posición mundial\nX: {vertexWorldPosition.x:F4}   Y: {vertexWorldPosition.y:F4}   Z: {vertexWorldPosition.z:F4}"
                        : "Pulsa \"Display Vertex ID\" para consultar su posición mundial.";
                }

                var displayButton = new MTUIActionButton("Display Vertex ID", () =>
                {
                    DisplayVertexID();
                    RefreshPositionLabel();
                });
                displayButton.style.marginTop = 6;
                queryPanel.Add(displayButton);
                queryPanel.Add(resultBox);
                contentContainer.Add(queryPanel);

                RefreshPositionLabel();
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
