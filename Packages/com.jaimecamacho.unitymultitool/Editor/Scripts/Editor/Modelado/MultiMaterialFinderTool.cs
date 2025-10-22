using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Optizone
{
    public static class MultiMaterialFinderTool
    {
        private static Vector2 scrollPos;
        private static List<GameObject> objectsWithMultiMaterial = new List<GameObject>();

        public static void DrawTool()
        {
            GUILayout.Label("Multi-Material Finder", EditorStyles.boldLabel);

            // Botón para encontrar los objetos
            if (GUILayout.Button("Find Objects with Multiple Materials"))
            {
                FindObjectsWithMultipleMaterials();
            }

            GUILayout.Space(10);

            // Mostrar los objetos encontrados
            if (objectsWithMultiMaterial.Count > 0)
            {
                GUILayout.Label("Objects with Multiple Materials:", EditorStyles.boldLabel);

                // Scroll para la lista de objetos
                scrollPos = GUILayout.BeginScrollView(scrollPos, false, true);

                for (int i = 0; i < objectsWithMultiMaterial.Count; i++)
                {
                    GUILayout.BeginHorizontal();

                    // Botón para seleccionar el objeto
                    if (GUILayout.Button(objectsWithMultiMaterial[i].name, GUILayout.Width(200)))
                    {
                        Selection.activeGameObject = objectsWithMultiMaterial[i];
                        EditorGUIUtility.PingObject(objectsWithMultiMaterial[i]);
                    }

                    // Botón para resaltar el objeto
                    if (GUILayout.Button("Highlight", GUILayout.Width(100)))
                    {
                        EditorGUIUtility.PingObject(objectsWithMultiMaterial[i]);
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("No objects with multiple materials found.", EditorStyles.helpBox);
            }
        }

        // Método para encontrar los objetos con varios materiales
        private static void FindObjectsWithMultipleMaterials()
        {
            objectsWithMultiMaterial.Clear();
            Renderer[] renderers = Object.FindObjectsOfType<Renderer>();

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
