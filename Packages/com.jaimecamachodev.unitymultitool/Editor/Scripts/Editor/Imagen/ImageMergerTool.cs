using System.Diagnostics;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace JaimeCamachoDev.Multitool.Textures
{
    public static class ImageChannelMergerTool
    {
        private static Texture2D redChannelTexture;
        private static Texture2D greenChannelTexture;
        private static Texture2D blueChannelTexture;
        private static Texture2D alphaChannelTexture;
        private static string outputImageName = "Channels_Combined";
        private static DefaultAsset outputFolder;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Merge Textures into One") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Combina cuatro texturas de un solo canal en una única imagen RGBA, usando ffmpeg.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var channelsPanel = new MTUIPanel("Canales de origen");

                var redField = new ObjectField("Textura del canal Rojo") { objectType = typeof(Texture2D), allowSceneObjects = false, value = redChannelTexture };
                redField.RegisterValueChangedCallback(evt => { redChannelTexture = evt.newValue as Texture2D; RefreshContent(); });
                channelsPanel.Add(redField);

                var greenField = new ObjectField("Textura del canal Verde") { objectType = typeof(Texture2D), allowSceneObjects = false, value = greenChannelTexture };
                greenField.RegisterValueChangedCallback(evt => { greenChannelTexture = evt.newValue as Texture2D; RefreshContent(); });
                channelsPanel.Add(greenField);

                var blueField = new ObjectField("Textura del canal Azul") { objectType = typeof(Texture2D), allowSceneObjects = false, value = blueChannelTexture };
                blueField.RegisterValueChangedCallback(evt => { blueChannelTexture = evt.newValue as Texture2D; RefreshContent(); });
                channelsPanel.Add(blueField);

                var alphaField = new ObjectField("Textura del canal Alpha") { objectType = typeof(Texture2D), allowSceneObjects = false, value = alphaChannelTexture };
                alphaField.RegisterValueChangedCallback(evt => { alphaChannelTexture = evt.newValue as Texture2D; RefreshContent(); });
                channelsPanel.Add(alphaField);

                contentContainer.Add(channelsPanel);

                var outputPanel = new MTUIPanel("Salida") { style = { marginTop = 10 } };

                var nameField = new TextField("Nombre de la imagen de salida") { value = outputImageName };
                nameField.RegisterValueChangedCallback(evt => outputImageName = evt.newValue);
                outputPanel.Add(nameField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder };
                folderField.RegisterValueChangedCallback(evt => { outputFolder = evt.newValue as DefaultAsset; RefreshContent(); });
                outputPanel.Add(folderField);

                contentContainer.Add(outputPanel);

                bool hasAllTextures = redChannelTexture != null && greenChannelTexture != null && blueChannelTexture != null && alphaChannelTexture != null;
                bool canMerge = hasAllTextures && outputFolder != null;

                if (!hasAllTextures)
                {
                    contentContainer.Add(new HelpBox("Selecciona las cuatro texturas (Red, Green, Blue, Alpha) antes de combinar.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }
                else if (outputFolder == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona una carpeta de destino para guardar la imagen combinada.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var mergeButton = new MTUIActionButton("Merge Channels", MergeImagesChannels);
                mergeButton.style.marginTop = 10;
                mergeButton.SetAvailable(canMerge);
                contentContainer.Add(mergeButton);
            }

            RefreshContent();
            return root;
        }

        private static void MergeImagesChannels()
        {
            string redImagePath = AssetDatabase.GetAssetPath(redChannelTexture);
            string greenImagePath = AssetDatabase.GetAssetPath(greenChannelTexture);
            string blueImagePath = AssetDatabase.GetAssetPath(blueChannelTexture);
            string alphaImagePath = AssetDatabase.GetAssetPath(alphaChannelTexture);

            string outputFolderPath = AssetDatabase.GetAssetPath(outputFolder);
            if (!Directory.Exists(outputFolderPath))
            {
                Debug.LogError("La carpeta de destino seleccionada no es válida.");
                return;
            }

            string outputPath = Path.Combine(outputFolderPath, $"{outputImageName}.png");

            string ffmpegPath = FfmpegLocator.ExecutablePath;

            string command = $"-i \"{greenImagePath}\" -i \"{blueImagePath}\" -i \"{redImagePath}\" -i \"{alphaImagePath}\" -filter_complex " +
                             "\"[0:v][1:v][2:v]mergeplanes=0x001020:format=gbrp10le[vrgb];[vrgb][3:v]alphamerge\" -c:v png \"" + outputPath + "\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();

            AssetDatabase.Refresh();
            Debug.Log($"Imagen combinada guardada en: {outputPath}");
        }
    }
}
