using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
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

            var statusContainer = new VisualElement();
            var generateButton = new MTUIActionButton("Generar UV2", GenerateUV2);
            generateButton.style.marginTop = 6;

            // Campo para arrastrar y soltar la malla
            var meshField = new ObjectField("Malla para Generar UV2") { objectType = typeof(Mesh), allowSceneObjects = false, value = selectedMesh };
            meshField.RegisterValueChangedCallback(evt =>
            {
                selectedMesh = evt.newValue as Mesh;
                RefreshStatus(statusContainer);
                generateButton.SetAvailable(selectedMesh != null);
            });
            root.Add(meshField);
            root.Add(statusContainer);
            root.Add(generateButton);

            RefreshStatus(statusContainer);
            generateButton.SetAvailable(selectedMesh != null);

            return root;
        }

        private static void RefreshStatus(VisualElement container)
        {
            container.Clear();

            if (selectedMesh == null)
            {
                container.Add(new HelpBox("Arrastra una Mesh para poder generar su UV2.", HelpBoxMessageType.Info));
            }
            else if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(selectedMesh)) is ModelImporter)
            {
                container.Add(new HelpBox("Esta malla proviene de un modelo importado (FBX/OBJ/etc). Los cambios se perderán al reimportar el modelo; considera duplicar la malla como un asset independiente antes de generar el UV2.", HelpBoxMessageType.Warning));
            }
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
