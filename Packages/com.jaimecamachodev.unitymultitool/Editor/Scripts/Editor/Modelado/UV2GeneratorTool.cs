using UnityEngine;
using UnityEditor;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class UV2GeneratorTool
    {
        private static Mesh selectedMesh;

        public static void DrawTool()
        {
            GUILayout.Label("Generar UV2 para Lightmapping", EditorStyles.boldLabel);

            // Campo para arrastrar y soltar la malla
            selectedMesh = (Mesh)EditorGUILayout.ObjectField("Malla para Generar UV2", selectedMesh, typeof(Mesh), false);

            if (selectedMesh == null)
            {
                EditorGUILayout.HelpBox("Arrastra una Mesh para poder generar su UV2.", MessageType.Info);
            }
            else if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(selectedMesh)) is ModelImporter)
            {
                EditorGUILayout.HelpBox("Esta malla proviene de un modelo importado (FBX/OBJ/etc). Los cambios se perderán al reimportar el modelo; considera duplicar la malla como un asset independiente antes de generar el UV2.", MessageType.Warning);
            }

            // Botón para generar el UV2
            using (new EditorGUI.DisabledScope(selectedMesh == null))
            {
                if (GUILayout.Button("Generar UV2"))
                {
                    GenerateUV2();
                }
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
