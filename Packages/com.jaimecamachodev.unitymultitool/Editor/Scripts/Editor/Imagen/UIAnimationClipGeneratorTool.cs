using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace VZOptizone
{
    public static class UIAnimationClipGeneratorTool
    {
        private static AnimationClip animationClip;
        private static List<Sprite> sprites = new List<Sprite>();
        private static float timeBetweenFrames = 0.1f;
        private static Image targetImage;

        public static void DrawTool()
        {
            GUILayout.Label("Animation Clip Settings", EditorStyles.boldLabel);

            GUILayout.Space(10f);

            // Campo para la duración entre fotogramas
            timeBetweenFrames = EditorGUILayout.FloatField("Time Between Frames:", timeBetweenFrames);

            GUILayout.Space(10f);

            // Campo para asignar el `Image` de destino en la UI
            targetImage = EditorGUILayout.ObjectField("Target Image:", targetImage, typeof(Image), true) as Image;

            GUILayout.Space(10f);

            // Selección de los sprites
            GUILayout.Label("Select Sprites for Animation:", EditorStyles.boldLabel);
            GUILayout.Space(5f);

            if (GUILayout.Button("Select Sprites", GUILayout.Width(150)))
            {
                SelectSprites();
            }

            // Muestra una lista de sprites seleccionados
            GUILayout.Space(10f);
            if (sprites.Count > 0)
            {
                GUILayout.Label($"Selected Sprites ({sprites.Count}):", EditorStyles.boldLabel);
                foreach (var sprite in sprites)
                {
                    GUILayout.Label(sprite.name);
                }
            }

            GUILayout.Space(20f);

            // Botón para generar el clip de animación
            if (GUILayout.Button("Generate Animation Clip"))
            {
                GenerateAnimationClip();
            }
        }

        private static void SelectSprites()
        {
            // Limpia la lista de sprites antes de agregar nuevos
            sprites.Clear();

            // Obtener los GUID de los objetos seleccionados en el editor
            string[] guids = Selection.assetGUIDs;

            // Cargar todos los assets seleccionados
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object asset in assets)
                {
                    if (asset is Sprite)
                    {
                        sprites.Add(asset as Sprite);
                    }
                }
            }

            // Ordenar los sprites alfabéticamente por nombre para mantener el orden
            sprites = sprites.OrderBy(sprite => sprite.name).ToList();
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

            // Crear un nuevo clip de animación
            animationClip = new AnimationClip
            {
                frameRate = 1f / timeBetweenFrames
            };

            // Definir curvas para las propiedades de los sprites
            EditorCurveBinding spriteBinding = new EditorCurveBinding
            {
                type = typeof(Image),
                path = "",
                propertyName = "m_Sprite"
            };

            // Crear keyframes para el clip de animación
            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keyFrames[i] = new ObjectReferenceKeyframe
                {
                    time = i * timeBetweenFrames,
                    value = sprites[i]
                };
            }

            // Asignar los keyframes al clip de animación
            AnimationUtility.SetObjectReferenceCurve(animationClip, spriteBinding, keyFrames);

            // Guardar el clip de animación
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