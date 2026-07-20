using UnityEngine;
using UnityEditor;

namespace VZ_Optizone
{
    public static class UV2GeneratorTool
    {
        private static Mesh selectedMesh;

        public static void DrawTool()
        {
            GUILayout.Label("Generar UV2 para Lightmapping", EditorStyles.boldLabel);

            // Campo para arrastrar y soltar la malla
            selectedMesh = (Mesh)EditorGUILayout.ObjectField("Malla para Generar UV2", selectedMesh, typeof(Mesh), false);

            // Botón para generar el UV2
            if (GUILayout.Button("Generar UV2"))
            {
                if (selectedMesh != null)
                {
                    GenerateUV2();
                }
                else
                {
                    Debug.LogWarning("Por favor selecciona una Malla.");
                }
            }
        }

        private static void GenerateUV2()
        {
            // Generar UV2 para la malla seleccionada
            Unwrapping.GenerateSecondaryUVSet(selectedMesh);

            // Guardar los cambios en la malla
            string path = AssetDatabase.GetAssetPath(selectedMesh);
            if (!string.IsNullOrEmpty(path))
            {
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
