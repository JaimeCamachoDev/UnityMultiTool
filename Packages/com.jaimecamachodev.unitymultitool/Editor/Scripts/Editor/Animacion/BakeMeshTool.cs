using UnityEngine;
using UnityEditor;
using System.IO;

namespace VZ_Optizone
{
    public static class BakeMeshTool
    {
        // Variables para el skinned mesh, nombre de la malla y la carpeta de destino
        private static SkinnedMeshRenderer skinnedMeshRenderer;
        private static string outputMeshName = "BakedMesh";
        private static Object destinationFolder;

        // Método para dibujar la herramienta
        public static void DrawTool()
        {
            GUILayout.Label("VZ Optizone - Bake Skinned Mesh Pose", EditorStyles.boldLabel);

            // Campo para seleccionar el SkinnedMeshRenderer
            skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned Mesh Renderer", skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);

            // Campo para seleccionar la carpeta de destino
            destinationFolder = EditorGUILayout.ObjectField("Destination Folder", destinationFolder, typeof(Object), false);

            // Campo para ingresar el nombre de la malla horneada
            outputMeshName = EditorGUILayout.TextField("Mesh Name", outputMeshName);

            GUILayout.Space(10);

            // Botón para hornear la malla
            if (GUILayout.Button("Bake Pose to Mesh"))
            {
                if (skinnedMeshRenderer != null && destinationFolder != null)
                {
                    BakeMesh();
                }
                else
                {
                    Debug.LogWarning("Please select a SkinnedMeshRenderer and a destination folder.");
                }
            }
        }

        // Método para hornear la malla y guardarla en la carpeta especificada
        private static void BakeMesh()
        {
            // Crear una nueva malla basada en la pose actual del SkinnedMeshRenderer
            Mesh bakedMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(bakedMesh); // Hornear la malla actual

            // Obtener la ruta de la carpeta de destino
            string folderPath = AssetDatabase.GetAssetPath(destinationFolder);
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError("Invalid destination folder.");
                return;
            }

            // Definir la ruta y nombre del nuevo archivo de malla
            string meshPath = Path.Combine(folderPath, outputMeshName + ".asset");

            // Guardar la nueva malla como un asset
            AssetDatabase.CreateAsset(bakedMesh, meshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Baked mesh saved at: {meshPath}");
        }
    }
}

