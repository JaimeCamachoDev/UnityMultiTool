using System.Collections.Generic;
using System.Linq;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    public static class AnimationTerminatorTool
    {
        private static readonly List<AnimationClip> clips = new List<AnimationClip>();
        private static string nameToRemove = "Texto a eliminar";
        private static string nameToSearch = "Texto a buscar";
        private static string nameToReplace = "Texto a reemplazar";

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Animation Terminator") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Elimina o renombra keys de animación (escala, posición, rotación, visibilidad) en uno o varios AnimationClip.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var clipsPanel = new MTUIPanel("Animation Clips");

                var scroll = new ScrollView { style = { maxHeight = 200 } };
                for (int i = 0; i < clips.Count; i++)
                {
                    int index = i;
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

                    var clipField = new ObjectField($"Clip {index}") { objectType = typeof(AnimationClip), allowSceneObjects = false, value = clips[index], style = { flexGrow = 1 } };
                    clipField.RegisterValueChangedCallback(evt => clips[index] = evt.newValue as AnimationClip);
                    row.Add(clipField);

                    var removeButton = new MTUIActionButton("X", () =>
                    {
                        clips.RemoveAt(index);
                        RefreshContent();
                    }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                    removeButton.style.marginLeft = 4;
                    removeButton.style.width = 24;
                    row.Add(removeButton);

                    scroll.Add(row);
                }
                clipsPanel.Add(scroll);

                var addButton = new MTUIActionButton("Add Animation Clip", () =>
                {
                    clips.Add(null);
                    RefreshContent();
                });
                addButton.style.marginTop = 6;
                clipsPanel.Add(addButton);

                var removeNullButton = new MTUIActionButton("Remove Null Clips", () =>
                {
                    clips.RemoveAll(clip => clip == null);
                    RefreshContent();
                });
                removeNullButton.style.marginTop = 4;
                clipsPanel.Add(removeNullButton);

                contentContainer.Add(clipsPanel);

                bool hasClips = clips.Any(clip => clip != null);
                if (!hasClips)
                {
                    contentContainer.Add(new HelpBox("Añade al menos un Animation Clip para poder operar sobre él.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var removeKeysPanel = new MTUIPanel("Remove Keys") { style = { marginTop = 10 } };

                var removeScaleButton = new MTUIActionButton("Remove Scale Keys", () => RemoveKeys("m_LocalScale"));
                removeScaleButton.SetAvailable(hasClips);
                removeKeysPanel.Add(removeScaleButton);

                var removePositionButton = new MTUIActionButton("Remove Position Keys", () => RemoveKeys("m_LocalPosition"));
                removePositionButton.style.marginTop = 4;
                removePositionButton.SetAvailable(hasClips);
                removeKeysPanel.Add(removePositionButton);

                var removeRotationButton = new MTUIActionButton("Remove Rotation Keys", () => RemoveKeys("m_LocalRotation"));
                removeRotationButton.style.marginTop = 4;
                removeRotationButton.SetAvailable(hasClips);
                removeKeysPanel.Add(removeRotationButton);

                var removeRendererButton = new MTUIActionButton("Remove Renderer Enabled Keys", RemoveRendererEnabledKeys);
                removeRendererButton.style.marginTop = 4;
                removeRendererButton.SetAvailable(hasClips);
                removeKeysPanel.Add(removeRendererButton);

                contentContainer.Add(removeKeysPanel);

                var removeByNamePanel = new MTUIPanel("Eliminar Keys por Nombre") { style = { marginTop = 10 } };

                var nameToRemoveField = new TextField("Texto a eliminar") { value = nameToRemove };
                nameToRemoveField.RegisterValueChangedCallback(evt => nameToRemove = evt.newValue);
                removeByNamePanel.Add(nameToRemoveField);

                var removeByNameButton = new MTUIActionButton("Eliminar Keys por Nombre", () => RemoveKeysByName(nameToRemove));
                removeByNameButton.style.marginTop = 6;
                removeByNameButton.SetAvailable(hasClips);
                removeByNamePanel.Add(removeByNameButton);

                contentContainer.Add(removeByNamePanel);

                var replacePanel = new MTUIPanel("Buscar y Reemplazar Nombres") { style = { marginTop = 10 } };

                var searchField = new TextField("Buscar") { value = nameToSearch };
                searchField.RegisterValueChangedCallback(evt => nameToSearch = evt.newValue);
                replacePanel.Add(searchField);

                var replaceField = new TextField("Reemplazar") { value = nameToReplace };
                replaceField.RegisterValueChangedCallback(evt => nameToReplace = evt.newValue);
                replacePanel.Add(replaceField);

                var replaceButton = new MTUIActionButton("Reemplazar Nombres en Keys", () => ReplaceKeysByName(nameToSearch, nameToReplace));
                replaceButton.style.marginTop = 6;
                replaceButton.SetAvailable(hasClips);
                replacePanel.Add(replaceButton);

                contentContainer.Add(replacePanel);
            }

            RefreshContent();
            return root;
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
                        var newBinding = binding;
                        newBinding.path = binding.path.Replace(search, replace);
                        newBinding.propertyName = binding.propertyName.Replace(search, replace);

                        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve);

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
