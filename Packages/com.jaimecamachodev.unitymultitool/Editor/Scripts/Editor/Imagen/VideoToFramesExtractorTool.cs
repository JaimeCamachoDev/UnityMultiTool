using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;


namespace VZOptizone
{
    public static class VideoToFramesExtractorTool
    {
        private static string videoPath;
        private static DefaultAsset outputFolder;
        private static float videoDuration = 3f; // Duración por defecto
        private static float videoFrameRate = 24f; // Tasa de fotogramas por defecto

        public static void DrawTool()
        {
            GUILayout.Label("Video to Frames Extractor", EditorStyles.boldLabel);

            // Campo para arrastrar y soltar el archivo de video
            videoPath = DrawDragAndDropField("Drag and Drop Video File", videoPath);

            GUILayout.Space(10f);

            // Carpeta de salida: arrastra una carpeta desde la ventana Project
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
            if (outputFolder == null)
            {
                EditorGUILayout.HelpBox("Arrastra una carpeta del proyecto para guardar los frames extraídos.", MessageType.Info);
            }

            GUILayout.Space(10f);

            GUILayout.Label("Video Properties", EditorStyles.boldLabel);
            videoDuration = EditorGUILayout.FloatField("Video Duration (seconds):", videoDuration);
            videoFrameRate = EditorGUILayout.FloatField("Frame Rate:", videoFrameRate);

            if (string.IsNullOrEmpty(videoPath))
            {
                EditorGUILayout.HelpBox("Arrastra un archivo de vídeo en el campo superior para poder extraer frames.", MessageType.Info);
            }

            GUILayout.Space(20f);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(videoPath) || outputFolder == null))
            {
                if (GUILayout.Button("Extract Frames from Video"))
                {
                    ExtractFramesFromVideo();
                }
            }
        }

        private static string DrawDragAndDropField(string label, string path)
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
            path = EditorGUILayout.TextField(path);
            Rect dropArea = GUILayoutUtility.GetLastRect();

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!dropArea.Contains(evt.mousePosition)) return path;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var draggedObject in DragAndDrop.paths)
                    {
                        if (File.Exists(draggedObject))
                        {
                            path = draggedObject;
                            break;
                        }
                    }
                }
            }

            return path;
        }

        private static void ExtractFramesFromVideo()
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                UnityEngine.Debug.LogError("Video path is empty. Please specify the path to the video file.");
                return;
            }

            if (outputFolder == null)
            {
                UnityEngine.Debug.LogError("Output folder is empty. Please assign the output folder.");
                return;
            }

            string outputPath = AssetDatabase.GetAssetPath(outputFolder);

            // Calcular el número total de fotogramas
            int totalFrames = Mathf.CeilToInt(videoDuration * videoFrameRate);

            // Crear carpeta para los frames
            string framesFolder = Path.Combine(outputPath, "Frames");
            Directory.CreateDirectory(framesFolder);

            // Extraer los fotogramas del video usando FFmpeg
            string framesOutputPath = Path.Combine(framesFolder, "frame_%04d.png"); 
            
            string intermediatePath = Path.Combine(outputPath, "intermediate.mov");
            string convertArgs = $"-i \"{videoPath}\" -c:v qtrle -pix_fmt argb \"{intermediatePath}\"";
            RunFFmpegCommand(convertArgs);

            // Luego extrae los frames del archivo intermedio
            string ffmpegArgs = $"-i \"{intermediatePath}\" -vf \"fps={videoFrameRate},format=rgba\" -c:v png \"{framesOutputPath}\"";
            RunFFmpegCommand(ffmpegArgs);

            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"Frames extracted and saved to: {framesFolder}");
        }

        private static void RunFFmpegCommand(string args)
        {
            string ffmpegPath = Application.dataPath + "/VZ Optizone/Plugins/ffmpeg/bin/ffmpeg.exe";

            ProcessStartInfo processInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process process = Process.Start(processInfo);
            process.WaitForExit();
        }
    }
}