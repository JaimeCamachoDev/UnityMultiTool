using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
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

            var contentContainer = new VisualElement { style = { marginTop = 6 } };

            void SetSelectedObject(GameObject go)
            {
                selectedObject = go;

                // Solo releer la malla y sus vértices cuando cambia el objeto seleccionado
                MeshFilter changedMeshFilter = selectedObject != null ? selectedObject.GetComponent<MeshFilter>() : null;
                selectedMesh = changedMeshFilter != null ? changedMeshFilter.sharedMesh : null;
                vertices = selectedMesh != null ? selectedMesh.vertices : null;
            }

            var objectField = new ObjectField("GameObject") { objectType = typeof(GameObject), allowSceneObjects = true, value = selectedObject };
            objectField.RegisterValueChangedCallback(evt =>
            {
                SetSelectedObject(evt.newValue as GameObject);
                RefreshContent(contentContainer);
            });
            root.Add(objectField);

            // Sigue la selección de la escena mientras la herramienta esté abierta, sin bloquear
            // el arrastre manual (igual que Advanced Mesh Combiner reacciona a la selección).
            void SyncFromSelection()
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject != selectedObject)
                {
                    SetSelectedObject(Selection.activeGameObject);
                    objectField.SetValueWithoutNotify(selectedObject);
                    RefreshContent(contentContainer);
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += SyncFromSelection);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= SyncFromSelection);

            root.Add(contentContainer);
            RefreshContent(contentContainer);

            return root;
        }

        private static void RefreshContent(VisualElement container)
        {
            container.Clear();

            if (selectedObject == null)
            {
                container.Add(new HelpBox("Selecciona un objeto de la escena, o arrástralo arriba, para mostrar sus vértices.", HelpBoxMessageType.Info));
                return;
            }

            if (selectedMesh == null)
            {
                container.Add(new HelpBox("Selected GameObject does not have a MeshFilter with a mesh.", HelpBoxMessageType.Warning));
                return;
            }

            var vertexIdField = new IntegerField("Enter Vertex ID to Display:") { value = vertexID };
            vertexIdField.RegisterValueChangedCallback(evt => vertexID = evt.newValue);
            container.Add(vertexIdField);

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
            container.Add(displayButton);

            RefreshPositionLabel();
            container.Add(positionLabel);
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
