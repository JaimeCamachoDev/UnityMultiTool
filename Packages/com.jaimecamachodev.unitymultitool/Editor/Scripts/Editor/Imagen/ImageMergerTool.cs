using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;

namespace VZOptizone
{
    public static class ImageChannelMergerTool
    {
        private static Texture2D redChannelTexture;
        private static Texture2D greenChannelTexture;
        private static Texture2D blueChannelTexture;
        private static Texture2D alphaChannelTexture;
        private static string outputImageName = "Channels_Combined";
        private static Object outputFolder; // Carpeta donde se guardará la imagen combinada

        public static void DrawTool()
        {
            GUILayout.Label("Merger - Combine Image Channels", EditorStyles.boldLabel);

            // Campo para la textura del canal Rojo
            redChannelTexture = (Texture2D)EditorGUILayout.ObjectField("Red Channel Texture", redChannelTexture, typeof(Texture2D), false);

            // Campo para la textura del canal Verde
            greenChannelTexture = (Texture2D)EditorGUILayout.ObjectField("Green Channel Texture", greenChannelTexture, typeof(Texture2D), false);

            // Campo para la textura del canal Azul
            blueChannelTexture = (Texture2D)EditorGUILayout.ObjectField("Blue Channel Texture", blueChannelTexture, typeof(Texture2D), false);

            // Campo para la textura del canal Alpha
            alphaChannelTexture = (Texture2D)EditorGUILayout.ObjectField("Alpha Channel Texture", alphaChannelTexture, typeof(Texture2D), false);

            // Campo para el nombre de la imagen de salida
            outputImageName = EditorGUILayout.TextField("Output Image Name", outputImageName);

            // Campo para seleccionar la carpeta de salida
            outputFolder = EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            // Botón para combinar los canales
            if (GUILayout.Button("Merge Channels"))
            {
                MergeImagesChannels();
            }
        }

        private static void MergeImagesChannels()
        {
            // Verificar que todas las texturas estén seleccionadas
            if (redChannelTexture == null || greenChannelTexture == null || blueChannelTexture == null || alphaChannelTexture == null)
            {
                UnityEngine.Debug.LogError("Debes seleccionar todas las texturas (Red, Green, Blue, Alpha) antes de combinar.");
                return;
            }

            // Verificar que la carpeta de salida esté seleccionada
            if (outputFolder == null)
            {
                UnityEngine.Debug.LogError("Por favor, selecciona una carpeta de destino para guardar la imagen combinada.");
                return;
            }

            // Obtener las rutas de los archivos seleccionados
            string redImagePath = AssetDatabase.GetAssetPath(redChannelTexture);
            string greenImagePath = AssetDatabase.GetAssetPath(greenChannelTexture);
            string blueImagePath = AssetDatabase.GetAssetPath(blueChannelTexture);
            string alphaImagePath = AssetDatabase.GetAssetPath(alphaChannelTexture);

            // Obtener la ruta de la carpeta de salida
            string outputFolderPath = AssetDatabase.GetAssetPath(outputFolder);
            if (!Directory.Exists(outputFolderPath))
            {
                UnityEngine.Debug.LogError("La carpeta de destino seleccionada no es válida.");
                return;
            }

            // Ruta completa de la imagen de salida
            string outputPath = Path.Combine(outputFolderPath, $"{outputImageName}.png");

            // Ruta del binario de ffmpeg
            string ffmpegPath = Application.dataPath + "/VZ Optizone/Plugins/ffmpeg/bin/ffmpeg.exe";

            // Comando para combinar las imágenes
            string command = $"-i \"{greenImagePath}\" -i \"{blueImagePath}\" -i \"{redImagePath}\" -i \"{alphaImagePath}\" -filter_complex " +
                             "\"[0:v][1:v][2:v]mergeplanes=0x001020:format=gbrp10le[vrgb];[vrgb][3:v]alphamerge\" -c:v png \""+ outputPath + "\"";

            // Ejecutar el comando ffmpeg
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

            AssetDatabase.Refresh();
            UnityEngine.Debug.Log($"Imagen combinada guardada en: {outputPath}");
        }
    }
}
