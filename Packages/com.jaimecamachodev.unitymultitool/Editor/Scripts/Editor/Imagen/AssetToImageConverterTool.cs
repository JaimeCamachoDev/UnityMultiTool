using UnityEngine;
using UnityEditor;
using System.IO;

namespace VZOptizone
{
    public static class AssetToImageConverterTool
    {
        private static Texture2D selectedTexture;
        private static string outputName = "ConvertedImage";
        private static string[] formats = { "PNG", "JPG" };
        private static int selectedFormatIndex = 0;
        private static bool deleteOriginalAsset = false; // Opción para eliminar el archivo original
        private static bool convertBackToAsset = false;  // Opción para convertir de imagen a asset

        public static void DrawTool()
        {
            GUILayout.Label("Convertir Asset a Imagen o Imagen a Asset", EditorStyles.boldLabel);

            selectedTexture = (Texture2D)EditorGUILayout.ObjectField("Asset de Textura", selectedTexture, typeof(Texture2D), false);

            if (selectedTexture == null)
            {
                GUILayout.Label("Por favor, selecciona una textura válida.", EditorStyles.helpBox);
                return;
            }

            GUILayout.Space(10);

            GUILayout.Label("Opciones de salida", EditorStyles.boldLabel);

            // Selección del formato
            selectedFormatIndex = EditorGUILayout.Popup("Formato de salida", selectedFormatIndex, formats);

            // Campo de texto para el nombre del archivo de salida
            outputName = EditorGUILayout.TextField("Nombre del archivo de salida", outputName);

            // Opción para eliminar el asset original
            deleteOriginalAsset = EditorGUILayout.Toggle("Eliminar Asset Original", deleteOriginalAsset);

            GUILayout.Space(10);

            // Botón para convertir
            if (GUILayout.Button("Convertir y Guardar"))
            {
                ConvertAndSaveImage();
            }

            // Botón para convertir de imagen a .asset
            convertBackToAsset = EditorGUILayout.Toggle("Convertir Imagen a Asset", convertBackToAsset);

            if (convertBackToAsset && GUILayout.Button("Convertir Imagen a Asset"))
            {
                ConvertImageToAsset();
            }
        }

        private static void ConvertAndSaveImage()
        {
            if (selectedTexture == null)
            {
                Debug.LogError("No se ha seleccionado ninguna textura.");
                return;
            }

            // Obtener la ruta del asset original
            string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("No se pudo obtener la ruta del asset.");
                return;
            }

            // Definir la ruta de salida en la misma carpeta que el asset
            string directory = Path.GetDirectoryName(assetPath);
            string outputPath = Path.Combine(directory, outputName);

            // Agregar la extensión según el formato seleccionado
            string extension = formats[selectedFormatIndex] == "PNG" ? ".png" : ".jpg";
            outputPath += extension;

            // Codificar y guardar la imagen
            byte[] bytes;
            if (formats[selectedFormatIndex] == "PNG")
            {
                bytes = selectedTexture.EncodeToPNG();
            }
            else
            {
                bytes = selectedTexture.EncodeToJPG();
            }

            File.WriteAllBytes(outputPath, bytes);
            Debug.Log($"La imagen ha sido guardada en {outputPath}");

            // Eliminar el archivo original si está habilitada la opción
            if (deleteOriginalAsset)
            {
                AssetDatabase.DeleteAsset(assetPath);
                Debug.Log($"El archivo .asset original ha sido eliminado.");
            }

            // Refrescar el AssetDatabase para que Unity reconozca el nuevo archivo
            AssetDatabase.Refresh();
        }

        private static void ConvertImageToAsset()
        {
            if (selectedTexture == null)
            {
                Debug.LogError("No se ha seleccionado ninguna imagen.");
                return;
            }

            // Obtener la ruta de la imagen original
            string imagePath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(imagePath))
            {
                Debug.LogError("No se pudo obtener la ruta de la imagen.");
                return;
            }

            // Crear un nuevo archivo .asset a partir de la imagen
            string directory = Path.GetDirectoryName(imagePath);
            string assetName = Path.GetFileNameWithoutExtension(imagePath);
            string outputAssetPath = Path.Combine(directory, assetName + ".asset");

            Texture2D newTexture = new Texture2D(selectedTexture.width, selectedTexture.height);
            EditorUtility.CopySerialized(selectedTexture, newTexture);

            AssetDatabase.CreateAsset(newTexture, outputAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"La imagen ha sido convertida a un archivo .asset en {outputAssetPath}");
        }
    }
}