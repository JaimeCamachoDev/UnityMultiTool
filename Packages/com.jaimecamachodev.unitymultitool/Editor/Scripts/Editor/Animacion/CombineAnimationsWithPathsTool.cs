using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    public static class CombineAnimationsWithPathsTool
    {
        private static GameObject parentObject;
        private static string newClipName = "CombinedAnimationClip";
        private static DefaultAsset saveFolder;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Combine Animations Into One") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Combina las animaciones de todos los Animator hijos de un objeto padre en un único AnimationClip, prefijando cada ruta con el nombre del hijo de origen.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var parentField = new ObjectField("Objeto Padre") { objectType = typeof(GameObject), allowSceneObjects = true, value = parentObject };
                parentField.RegisterValueChangedCallback(evt => { parentObject = evt.newValue as GameObject; RefreshContent(); });
                sourcePanel.Add(parentField);

                contentContainer.Add(sourcePanel);

                var outputPanel = new MTUIPanel("Salida") { style = { marginTop = 10 } };

                var nameField = new TextField("Nombre del Clip") { value = newClipName };
                nameField.RegisterValueChangedCallback(evt => newClipName = evt.newValue);
                outputPanel.Add(nameField);

                var folderField = new ObjectField("Carpeta de Guardado") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = saveFolder };
                folderField.RegisterValueChangedCallback(evt => { saveFolder = evt.newValue as DefaultAsset; RefreshContent(); });
                outputPanel.Add(folderField);

                contentContainer.Add(outputPanel);

                bool canCombine = parentObject != null && saveFolder != null;
                if (!canCombine)
                {
                    contentContainer.Add(new HelpBox("Selecciona el objeto padre y una carpeta de guardado.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var combineButton = new MTUIActionButton("Combinar Animaciones", CombineAnimations);
                combineButton.style.marginTop = 10;
                combineButton.SetAvailable(canCombine);
                contentContainer.Add(combineButton);
            }

            RefreshContent();
            return root;
        }

        private static void CombineAnimations()
        {
            string saveFolderPath = AssetDatabase.GetAssetPath(saveFolder);

            if (!AssetDatabase.IsValidFolder(saveFolderPath))
            {
                Debug.LogError("La carpeta especificada no es válida. Verifica la ruta.");
                return;
            }

            var clipsToCombine = new List<AnimationClip>();
            var clipPaths = new Dictionary<string, AnimationClip>();

            Animator[] animators = parentObject.GetComponentsInChildren<Animator>();

            foreach (Animator animator in animators)
            {
                string childPath = GetRelativePath(parentObject.transform, animator.transform);

                RuntimeAnimatorController controller = animator.runtimeAnimatorController;
                if (controller == null)
                {
                    Debug.LogWarning($"El objeto '{animator.gameObject.name}' no tiene controlador de animator asignado.");
                    continue;
                }

                foreach (AnimationClip clip in controller.animationClips)
                {
                    string uniquePath = childPath + "/" + clip.name;
                    if (!clipPaths.ContainsKey(uniquePath))
                    {
                        AnimationClip clipInstance = Object.Instantiate(clip);
                        clipsToCombine.Add(clipInstance);
                        clipPaths[uniquePath] = clipInstance;
                    }
                }
            }

            if (clipsToCombine.Count == 0)
            {
                Debug.LogWarning("No se encontraron clips de animación para combinar.");
                return;
            }

            var combinedClip = new AnimationClip();

            foreach (var kvp in clipPaths)
            {
                string pathPrefix = kvp.Key.Split('/')[0];
                AnimationClip clip = kvp.Value;

                EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
                foreach (EditorCurveBinding binding in curveBindings)
                {
                    string newPath = pathPrefix + "/" + binding.path;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                    var adjustedCurve = new AnimationCurve();
                    foreach (Keyframe key in curve.keys)
                    {
                        adjustedCurve.AddKey(new Keyframe(key.time, key.value, key.inTangent, key.outTangent));
                    }
                    combinedClip.SetCurve(newPath, binding.type, binding.propertyName, adjustedCurve);
                }
            }

            string savePath = $"{saveFolderPath}/{newClipName}.anim";
            AssetDatabase.CreateAsset(combinedClip, savePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Animación combinada guardada en: {savePath}");
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == root) return "";

            string path = target.name;
            Transform current = target.parent;

            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}
