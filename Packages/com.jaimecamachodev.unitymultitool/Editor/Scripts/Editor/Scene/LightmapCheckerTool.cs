using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


namespace VZOptizone
{
    public static class LightmapCheckerTool
    {
        private static List<GameObject> lightmappedObjects = new List<GameObject>();
        private static Vector2 scrollPosLightmaps;
        private static GameObject selectedObject;

        public static void DrawTool()
        {
            GUILayout.Label("Lightmap Checker", EditorStyles.boldLabel);

            // Botón para revisar la escena en busca de lightmaps
            if (GUILayout.Button("Check Scene for Lightmaps"))
            {
                CheckScene();
            }

            GUILayout.Space(10);

            // Mostrar los objetos con lightmaps
            GUILayout.Label("Objects with Lightmaps:", EditorStyles.boldLabel);
            scrollPosLightmaps = EditorGUILayout.BeginScrollView(scrollPosLightmaps, GUILayout.Height(600)); // Ajusta la altura según sea necesario
            DisplayObjectList(lightmappedObjects);
            EditorGUILayout.EndScrollView();
        }

        private static void CheckScene()
        {
            lightmappedObjects.Clear();

            MeshRenderer[] meshRenderers = Object.FindObjectsOfType<MeshRenderer>();

            foreach (var renderer in meshRenderers)
            {
                // Comprobar si el objeto tiene asignado un lightmap
                if (renderer.lightmapIndex != -1)
                {
                    lightmappedObjects.Add(renderer.gameObject);
                }
            }
        }

        private static void DisplayObjectList(List<GameObject> objects)
        {
            foreach (var obj in objects)
            {
                EditorGUILayout.BeginHorizontal();

                GUIStyle style = new GUIStyle(GUI.skin.button);

                // Resaltar el botón si es el objeto seleccionado
                if (obj == selectedObject)
                {
                    style.normal.textColor = Color.green;
                    style.fontStyle = FontStyle.Bold;
                }

                if (GUILayout.Button(obj.name, style, GUILayout.ExpandWidth(true)))
                {
                    selectedObject = obj;
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                    SceneView.lastActiveSceneView.FrameSelected();
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}