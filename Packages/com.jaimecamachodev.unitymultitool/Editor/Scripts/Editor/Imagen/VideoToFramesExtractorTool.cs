using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;


namespace VZOptizone
{
    public static class VideoToFramesExtractorTool
    {
        private static string videoPath;
        private static string outputPath;
        private static float videoDuration = 3f; // Duración por defecto
        private static float videoFrameRate = 24f; // Tasa de fotogramas por defecto

        public static void DrawTool()
        {
            GUILayout.Label("Video to Frames Extractor", EditorStyles.boldLabel);

            // Campo para arrastrar y soltar el archivo de video
            videoPath = DrawDragAndDropField("Drag and Drop Video File", videoPath);

            GUILayout.Space(10f);

            // Campo para arrastrar y soltar la carpeta de salida
            outputPath = DrawDragAndDropField("Output Folder", outputPath, true);

            GUILayout.Space(10f);

            GUILayout.Label("Video Properties", EditorStyles.boldLabel);
            videoDuration = EditorGUILayout.FloatField("Video Duration (seconds):", videoDuration);
            videoFrameRate = EditorGUILayout.FloatField("Frame Rate:", videoFrameRate);

            GUILayout.Space(20f);

            if (GUILayout.Button("Extract Frames from Video"))
            {
                ExtractFramesFromVideo();
            }
        }

        private static string DrawDragAndDropField(string label, string path, bool isFolder = false)
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
                        if ((isFolder && Directory.Exists(draggedObject)) || (!isFolder && File.Exists(draggedObject)))
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

            if (string.IsNullOrEmpty(outputPath))
            {
                UnityEngine.Debug.LogError("Output path is empty. Please specify the output folder path.");
                return;
            }

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