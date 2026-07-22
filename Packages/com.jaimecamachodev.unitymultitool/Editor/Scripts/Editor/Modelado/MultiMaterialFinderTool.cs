using System.Collections.Generic;
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

            var resultsContainer = new VisualElement { style = { marginTop = 10 } };

            var findButton = new Button(() =>
            {
                FindObjectsWithMultipleMaterials();
                RefreshResults(resultsContainer);
            })
            { text = "Find Objects with Multiple Materials" };
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

            container.Add(new Label("Objects with Multiple Materials:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

            var scroll = new ScrollView { style = { maxHeight = 240 } };
            foreach (GameObject go in objectsWithMultiMaterial)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

                row.Add(new Button(() =>
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                })
                { text = go.name, style = { width = 200 } });

                row.Add(new Button(() => EditorGUIUtility.PingObject(go))
                { text = "Highlight", style = { width = 100 } });

                scroll.Add(row);
            }
            container.Add(scroll);
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
