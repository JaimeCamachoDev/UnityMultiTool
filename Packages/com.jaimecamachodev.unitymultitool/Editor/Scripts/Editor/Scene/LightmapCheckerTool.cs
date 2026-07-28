using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Lighting
{
    public static class LightmapCheckerTool
    {
        private static readonly List<GameObject> lightmappedObjects = new List<GameObject>();

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();

            root.Add(new Label("Lightmap Checker") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Busca en la escena los objetos que tienen un lightmap horneado asignado.", HelpBoxMessageType.Info));

            var resultsContainer = new VisualElement { style = { marginTop = 10 } };

            var checkButton = new MTUIActionButton("Check Scene for Lightmaps", () =>
            {
                CheckScene();
                RefreshResults(resultsContainer);
            });
            checkButton.style.marginTop = 10;
            root.Add(checkButton);

            root.Add(resultsContainer);
            RefreshResults(resultsContainer);

            return root;
        }

        private static void RefreshResults(VisualElement container)
        {
            container.Clear();

            if (lightmappedObjects.Count == 0)
            {
                container.Add(new HelpBox("No se encontraron objetos con lightmap asignado.", HelpBoxMessageType.Info));
                return;
            }

            var resultsPanel = new MTUIPanel($"Objetos con lightmap ({lightmappedObjects.Count})");

            var scroll = new ScrollView { style = { maxHeight = 400 } };
            foreach (GameObject go in lightmappedObjects)
            {
                if (go == null)
                {
                    continue;
                }

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

                var selectButton = new MTUIActionButton(go.name, () =>
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                    if (SceneView.lastActiveSceneView != null)
                    {
                        SceneView.lastActiveSceneView.FrameSelected();
                    }
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

        private static void CheckScene()
        {
            lightmappedObjects.Clear();

            MeshRenderer[] meshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer.lightmapIndex != -1)
                {
                    lightmappedObjects.Add(renderer.gameObject);
                }
            }
        }
    }
}
