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
    public static class VideoToFramesExtractorTool
    {
        private static string videoPath;
        private static DefaultAsset outputFolder;
        private static float videoDuration = 3f;
        private static float videoFrameRate = 24f;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Extract Frames from Video") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Extrae los fotogramas de un vídeo como una secuencia de imágenes PNG, usando ffmpeg.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var videoField = new TextField("Arrastra un archivo de vídeo") { value = videoPath };
                videoField.RegisterValueChangedCallback(evt => videoPath = evt.newValue);
                videoField.RegisterCallback<DragUpdatedEvent>(evt =>
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    evt.StopPropagation();
                });
                videoField.RegisterCallback<DragPerformEvent>(evt =>
                {
                    DragAndDrop.AcceptDrag();
                    foreach (string draggedPath in DragAndDrop.paths)
                    {
                        if (File.Exists(draggedPath))
                        {
                            videoPath = draggedPath;
                            break;
                        }
                    }
                    RefreshContent();
                    evt.StopPropagation();
                });
                sourcePanel.Add(videoField);

                if (string.IsNullOrEmpty(videoPath))
                {
                    sourcePanel.Add(new HelpBox("Arrastra un archivo de vídeo en el campo superior para poder extraer frames.", HelpBoxMessageType.Info) { style = { marginTop = 4 } });
                }

                var folderField = new ObjectField("Carpeta de salida") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder };
                folderField.RegisterValueChangedCallback(evt => { outputFolder = evt.newValue as DefaultAsset; RefreshContent(); });
                sourcePanel.Add(folderField);

                if (outputFolder == null)
                {
                    sourcePanel.Add(new HelpBox("Arrastra una carpeta del proyecto para guardar los frames extraídos.", HelpBoxMessageType.Info) { style = { marginTop = 4 } });
                }

                contentContainer.Add(sourcePanel);

                var propertiesPanel = new MTUIPanel("Propiedades del vídeo") { style = { marginTop = 10 } };

                var durationField = new FloatField("Duración del vídeo (segundos)") { value = videoDuration };
                durationField.RegisterValueChangedCallback(evt => videoDuration = evt.newValue);
                propertiesPanel.Add(durationField);

                var frameRateField = new FloatField("Frame Rate") { value = videoFrameRate };
                frameRateField.RegisterValueChangedCallback(evt => videoFrameRate = evt.newValue);
                propertiesPanel.Add(frameRateField);

                contentContainer.Add(propertiesPanel);

                bool canExtract = !string.IsNullOrEmpty(videoPath) && outputFolder != null;

                var extractButton = new MTUIActionButton("Extract Frames from Video", ExtractFramesFromVideo);
                extractButton.style.marginTop = 10;
                extractButton.SetAvailable(canExtract);
                contentContainer.Add(extractButton);
            }

            RefreshContent();
            return root;
        }

        private static void ExtractFramesFromVideo()
        {
            if (string.IsNullOrEmpty(videoPath))
            {
                Debug.LogError("Video path is empty. Please specify the path to the video file.");
                return;
            }

            if (outputFolder == null)
            {
                Debug.LogError("Output folder is empty. Please assign the output folder.");
                return;
            }

            string outputPath = AssetDatabase.GetAssetPath(outputFolder);

            string framesFolder = Path.Combine(outputPath, "Frames");
            Directory.CreateDirectory(framesFolder);

            string framesOutputPath = Path.Combine(framesFolder, "frame_%04d.png");

            string intermediatePath = Path.Combine(outputPath, "intermediate.mov");
            string convertArgs = $"-i \"{videoPath}\" -c:v qtrle -pix_fmt argb \"{intermediatePath}\"";
            RunFFmpegCommand(convertArgs);

            string ffmpegArgs = $"-i \"{intermediatePath}\" -vf \"fps={videoFrameRate},format=rgba\" -c:v png \"{framesOutputPath}\"";
            RunFFmpegCommand(ffmpegArgs);

            AssetDatabase.Refresh();

            Debug.Log($"Frames extracted and saved to: {framesFolder}");
        }

        private static void RunFFmpegCommand(string args)
        {
            string ffmpegPath = FfmpegLocator.ExecutablePath;

            var processInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using Process process = Process.Start(processInfo);
            process.WaitForExit();
        }
    }
}
