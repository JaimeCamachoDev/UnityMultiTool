using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace VZOptizone
{
    public static class CombineAnimationsWithPathsTool
    {
        private static GameObject parentObject; // Objeto padre con los hijos que contienen animaciones
        private static string newClipName = "CombinedAnimationClip"; // Nombre predeterminado del nuevo clip combinado
        private static DefaultAsset saveFolder; // Carpeta de destino como DefaultAsset

        public static void DrawTool()
        {
            EditorGUILayout.LabelField("1. Selecciona el Objeto Padre", EditorStyles.boldLabel);
            parentObject = (GameObject)EditorGUILayout.ObjectField("Objeto Padre", parentObject, typeof(GameObject), true);

            EditorGUILayout.LabelField("2. Nombre del Nuevo Clip", EditorStyles.boldLabel);
            newClipName = EditorGUILayout.TextField("Nombre del Clip", newClipName);

            EditorGUILayout.LabelField("3. Carpeta de Guardado", EditorStyles.boldLabel);
            saveFolder = (DefaultAsset)EditorGUILayout.ObjectField("Carpeta de Guardado", saveFolder, typeof(DefaultAsset), false);

            GUILayout.Space(20);

            if (GUILayout.Button("Combinar Animaciones"))
            {
                if (parentObject != null && saveFolder != null)
                {
                    CombineAnimations();
                }
                else
                {
                    if (parentObject == null) Debug.LogError("Por favor selecciona el objeto padre.");
                    if (saveFolder == null) Debug.LogError("Por favor selecciona una carpeta de guardado.");
                }
            }
        }

        private static void CombineAnimations()
        {
            string saveFolderPath = AssetDatabase.GetAssetPath(saveFolder);

            if (!AssetDatabase.IsValidFolder(saveFolderPath))
            {
                Debug.LogError("La carpeta especificada no es válida. Verifica la ruta.");
                return;
            }

            List<AnimationClip> clipsToCombine = new List<AnimationClip>();
            Dictionary<string, AnimationClip> clipPaths = new Dictionary<string, AnimationClip>();

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

            AnimationClip combinedClip = new AnimationClip();

            foreach (var kvp in clipPaths)
            {
                string pathPrefix = kvp.Key.Split('/')[0];
                AnimationClip clip = kvp.Value;

                EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
                foreach (EditorCurveBinding binding in curveBindings)
                {
                    string newPath = pathPrefix + "/" + binding.path;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                    AnimationCurve adjustedCurve = new AnimationCurve();
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
