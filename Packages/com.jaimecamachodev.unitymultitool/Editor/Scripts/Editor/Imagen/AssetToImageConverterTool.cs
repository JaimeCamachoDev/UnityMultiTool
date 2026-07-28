using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Textures
{
    public static class AssetToImageConverterTool
    {
        private static readonly string[] formats = { "PNG", "JPG" };

        private static Texture2D selectedTexture;
        private static string outputName = "ConvertedImage";
        private static int selectedFormatIndex;
        private static bool deleteOriginalAsset;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Convert Asset to Image") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Convierte una textura del proyecto a un archivo de imagen (PNG/JPG) en disco, o crea un asset de Texture2D a partir de una imagen existente.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var textureField = new ObjectField("Asset de textura") { objectType = typeof(Texture2D), allowSceneObjects = false, value = selectedTexture };
                textureField.RegisterValueChangedCallback(evt =>
                {
                    selectedTexture = evt.newValue as Texture2D;
                    RefreshContent();
                });
                sourcePanel.Add(textureField);
                contentContainer.Add(sourcePanel);

                if (selectedTexture == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona una textura válida para continuar.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                    return;
                }

                var exportPanel = new MTUIPanel("Exportar a imagen") { style = { marginTop = 10 } };

                var formatField = new PopupField<string>("Formato de salida", new List<string>(formats), selectedFormatIndex);
                formatField.RegisterValueChangedCallback(evt => selectedFormatIndex = formatField.index);
                exportPanel.Add(formatField);

                var nameField = new TextField("Nombre del archivo de salida") { value = outputName };
                nameField.RegisterValueChangedCallback(evt => outputName = evt.newValue);
                exportPanel.Add(nameField);

                var deleteToggle = new Toggle("Eliminar asset original") { value = deleteOriginalAsset };
                deleteToggle.RegisterValueChangedCallback(evt => deleteOriginalAsset = evt.newValue);
                exportPanel.Add(deleteToggle);

                var convertButton = new MTUIActionButton("Convertir y guardar", ConvertAndSaveImage);
                convertButton.style.marginTop = 10;
                convertButton.SetAvailable(!string.IsNullOrEmpty(outputName));
                exportPanel.Add(convertButton);

                contentContainer.Add(exportPanel);

                var importPanel = new MTUIPanel("Convertir imagen a asset") { style = { marginTop = 10 } };
                importPanel.Add(new MTUIInfoLabel("Crea un asset Texture2D a partir de la textura seleccionada arriba."));

                var convertBackButton = new MTUIActionButton("Convertir imagen a asset", ConvertImageToAsset);
                convertBackButton.style.marginTop = 6;
                importPanel.Add(convertBackButton);

                contentContainer.Add(importPanel);
            }

            RefreshContent();
            return root;
        }

        private static void ConvertAndSaveImage()
        {
            if (selectedTexture == null)
            {
                Debug.LogError("No se ha seleccionado ninguna textura.");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("No se pudo obtener la ruta del asset.");
                return;
            }

            string directory = Path.GetDirectoryName(assetPath);
            string outputPath = Path.Combine(directory, outputName);

            string extension = formats[selectedFormatIndex] == "PNG" ? ".png" : ".jpg";
            outputPath += extension;

            byte[] bytes = formats[selectedFormatIndex] == "PNG" ? selectedTexture.EncodeToPNG() : selectedTexture.EncodeToJPG();

            File.WriteAllBytes(outputPath, bytes);
            Debug.Log($"La imagen ha sido guardada en {outputPath}");

            if (deleteOriginalAsset)
            {
                AssetDatabase.DeleteAsset(assetPath);
                Debug.Log("El archivo .asset original ha sido eliminado.");
            }

            AssetDatabase.Refresh();
        }

        private static void ConvertImageToAsset()
        {
            if (selectedTexture == null)
            {
                Debug.LogError("No se ha seleccionado ninguna imagen.");
                return;
            }

            string imagePath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(imagePath))
            {
                Debug.LogError("No se pudo obtener la ruta de la imagen.");
                return;
            }

            string directory = Path.GetDirectoryName(imagePath);
            string assetName = Path.GetFileNameWithoutExtension(imagePath);
            string outputAssetPath = Path.Combine(directory, assetName + ".asset");

            var newTexture = new Texture2D(selectedTexture.width, selectedTexture.height);
            EditorUtility.CopySerialized(selectedTexture, newTexture);

            AssetDatabase.CreateAsset(newTexture, outputAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"La imagen ha sido convertida a un archivo .asset en {outputAssetPath}");
        }
    }
}
