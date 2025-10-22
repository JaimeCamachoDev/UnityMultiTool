using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace VZOptizone
{
    public static class AnimationTerminatorTool
    {
        private static List<AnimationClip> clips = new List<AnimationClip>();
        private static string nameToRemove = "Texto a eliminar";
        private static string nameToSearch = "Texto a buscar";
        private static string nameToReplace = "Texto a reemplazar";

        public static void DrawTool()
        {
            GUILayout.Label("Animation Clips", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            for (int i = 0; i < clips.Count; i++)
            {
                clips[i] = (AnimationClip)EditorGUILayout.ObjectField("Clip " + i, clips[i], typeof(AnimationClip), false);
            }

            // Botón para añadir más clips
            if (GUILayout.Button("Add Animation Clip"))
            {
                clips.Add(null); // Añadir un nuevo espacio para un clip
            }

            // Botón para eliminar clips vacíos
            if (GUILayout.Button("Remove Null Clips"))
            {
                clips.RemoveAll(clip => clip == null); // Eliminar clips que no han sido asignados
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Botones para remover Keys de Scale, Position, Rotation, Renderer Enabled
            GUILayout.Label("Remove Keys", EditorStyles.boldLabel);
            if (GUILayout.Button("Remove Scale Keys"))
            {
                RemoveKeys("m_LocalScale");
            }

            if (GUILayout.Button("Remove Position Keys"))
            {
                RemoveKeys("m_LocalPosition");
            }

            if (GUILayout.Button("Remove Rotation Keys"))
            {
                RemoveKeys("m_LocalRotation");
            }

            if (GUILayout.Button("Remove Renderer Enabled Keys"))
            {
                RemoveRendererEnabledKeys();
            }

            GUILayout.Space(10);

            // Campo para eliminar Keys por nombre
            GUILayout.Label("Eliminar Keys por Nombre", EditorStyles.boldLabel);
            nameToRemove = EditorGUILayout.TextField("Texto a eliminar", nameToRemove);
            if (GUILayout.Button("Eliminar Keys por Nombre"))
            {
                RemoveKeysByName(nameToRemove);
            }

            GUILayout.Space(10);

            // Campos para buscar y reemplazar nombres en los clips
            GUILayout.Label("Buscar y Reemplazar Nombres", EditorStyles.boldLabel);
            nameToSearch = EditorGUILayout.TextField("Buscar", nameToSearch);
            nameToReplace = EditorGUILayout.TextField("Reemplazar", nameToReplace);

            if (GUILayout.Button("Reemplazar Nombres en Keys"))
            {
                ReplaceKeysByName(nameToSearch, nameToReplace);
            }
        }

        private static void RemoveKeys(string propertyName)
        {
            if (clips.Count == 0)
            {
                Debug.LogWarning("No animation clips selected.");
                return;
            }

            foreach (var clip in clips)
            {
                if (clip == null) continue;

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                int removedKeysCount = 0;

                foreach (var binding in bindings)
                {
                    if (binding.propertyName.StartsWith(propertyName))
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        removedKeysCount++;
                    }
                }

                Debug.Log($"Removed {removedKeysCount} {propertyName} keys from {clip.name}.");
            }
        }

        private static void RemoveKeysByName(string keyName)
        {
            if (clips.Count == 0)
            {
                Debug.LogWarning("No animation clips selected.");
                return;
            }

            foreach (var clip in clips)
            {
                if (clip == null) continue;

                int removedKeysCount = 0;
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

                foreach (var binding in bindings)
                {
                    if (binding.path.Contains(keyName) || binding.propertyName.Contains(keyName))
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        removedKeysCount++;
                    }
                }

                Debug.Log($"Removed {removedKeysCount} keys from {clip.name} containing '{keyName}'.");
            }
        }

        private static void ReplaceKeysByName(string search, string replace)
        {
            if (clips.Count == 0)
            {
                Debug.LogWarning("No animation clips selected.");
                return;
            }

            foreach (var clip in clips)
            {
                if (clip == null) continue;

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                int changedKeysCount = 0;

                foreach (var binding in bindings)
                {
                    if (binding.path.Contains(search) || binding.propertyName.Contains(search))
                    {
                        // Crear un nuevo binding con los valores reemplazados
                        var newBinding = binding;
                        newBinding.path = binding.path.Replace(search, replace);
                        newBinding.propertyName = binding.propertyName.Replace(search, replace);

                        // Copiar la curva desde el binding original al nuevo binding
                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        AnimationUtility.SetEditorCurve(clip, binding, null); // Eliminar el binding original
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve); // Crear la nueva curva

                        changedKeysCount++;
                    }
                }

                Debug.Log($"Replaced {changedKeysCount} keys in {clip.name}, searching '{search}' and replacing with '{replace}'.");
            }
        }

        private static void RemoveRendererEnabledKeys()
        {
            if (clips.Count == 0)
            {
                Debug.LogWarning("No animation clips selected.");
                return;
            }

            foreach (var clip in clips)
            {
                if (clip == null) continue;

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

                int removedKeysCount = 0;

                foreach (var binding in bindings)
                {
                    if (binding.type == typeof(Renderer) && binding.propertyName == "m_Enabled")
                    {
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        removedKeysCount++;
                    }
                }

                Debug.Log($"Removed {removedKeysCount} renderer enabled keys from {clip.name}.");
            }
        }
    }
}
