using System.Collections.Generic;
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
    public static class ImageChannelSplitterTool
    {
        private static readonly string[] formats = { "PNG", "JPG" };

        private static Texture2D selectedTexture;
        private static string outputName = "Channels_Extracted";
        private static int selectedFormatIndex;
        private static bool redChannel = true;
        private static string redSuffix = "_r_Roughness";
        private static bool greenChannel = true;
        private static string greenSuffix = "_g_Metallic";
        private static bool blueChannel = true;
        private static string blueSuffix = "_b_AO";
        private static bool alphaChannel = true;
        private static string alphaSuffix = "_a_Emission";

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Split Texture into Channels") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Extrae los canales R, G, B y A de una textura como imágenes independientes, usando ffmpeg.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var textureField = new ObjectField("Textura") { objectType = typeof(Texture2D), allowSceneObjects = false, value = selectedTexture };
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

                var optionsPanel = new MTUIPanel("Opciones de salida") { style = { marginTop = 10 } };

                var formatField = new PopupField<string>("Formato de salida", new List<string>(formats), selectedFormatIndex);
                formatField.RegisterValueChangedCallback(evt => selectedFormatIndex = formatField.index);
                optionsPanel.Add(formatField);

                var nameField = new TextField("Nombre de archivo base") { value = outputName };
                nameField.RegisterValueChangedCallback(evt => outputName = evt.newValue);
                optionsPanel.Add(nameField);

                contentContainer.Add(optionsPanel);

                var channelsPanel = new MTUIPanel("Canales a extraer") { style = { marginTop = 10 } };
                channelsPanel.Add(BuildChannelRow("Rojo", redChannel, v => redChannel = v, redSuffix, v => redSuffix = v));
                channelsPanel.Add(BuildChannelRow("Verde", greenChannel, v => greenChannel = v, greenSuffix, v => greenSuffix = v));
                channelsPanel.Add(BuildChannelRow("Azul", blueChannel, v => blueChannel = v, blueSuffix, v => blueSuffix = v));
                channelsPanel.Add(BuildChannelRow("Alpha", alphaChannel, v => alphaChannel = v, alphaSuffix, v => alphaSuffix = v));
                contentContainer.Add(channelsPanel);

                bool canExtract = redChannel || greenChannel || blueChannel || alphaChannel;
                if (!canExtract)
                {
                    contentContainer.Add(new HelpBox("Activa al menos un canal para extraer.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var extractButton = new MTUIActionButton("Extract Selected Image Channels", ExtractImageChannels);
                extractButton.style.marginTop = 10;
                extractButton.SetAvailable(canExtract);
                contentContainer.Add(extractButton);
            }

            RefreshContent();
            return root;
        }

        private static VisualElement BuildChannelRow(string label, bool enabled, System.Action<bool> setEnabled, string suffix, System.Action<string> setSuffix)
        {
            var row = new VisualElement { style = { marginBottom = 4 } };

            var toggle = new Toggle($"Extraer {label}") { value = enabled };
            row.Add(toggle);

            var suffixField = new TextField($"Sufijo {label}") { value = suffix, style = { marginLeft = 15, display = enabled ? DisplayStyle.Flex : DisplayStyle.None } };
            suffixField.RegisterValueChangedCallback(evt => setSuffix(evt.newValue));
            row.Add(suffixField);

            toggle.RegisterValueChangedCallback(evt =>
            {
                setEnabled(evt.newValue);
                suffixField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            return row;
        }

        private static void ExtractImageChannels()
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("No se pudo obtener la ruta del asset.");
                return;
            }

            string directory = Path.GetDirectoryName(assetPath);
            string extension = formats[selectedFormatIndex] == "PNG" ? ".png" : ".jpg";

            if (redChannel)
            {
                ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{redSuffix}{extension}"), "r");
            }
            if (greenChannel)
            {
                ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{greenSuffix}{extension}"), "g");
            }
            if (blueChannel)
            {
                ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{blueSuffix}{extension}"), "b");
            }
            if (alphaChannel)
            {
                ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{alphaSuffix}{extension}"), "a");
            }

            AssetDatabase.Refresh();
        }

        private static void ExecuteFFmpegCommand(string inputFilePath, string outputFilePath, string channel)
        {
            string ffmpegPath = FfmpegLocator.ExecutablePath;
            string command = $"-i \"{inputFilePath}\" -filter_complex \"[0:v]extractplanes={channel}[{channel}]\" -map \"[{channel}]\" \"{outputFilePath}\"";

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
        }
    }
}
