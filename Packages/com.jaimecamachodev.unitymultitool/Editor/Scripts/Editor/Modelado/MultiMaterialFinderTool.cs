using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class MultiMaterialFinderTool
    {
        private static readonly List<GameObject> objectsWithMultiMaterial = new List<GameObject>();

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();

            root.Add(new Label("Multi-Material Finder") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Busca objetos en la escena que usen varios materiales a la vez.", HelpBoxMessageType.Info));

            var resultsContainer = new VisualElement { style = { marginTop = 10 } };

            var findButton = new MTUIActionButton("Find Objects with Multiple Materials", () =>
            {
                FindObjectsWithMultipleMaterials();
                RefreshResults(resultsContainer);
            });
            findButton.style.marginTop = 10;
            root.Add(findButton);

            root.Add(resultsContainer);
            RefreshResults(resultsContainer);

            return root;
        }

        private static void RefreshResults(VisualElement container)
        {
            container.Clear();

            if (objectsWithMultiMaterial.Count == 0)
            {
                container.Add(new HelpBox("No objects with multiple materials found.", HelpBoxMessageType.Info));
                return;
            }

            var resultsPanel = new MTUIPanel($"Objetos con múltiples materiales ({objectsWithMultiMaterial.Count})");

            var scroll = new ScrollView { style = { maxHeight = 240 } };
            foreach (GameObject go in objectsWithMultiMaterial)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

                var selectButton = new MTUIActionButton(go.name, () =>
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText, TextAnchor.MiddleLeft);
                selectButton.style.width = 200;
                row.Add(selectButton);

                var highlightButton = new MTUIActionButton("Highlight", () => EditorGUIUtility.PingObject(go),
                    MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                highlightButton.style.width = 100;
                row.Add(highlightButton);

                scroll.Add(row);
            }
            resultsPanel.Add(scroll);
            container.Add(resultsPanel);
        }

        // Método para encontrar los objetos con varios materiales
        private static void FindObjectsWithMultipleMaterials()
        {
            objectsWithMultiMaterial.Clear();
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            foreach (Renderer renderer in renderers)
            {
                if (renderer.sharedMaterials.Length > 1)
                {
                    objectsWithMultiMaterial.Add(renderer.gameObject);
                }
            }

            if (objectsWithMultiMaterial.Count == 0)
            {
                Debug.Log("No objects with multiple materials found in the scene.");
            }
            else
            {
                Debug.Log(objectsWithMultiMaterial.Count + " objects with multiple materials found in the scene.");
            }
        }
    }
}
