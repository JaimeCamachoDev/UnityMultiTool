using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace VZOptizone
{
    public static class ImageChannelSplitterTool
    {
        private static Texture2D selectedTexture;
        private static string outputName = "Channels_Extracted";
        private static string[] formats = { "PNG", "JPG" };
        private static int selectedFormatIndex = 0;
        private static bool red_Channel = true;
        private static string red_Sufix = "_r_Roughness";
        private static bool green_Channel = true;
        private static string green_Sufix = "_g_Metallic";
        private static bool blue_Channel = true;
        private static string blue_Sufix = "_b_AO";
        private static bool alpha_Channel = true;
        private static string alpha_Sufix = "_a_Emission";

        public static void DrawTool()
        {
            GUILayout.Label("Splitter - Extract Image Channels", EditorStyles.boldLabel);

            // Campo para arrastrar la textura
            selectedTexture = (Texture2D)EditorGUILayout.ObjectField("Drag and Drop Texture", selectedTexture, typeof(Texture2D), false);

            if (selectedTexture == null)
            {
                GUILayout.Label("Please select a valid texture.", EditorStyles.helpBox);
                return;
            }

            // Selección del formato
            selectedFormatIndex = EditorGUILayout.Popup("Formato de salida", selectedFormatIndex, formats);

            // Campo de texto para el nombre de salida
            outputName = EditorGUILayout.TextField("Output File Name", outputName);

            DrawChannelSelection();

            if (GUILayout.Button("Extract Selected Image Channels"))
            {
                ExtractImageChannels();
            }
        }

        private static void DrawChannelSelection()
        {
            red_Channel = EditorGUILayout.Toggle("Extract Red", red_Channel);
            if (red_Channel) red_Sufix = EditorGUILayout.TextField("Red Suffix", red_Sufix);

            green_Channel = EditorGUILayout.Toggle("Extract Green", green_Channel);
            if (green_Channel) green_Sufix = EditorGUILayout.TextField("Green Suffix", green_Sufix);

            blue_Channel = EditorGUILayout.Toggle("Extract Blue", blue_Channel);
            if (blue_Channel) blue_Sufix = EditorGUILayout.TextField("Blue Suffix", blue_Sufix);

            alpha_Channel = EditorGUILayout.Toggle("Extract Alpha", alpha_Channel);
            if (alpha_Channel) alpha_Sufix = EditorGUILayout.TextField("Alpha Suffix", alpha_Sufix);
        }

        private static void ExtractImageChannels()
        {
            // Obtener la ruta de la textura seleccionada
            string assetPath = AssetDatabase.GetAssetPath(selectedTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                UnityEngine.Debug.LogError("No se pudo obtener la ruta del asset.");
                return;
            }

            string directory = Path.GetDirectoryName(assetPath);
            string extension = formats[selectedFormatIndex] == "PNG" ? ".png" : ".jpg";

            // Extraer los canales seleccionados
            if (red_Channel) ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{red_Sufix}{extension}"), "r");
            if (green_Channel) ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{green_Sufix}{extension}"), "g");
            if (blue_Channel) ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{blue_Sufix}{extension}"), "b");
            if (alpha_Channel) ExecuteFFmpegCommand(assetPath, Path.Combine(directory, $"{outputName}{alpha_Sufix}{extension}"), "a");

            AssetDatabase.Refresh();
        }

        private static void ExecuteFFmpegCommand(string inputFilePath, string outputFilePath, string channel)
        {
            string ffmpegPath = Application.dataPath + "/VZ Optizone/Plugins/ffmpeg/bin/ffmpeg.exe";
            string command = $"-i \"{inputFilePath}\" -filter_complex \"[0:v]extractplanes={channel}[{channel}]\" -map \"[{channel}]\" \"{outputFilePath}\"";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();
        }
    }
}