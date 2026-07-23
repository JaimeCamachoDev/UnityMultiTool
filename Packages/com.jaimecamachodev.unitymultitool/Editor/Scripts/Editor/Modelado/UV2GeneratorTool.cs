using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class UV2GeneratorTool
    {
        private static Mesh selectedMesh;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Generar UV2 para Lightmapping") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Selecciona una Mesh en el Project, o un objeto con MeshFilter en la escena, para generar su UV2.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                selectedMesh = ResolveMeshFromSelection();

                if (selectedMesh == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona una Mesh o un objeto con MeshFilter para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                var meshPanel = new MTUIPanel("Malla seleccionada");
                meshPanel.Add(new MTUIInfoLabel(selectedMesh.name));

                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(selectedMesh)) is ModelImporter)
                {
                    meshPanel.Add(new HelpBox("Esta malla proviene de un modelo importado (FBX/OBJ/etc). Los cambios se perderán al reimportar el modelo; considera duplicar la malla como un asset independiente antes de generar el UV2.", HelpBoxMessageType.Warning));
                }
                contentContainer.Add(meshPanel);

                var generateButton = new MTUIActionButton("Generar UV2", GenerateUV2);
                generateButton.style.marginTop = 10;
                contentContainer.Add(generateButton);
            }

            // Sigue la selección de la escena/proyecto mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static Mesh ResolveMeshFromSelection()
        {
            Mesh candidate = Selection.activeObject as Mesh;
            if (candidate == null && Selection.activeGameObject != null)
            {
                MeshFilter filter = Selection.activeGameObject.GetComponent<MeshFilter>();
                candidate = filter != null ? filter.sharedMesh : null;
            }

            return candidate;
        }

        private static void GenerateUV2()
        {
            // Generar UV2 para la malla seleccionada
            Undo.RecordObject(selectedMesh, "Generar UV2");
            Unwrapping.GenerateSecondaryUVSet(selectedMesh);

            // Guardar los cambios en la malla
            string path = AssetDatabase.GetAssetPath(selectedMesh);
            if (!string.IsNullOrEmpty(path))
            {
                EditorUtility.SetDirty(selectedMesh);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("UV2 generado y guardado para: " + selectedMesh.name);
            }
            else
            {
                Debug.LogError("Error al guardar UV2. Asegúrate de que la malla esté guardada como un asset.");
            }
        }
    }
}
