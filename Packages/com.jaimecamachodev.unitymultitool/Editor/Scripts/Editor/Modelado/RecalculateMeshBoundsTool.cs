using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class RecalculateMeshBoundsTool
    {
        private static MeshFilter targetMeshFilter;
        private static SkinnedMeshRenderer targetSkinnedMeshRenderer;
        private static Bounds editableBounds;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Mesh Bounds Adjuster") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Selecciona en la escena un objeto con MeshFilter o SkinnedMeshRenderer para ajustar los bounds de su Mesh.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                GameObject active = Selection.activeGameObject;
                MeshFilter meshFilter = active != null ? active.GetComponent<MeshFilter>() : null;
                SkinnedMeshRenderer skinnedMeshRenderer = meshFilter == null && active != null ? active.GetComponent<SkinnedMeshRenderer>() : null;

                if (meshFilter != targetMeshFilter || skinnedMeshRenderer != targetSkinnedMeshRenderer)
                {
                    SetTarget(meshFilter, skinnedMeshRenderer);
                }

                if (meshFilter == null && skinnedMeshRenderer == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona un objeto con MeshFilter o SkinnedMeshRenderer en la escena para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                Mesh sharedMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
                if (sharedMesh == null)
                {
                    string componentName = meshFilter != null ? "MeshFilter" : "SkinnedMeshRenderer";
                    contentContainer.Add(new HelpBox($"El {componentName} de '{active.name}' no tiene ninguna Mesh asignada.", HelpBoxMessageType.Warning));
                    return;
                }

                var boundsPanel = new MTUIPanel("Bounds de " + active.name);

                // Mostrar y permitir la edición de los bounds actuales
                var centerField = new Vector3Field("Bounds Center") { value = editableBounds.center };
                centerField.RegisterValueChangedCallback(evt => editableBounds.center = evt.newValue);
                boundsPanel.Add(centerField);

                var sizeField = new Vector3Field("Bounds Size") { value = editableBounds.size };
                sizeField.RegisterValueChangedCallback(evt => editableBounds.size = evt.newValue);
                boundsPanel.Add(sizeField);
                contentContainer.Add(boundsPanel);

                // Botón para aplicar los cambios
                var applyButton = new MTUIActionButton("Apply Bounds", ApplyBounds);
                applyButton.style.marginTop = 10;
                contentContainer.Add(applyButton);

                // Botón para resetear los bounds originales
                contentContainer.Add(new MTUIActionButton("Reset Bounds to last saved Mesh Bounds", () =>
                {
                    ResetBounds();
                    centerField.SetValueWithoutNotify(editableBounds.center);
                    sizeField.SetValueWithoutNotify(editableBounds.size);
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));
            }

            // Sigue la selección de la escena mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void ApplyBounds()
        {
            if (targetSkinnedMeshRenderer != null)
            {
                // Los bounds de un SkinnedMeshRenderer son propios de esa instancia en la
                // escena (dependen de la pose/skeleton), no del asset de Mesh compartido.
                Undo.RecordObject(targetSkinnedMeshRenderer, "Adjust Skinned Mesh Bounds");
                targetSkinnedMeshRenderer.localBounds = editableBounds;
                EditorUtility.SetDirty(targetSkinnedMeshRenderer);

                Debug.Log("Bounds updated! New size: " + targetSkinnedMeshRenderer.localBounds.size);
            }
            else if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
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
            if (targetSkinnedMeshRenderer != null)
            {
                editableBounds = targetSkinnedMeshRenderer.localBounds;
                Debug.Log("Bounds reset to original mesh bounds.");
            }
            else if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
                Debug.Log("Bounds reset to original mesh bounds.");
            }
        }

        public static void SetTarget(MeshFilter meshFilter, SkinnedMeshRenderer skinnedMeshRenderer = null)
        {
            targetMeshFilter = meshFilter;
            targetSkinnedMeshRenderer = skinnedMeshRenderer;

            if (targetSkinnedMeshRenderer != null)
            {
                editableBounds = targetSkinnedMeshRenderer.localBounds;
            }
            else if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
            {
                editableBounds = targetMeshFilter.sharedMesh.bounds;
            }
        }

        // Dibuja el Bound en la SceneView
        public static void OnSceneGUI(SceneView sceneView)
        {
            if (targetSkinnedMeshRenderer != null)
            {
                Handles.color = Color.yellow;
                Handles.DrawWireCube(targetSkinnedMeshRenderer.transform.TransformPoint(editableBounds.center),
                                     targetSkinnedMeshRenderer.transform.TransformVector(editableBounds.size));
            }
            else if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
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
