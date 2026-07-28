using System.Collections.Generic;
using System.Linq;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

namespace JaimeCamachoDev.Multitool.Textures
{
    public static class UIAnimationClipGeneratorTool
    {
        private static readonly List<Sprite> sprites = new List<Sprite>();
        private static float timeBetweenFrames = 0.1f;
        private static Image targetImage;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Convert Sprites to Animation Clip") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Genera un AnimationClip que reproduce una secuencia de sprites sobre un componente Image de UI.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var settingsPanel = new MTUIPanel("Ajustes");

                var timeField = new FloatField("Tiempo entre fotogramas") { value = timeBetweenFrames };
                timeField.RegisterValueChangedCallback(evt => timeBetweenFrames = evt.newValue);
                settingsPanel.Add(timeField);

                var targetField = new ObjectField("Image de destino") { objectType = typeof(Image), allowSceneObjects = true, value = targetImage };
                targetField.RegisterValueChangedCallback(evt => { targetImage = evt.newValue as Image; RefreshContent(); });
                settingsPanel.Add(targetField);

                contentContainer.Add(settingsPanel);

                var spritesPanel = new MTUIPanel("Sprites") { style = { marginTop = 10 } };
                spritesPanel.Add(new MTUIInfoLabel("Selecciona los sprites en la ventana Project y pulsa \"Select Sprites\"."));

                var selectButton = new MTUIActionButton("Select Sprites", () =>
                {
                    SelectSprites();
                    RefreshContent();
                });
                selectButton.style.marginTop = 6;
                spritesPanel.Add(selectButton);

                if (sprites.Count > 0)
                {
                    var scroll = new ScrollView { style = { maxHeight = 200, marginTop = 6 } };
                    foreach (Sprite sprite in sprites)
                    {
                        scroll.Add(new Label(sprite.name));
                    }
                    spritesPanel.Add(new MTUIInfoLabel($"{sprites.Count} sprite(s) seleccionado(s):") { style = { marginTop = 6 } });
                    spritesPanel.Add(scroll);
                }

                contentContainer.Add(spritesPanel);

                bool canGenerate = sprites.Count > 0 && targetImage != null;
                if (sprites.Count == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona al menos un sprite para generar la animación.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }
                else if (targetImage == null)
                {
                    contentContainer.Add(new HelpBox("Asigna la Image de destino de la animación.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var generateButton = new MTUIActionButton("Generate Animation Clip", GenerateAnimationClip);
                generateButton.style.marginTop = 10;
                generateButton.SetAvailable(canGenerate);
                contentContainer.Add(generateButton);
            }

            RefreshContent();
            return root;
        }

        private static void SelectSprites()
        {
            sprites.Clear();

            string[] guids = Selection.assetGUIDs;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is Sprite sprite)
                    {
                        sprites.Add(sprite);
                    }
                }
            }

            sprites.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        }

        private static void GenerateAnimationClip()
        {
            if (sprites.Count == 0)
            {
                Debug.LogError("No sprites selected. Please select sprites for the animation.");
                return;
            }

            if (targetImage == null)
            {
                Debug.LogError("No target image selected. Please assign a target image for the animation.");
                return;
            }

            var animationClip = new AnimationClip
            {
                frameRate = 1f / timeBetweenFrames
            };

            var spriteBinding = new EditorCurveBinding
            {
                type = typeof(Image),
                path = "",
                propertyName = "m_Sprite"
            };

            var keyFrames = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyFrames[i] = new ObjectReferenceKeyframe
                {
                    time = i * timeBetweenFrames,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(animationClip, spriteBinding, keyFrames);

            string clipPath = EditorUtility.SaveFilePanelInProject("Save Animation Clip", "NewAnimationClip", "anim", "Please enter a name for the animation clip.");
            if (!string.IsNullOrEmpty(clipPath))
            {
                AssetDatabase.CreateAsset(animationClip, clipPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Animation clip created and saved at: " + clipPath);
            }
        }
    }
}
