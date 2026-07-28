using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Misc
{
    public static class RenameTool
    {
        private static string prefix = string.Empty;
        private static string suffix = string.Empty;
        private static string searchFor = string.Empty;
        private static string replaceWith = string.Empty;
        private static bool renameInScene = true;
        private static bool renameInProject = true;
        private static bool includeChildren;
        private static bool useUpperCase;
        private static bool useLowerCase;
        private static bool applySequentialNumbers;
        private static int startingNumber;
        private static string numberFormat = "D2";

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Renamer") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Renombra en bloque los objetos de escena y/o assets del proyecto actualmente seleccionados: prefijo, sufijo, buscar y reemplazar, mayúsculas/minúsculas y numeración secuencial.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var targetPanel = new MTUIPanel("Objetivo");

                var sceneToggle = new Toggle("Renombrar en la escena") { value = renameInScene };
                sceneToggle.RegisterValueChangedCallback(evt => { renameInScene = evt.newValue; RefreshContent(); });
                targetPanel.Add(sceneToggle);

                var projectToggle = new Toggle("Renombrar en el proyecto") { value = renameInProject };
                projectToggle.RegisterValueChangedCallback(evt => { renameInProject = evt.newValue; RefreshContent(); });
                targetPanel.Add(projectToggle);

                var childrenToggle = new Toggle("Incluir hijos") { value = includeChildren };
                childrenToggle.RegisterValueChangedCallback(evt => includeChildren = evt.newValue);
                targetPanel.Add(childrenToggle);

                int sceneCount = Selection.gameObjects.Length;
                int assetCount = 0;
                foreach (Object obj in Selection.objects)
                {
                    if (AssetDatabase.Contains(obj))
                    {
                        assetCount++;
                    }
                }

                targetPanel.Add(new MTUIInfoLabel($"{sceneCount} objeto(s) de escena y {assetCount} asset(s) seleccionados.") { style = { marginTop = 4 } });
                contentContainer.Add(targetPanel);

                var textPanel = new MTUIPanel("Prefijo, sufijo y reemplazo") { style = { marginTop = 10 } };

                var prefixField = new TextField("Prefijo") { value = prefix };
                prefixField.RegisterValueChangedCallback(evt => prefix = evt.newValue);
                textPanel.Add(prefixField);

                var suffixField = new TextField("Sufijo") { value = suffix };
                suffixField.RegisterValueChangedCallback(evt => suffix = evt.newValue);
                textPanel.Add(suffixField);

                var searchField = new TextField("Buscar") { value = searchFor };
                searchField.RegisterValueChangedCallback(evt => searchFor = evt.newValue);
                textPanel.Add(searchField);

                var replaceField = new TextField("Reemplazar por") { value = replaceWith };
                replaceField.RegisterValueChangedCallback(evt => replaceWith = evt.newValue);
                textPanel.Add(replaceField);

                contentContainer.Add(textPanel);

                var casePanel = new MTUIPanel("Mayúsculas, minúsculas y numeración") { style = { marginTop = 10 } };

                Toggle upperToggle = null;
                Toggle lowerToggle = null;

                upperToggle = new Toggle("Convertir a MAYÚSCULAS") { value = useUpperCase };
                lowerToggle = new Toggle("Convertir a minúsculas") { value = useLowerCase };

                upperToggle.RegisterValueChangedCallback(evt =>
                {
                    useUpperCase = evt.newValue;
                    if (useUpperCase && useLowerCase)
                    {
                        useLowerCase = false;
                        lowerToggle.SetValueWithoutNotify(false);
                    }
                });
                lowerToggle.RegisterValueChangedCallback(evt =>
                {
                    useLowerCase = evt.newValue;
                    if (useLowerCase && useUpperCase)
                    {
                        useUpperCase = false;
                        upperToggle.SetValueWithoutNotify(false);
                    }
                });

                casePanel.Add(upperToggle);
                casePanel.Add(lowerToggle);

                var sequentialToggle = new Toggle("Aplicar numeración secuencial") { value = applySequentialNumbers, style = { marginTop = 6 } };
                casePanel.Add(sequentialToggle);

                var sequentialFields = new VisualElement { style = { marginLeft = 15, marginTop = 2, display = applySequentialNumbers ? DisplayStyle.Flex : DisplayStyle.None } };

                var startField = new IntegerField("Número inicial") { value = startingNumber };
                startField.RegisterValueChangedCallback(evt => startingNumber = evt.newValue);
                sequentialFields.Add(startField);

                var formatField = new TextField("Formato (ej. D2)") { value = numberFormat };
                formatField.RegisterValueChangedCallback(evt => numberFormat = evt.newValue);
                sequentialFields.Add(formatField);

                sequentialToggle.RegisterValueChangedCallback(evt =>
                {
                    applySequentialNumbers = evt.newValue;
                    sequentialFields.style.display = applySequentialNumbers ? DisplayStyle.Flex : DisplayStyle.None;
                });

                casePanel.Add(sequentialFields);
                contentContainer.Add(casePanel);

                bool hasSelection = sceneCount > 0 || assetCount > 0;
                bool canRename = hasSelection && (renameInScene || renameInProject);

                if (!hasSelection)
                {
                    contentContainer.Add(new HelpBox("Selecciona uno o varios objetos de la escena o del proyecto para renombrar.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }
                else if (!renameInScene && !renameInProject)
                {
                    contentContainer.Add(new HelpBox("Activa \"Renombrar en la escena\" y/o \"Renombrar en el proyecto\".", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var renameButton = new MTUIActionButton("Renombrar", () =>
                {
                    ApplyRenaming();
                    RefreshContent();
                });
                renameButton.style.marginTop = 10;
                renameButton.SetAvailable(canRename);
                contentContainer.Add(renameButton);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void ApplyRenaming()
        {
            var selectedObjects = new List<Object>();
            if (renameInScene)
            {
                selectedObjects.AddRange(Selection.gameObjects);
            }
            if (renameInProject)
            {
                selectedObjects.AddRange(Selection.objects);
            }

            var processedGameObjects = new HashSet<GameObject>();
            int counter = startingNumber;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Renombrar objetos");

            foreach (Object obj in selectedObjects)
            {
                if (obj == null)
                {
                    continue;
                }

                if (obj is GameObject gameObject)
                {
                    RenameGameObject(gameObject, processedGameObjects, ref counter);
                    if (includeChildren)
                    {
                        foreach (Transform child in gameObject.transform)
                        {
                            RenameGameObject(child.gameObject, processedGameObjects, ref counter);
                        }
                    }
                }

                if (AssetDatabase.Contains(obj))
                {
                    RenameAsset(obj, ref counter);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RenameGameObject(GameObject gameObject, HashSet<GameObject> processedGameObjects, ref int counter)
        {
            // Evita renombrar dos veces el mismo objeto si aparece tanto en
            // Selection.gameObjects como en Selection.objects (ocurre cuando ambos
            // modos están activos a la vez y hay objetos de escena seleccionados).
            if (!processedGameObjects.Add(gameObject))
            {
                return;
            }

            Undo.RecordObject(gameObject, "Renombrar objeto");
            gameObject.name = GenerateNewName(gameObject.name, ref counter);
        }

        private static void RenameAsset(Object asset, ref int counter)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string originalName = Path.GetFileNameWithoutExtension(path);
            string newName = GenerateNewName(originalName, ref counter);
            AssetDatabase.RenameAsset(path, newName);
        }

        private static string GenerateNewName(string originalName, ref int counter)
        {
            string newName = originalName;

            if (!string.IsNullOrEmpty(prefix))
            {
                newName = prefix + newName;
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                newName += suffix;
            }

            if (!string.IsNullOrEmpty(searchFor))
            {
                newName = newName.Replace(searchFor, replaceWith);
            }

            if (useUpperCase)
            {
                newName = newName.ToUpper();
            }
            else if (useLowerCase)
            {
                newName = newName.ToLower();
            }

            if (applySequentialNumbers)
            {
                newName += counter.ToString(numberFormat);
                counter++;
            }

            return newName;
        }
    }
}
