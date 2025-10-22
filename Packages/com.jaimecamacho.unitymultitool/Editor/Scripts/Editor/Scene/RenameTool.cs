using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace Optizone
{
    public static class RenameTool
    {
        private static string prefix = "";
        private static string suffix = "";
        private static string searchFor = "";
        private static string replaceWith = "";
        private static bool renameInScene = true;
        private static bool renameInProject = true;
        private static bool includeChildren = false;
        private static bool useUpperCase = false;
        private static bool useLowerCase = false;
        private static bool applySequentialNumbers = false;
        private static int startingNumber = 0;
        private static string numberFormat = "D2"; // Default 2 digits
        private static List<Object> selectedObjects = new List<Object>();
        private static HashSet<GameObject> processedGameObjects = new HashSet<GameObject>(); // Para evitar duplicaciones

        public static void DrawTool()
        {
            GUILayout.Label("Renaming Tool", EditorStyles.boldLabel);

            // Opciones para renombrar en escena y en el proyecto
            renameInScene = EditorGUILayout.Toggle("Rename in Scene", renameInScene);
            renameInProject = EditorGUILayout.Toggle("Rename in Project", renameInProject);
            includeChildren = EditorGUILayout.Toggle("Include Children", includeChildren);

            GUILayout.Space(10);

            // Prefijo
            prefix = EditorGUILayout.TextField("Prefix", prefix);

            // Sufijo
            suffix = EditorGUILayout.TextField("Suffix", suffix);

            // Buscar y reemplazar
            searchFor = EditorGUILayout.TextField("Search For", searchFor);
            replaceWith = EditorGUILayout.TextField("Replace With", replaceWith);

            GUILayout.Space(10);

            // Opciones de renombrado adicionales
            useUpperCase = EditorGUILayout.Toggle("Convert to Uppercase", useUpperCase);
            useLowerCase = EditorGUILayout.Toggle("Convert to Lowercase", useLowerCase);

            // Renumeración secuencial
            applySequentialNumbers = EditorGUILayout.Toggle("Apply Sequential Numbers", applySequentialNumbers);
            if (applySequentialNumbers)
            {
                startingNumber = EditorGUILayout.IntField("Starting Number", startingNumber);
                numberFormat = EditorGUILayout.TextField("Number Format", numberFormat); // E.g. "D2" for 2 digits
            }

            GUILayout.Space(20);

            // Botón para aplicar renombrado
            if (GUILayout.Button("Rename"))
            {
                selectedObjects.Clear();
                processedGameObjects.Clear(); // Limpiar objetos ya procesados

                if (renameInScene)
                    selectedObjects.AddRange(Selection.gameObjects);
                if (renameInProject)
                    selectedObjects.AddRange(Selection.objects);

                ApplyRenaming();
            }
        }

        private static void ApplyRenaming()
        {
            int counter = startingNumber;

            foreach (var obj in selectedObjects)
            {
                if (obj == null) continue;

                // Renombrado en escena
                if (obj is GameObject gameObject)
                {
                    RenameGameObject(gameObject, ref counter);
                    if (includeChildren)
                    {
                        foreach (Transform child in gameObject.transform)
                        {
                            RenameGameObject(child.gameObject, ref counter);
                        }
                    }
                }

                // Renombrado en proyecto
                if (AssetDatabase.Contains(obj))
                {
                    RenameAsset(obj, ref counter);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RenameGameObject(GameObject gameObject, ref int counter)
        {
            // Verificar si ya fue procesado para evitar cambios dobles
            if (processedGameObjects.Contains(gameObject)) return;

            string originalName = gameObject.name;
            string newName = GenerateNewName(originalName, ref counter);
            gameObject.name = newName;

            // Marcar como procesado
            processedGameObjects.Add(gameObject);
        }

        private static void RenameAsset(Object asset, ref int counter)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            string originalName = Path.GetFileNameWithoutExtension(path);
            string newName = GenerateNewName(originalName, ref counter);

            string newPath = Path.Combine(Path.GetDirectoryName(path), newName + Path.GetExtension(path));

            AssetDatabase.RenameAsset(path, newName);
        }

        private static string GenerateNewName(string originalName, ref int counter)
        {
            string newName = originalName;

            // Aplicar prefijo
            if (!string.IsNullOrEmpty(prefix))
            {
                newName = prefix + newName;
            }

            // Aplicar sufijo
            if (!string.IsNullOrEmpty(suffix))
            {
                newName += suffix;
            }

            // Buscar y reemplazar
            if (!string.IsNullOrEmpty(searchFor))
            {
                newName = newName.Replace(searchFor, replaceWith);
            }

            // Convertir a mayúsculas o minúsculas
            if (useUpperCase)
            {
                newName = newName.ToUpper();
            }
            else if (useLowerCase)
            {
                newName = newName.ToLower();
            }

            // Aplicar numeración secuencial
            if (applySequentialNumbers)
            {
                newName += counter.ToString(numberFormat);
                counter++;
            }

            return newName;
        }
    }
}
