using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    public static class BakeMeshTool
    {
        private static SkinnedMeshRenderer skinnedMeshRenderer;
        private static string outputMeshName = "BakedMesh";
        private static DefaultAsset destinationFolder;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Bake Pose") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Hornea la pose actual de un SkinnedMeshRenderer en una malla estática y la guarda como un nuevo asset.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var rendererField = new ObjectField("Skinned Mesh Renderer") { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true, value = skinnedMeshRenderer };
                rendererField.RegisterValueChangedCallback(evt => { skinnedMeshRenderer = evt.newValue as SkinnedMeshRenderer; RefreshContent(); });
                sourcePanel.Add(rendererField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = destinationFolder };
                folderField.RegisterValueChangedCallback(evt => { destinationFolder = evt.newValue as DefaultAsset; RefreshContent(); });
                sourcePanel.Add(folderField);

                var nameField = new TextField("Nombre de la malla") { value = outputMeshName };
                nameField.RegisterValueChangedCallback(evt => outputMeshName = evt.newValue);
                sourcePanel.Add(nameField);

                contentContainer.Add(sourcePanel);

                bool canBake = skinnedMeshRenderer != null && destinationFolder != null;
                if (!canBake)
                {
                    contentContainer.Add(new HelpBox("Selecciona un Skinned Mesh Renderer y una carpeta de destino.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var bakeButton = new MTUIActionButton("Bake Pose to Mesh", BakeMesh);
                bakeButton.style.marginTop = 10;
                bakeButton.SetAvailable(canBake);
                contentContainer.Add(bakeButton);
            }

            RefreshContent();
            return root;
        }

        private static void BakeMesh()
        {
            var bakedMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(bakedMesh);

            string folderPath = AssetDatabase.GetAssetPath(destinationFolder);
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError("Invalid destination folder.");
                return;
            }

            string meshPath = Path.Combine(folderPath, outputMeshName + ".asset");

            AssetDatabase.CreateAsset(bakedMesh, meshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Baked mesh saved at: {meshPath}");
        }
    }
}
