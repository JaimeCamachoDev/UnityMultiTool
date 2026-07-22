using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class RecalculateMeshBoundsTool
    {
        private static MeshFilter targetMeshFilter;
        private static Bounds editableBounds;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Mesh Bounds Adjuster") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });

            var contentContainer = new VisualElement { style = { marginTop = 6 } };

            // Campo para seleccionar el MeshFilter
            var objectField = new ObjectField("Target MeshFilter") { objectType = typeof(MeshFilter), allowSceneObjects = true, value = targetMeshFilter };

            void SyncTarget(MeshFilter newTarget)
            {
                if (newTarget != targetMeshFilter)
                {
                    SetTarget(newTarget);
                }
                objectField.SetValueWithoutNotify(targetMeshFilter);
                RefreshContent(contentContainer);
            }

            objectField.RegisterValueChangedCallback(evt => SyncTarget(evt.newValue as MeshFilter));
            root.Add(objectField);
            root.Add(contentContainer);

            // Sigue la selección de la escena mientras la herramienta esté abierta
            void OnSelectionChanged()
            {
                if (Selection.activeGameObject != null)
                {
                    MeshFilter meshFilter = Selection.activeGameObject.GetComponent<MeshFilter>();
                    if (meshFilter != null)
                    {
                        SyncTarget(meshFilter);
                    }
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += OnSelectionChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= OnSelectionChanged);

            RefreshContent(contentContainer);

            return root;
        }

        private static void RefreshContent(VisualElement container)
        {
            container.Clear();

            if (targetMeshFilter == null)
            {
                container.Add(new HelpBox("Drag a GameObject with a MeshFilter here.", HelpBoxMessageType.Info));
                return;
            }

            if (targetMeshFilter.sharedMesh == null)
            {
                container.Add(new HelpBox("The selected GameObject's MeshFilter has no Mesh assigned.", HelpBoxMessageType.Warning));
                return;
            }

            // Mostrar y permitir la edición de los bounds actuales
            var centerField = new Vector3Field("Bounds Center") { value = editableBounds.center };
            centerField.RegisterValueChangedCallback(evt => editableBounds.center = evt.newValue);
            container.Add(centerField);

            var sizeField = new Vector3Field("Bounds Size") { value = editableBounds.size };
            sizeField.RegisterValueChangedCallback(evt => editableBounds.size = evt.newValue);
            container.Add(sizeField);

            // Botón para aplicar los cambios
            container.Add(new Button(ApplyBounds) { text = "Apply Bounds", style = { marginTop = 6 } });

            // Botón para resetear los bounds originales
            container.Add(new Button(() =>
            {
                ResetBounds();
                centerField.SetValueWithoutNotify(editableBounds.center);
                sizeField.SetValueWithoutNotify(editableBounds.size);
            })
            { text = "Reset Bounds to last saved Mesh Bounds" });
        }

        private static void ApplyBounds()
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Mesh mesh = targetMeshFilter.sharedMesh;

                Undo.RecordObject(mesh, "Adjust Mesh Bounds");
                mesh.bounds = editableBounds;

                // El objeto realmente modificado es el Mesh, no el MeshFilter que lo referencia
                EditorUtility.SetDirty(mesh);
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                {
                    AssetDatabase.SaveAssets();
                }

                Debug.Log("Bounds updated! New size: " + mesh.bounds.size);
            }
        }

        private static void ResetBounds()
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
                Debug.Log("Bounds reset to original mesh bounds.");
            }
        }

        public static void SetTarget(MeshFilter meshFilter)
        {
            targetMeshFilter = meshFilter;
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
            }
        }

        // Dibuja el Bound en la SceneView
        public static void OnSceneGUI(SceneView sceneView)
        {
            if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireCube(targetMeshFilter.transform.TransformPoint(editableBounds.center),
                                     targetMeshFilter.transform.TransformVector(editableBounds.size));
            }
        }

        // Habilita los Handles en la escena
        public static void EnableSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll(); // Asegura la actualización en la vista
        }

        // Deshabilita los Handles en la escena
        public static void DisableSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
    }
}
