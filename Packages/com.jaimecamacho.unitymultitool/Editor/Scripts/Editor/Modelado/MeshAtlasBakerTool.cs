using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class MeshAtlasBakerTool
    {
        private static readonly int[] atlasSizes = { 512, 1024, 2048, 4096 };
        private static readonly string[] atlasSizeLabels = { "512", "1024", "2048", "4096" };

        private static int atlasSizeIndex = 2;
        private static int atlasPadding = 8;
        private static bool saveAtlasTexture = true;
        private static bool saveMaterialAsset = true;
        private static bool saveMeshAsset = true;
        private static string outputName = "CombinedAtlas";
        private static DefaultAsset outputFolder;
        private static Vector2 materialScroll;

        public static void DrawTool()
        {
            GUILayout.Label("Atlasizador de materiales", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Convierte mallas combinadas con múltiples materiales en un único material con atlas.", MessageType.Info);

            GameObject active = Selection.activeGameObject;
            if (active == null)
            {
                EditorGUILayout.HelpBox("Selecciona un objeto con MeshRenderer para continuar.", MessageType.Warning);
                return;
            }

            MeshRenderer renderer = active.GetComponent<MeshRenderer>();
            MeshFilter filter = active.GetComponent<MeshFilter>();
            if (renderer == null || filter == null)
            {
                EditorGUILayout.HelpBox("El objeto activo necesita MeshRenderer y MeshFilter.", MessageType.Warning);
                return;
            }

            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                EditorGUILayout.HelpBox("El MeshFilter no tiene mesh asignado.", MessageType.Warning);
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.HelpBox("No se detectaron materiales en el renderer.", MessageType.Warning);
                return;
            }

            if (materials.Length == 1)
            {
                EditorGUILayout.HelpBox("Este renderer ya utiliza un único material.", MessageType.Info);
            }

            if (mesh.subMeshCount != materials.Length)
            {
                EditorGUILayout.HelpBox("El número de submeshes no coincide con la cantidad de materiales. El resultado puede no ser el esperado.", MessageType.Warning);
            }

            if (mesh.uv == null || mesh.uv.Length == 0)
            {
                EditorGUILayout.HelpBox("La malla necesita UVs en el canal principal para generar el atlas.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Materiales detectados", materials.Length.ToString());
            EditorGUILayout.LabelField("Submeshes", mesh.subMeshCount.ToString());

            materialScroll = EditorGUILayout.BeginScrollView(materialScroll, GUILayout.Height(120f));
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                string matName = material != null ? material.name : "(Material null)";
                string textureInfo = "Sin textura";
                if (material != null)
                {
                    Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    if (baseMap == null)
                    {
                        baseMap = material.mainTexture;
                    }
                    if (baseMap != null)
                    {
                        textureInfo = $"{baseMap.width}x{baseMap.height}";
                    }
                }

                EditorGUILayout.LabelField($"• Submesh {i + 1}: {matName} ({textureInfo})", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            GUILayout.Space(6f);

            atlasSizeIndex = GUILayout.SelectionGrid(atlasSizeIndex, atlasSizeLabels, atlasSizeLabels.Length, EditorStyles.miniButton);
            atlasSizeIndex = Mathf.Clamp(atlasSizeIndex, 0, atlasSizes.Length - 1);
            atlasPadding = EditorGUILayout.IntSlider("Padding", atlasPadding, 0, 64);

            saveAtlasTexture = EditorGUILayout.ToggleLeft("Guardar atlas como PNG", saveAtlasTexture);
            using (new EditorGUI.DisabledScope(!saveAtlasTexture))
            {
                bool newMaterialAsset = EditorGUILayout.ToggleLeft("Guardar material como asset", saveMaterialAsset);
                saveMaterialAsset = saveAtlasTexture ? newMaterialAsset : false;
            }
            saveMeshAsset = EditorGUILayout.ToggleLeft("Guardar malla resultante como asset", saveMeshAsset);

            using (new EditorGUI.IndentLevelScope())
            {
                outputName = EditorGUILayout.TextField("Nombre base", outputName);
                using (new EditorGUI.DisabledScope(!(saveAtlasTexture || saveMaterialAsset || saveMeshAsset)))
                {
                    outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Carpeta destino", outputFolder, typeof(DefaultAsset), false);
                }
                if (outputFolder == null)
                {
                    EditorGUILayout.HelpBox("Si no se asigna carpeta se usará 'Assets/'.", MessageType.Info);
                }
            }

            if (!saveAtlasTexture)
            {
                EditorGUILayout.HelpBox("El atlas y el material permanecerán en memoria hasta que guardes la escena o exportes manualmente.", MessageType.Info);
            }

            GUILayout.Space(10f);

            bool canGenerateAtlas = HasMultipleMaterials(materials) || mesh.subMeshCount > 1;

            if (!canGenerateAtlas)
            {
                EditorGUILayout.HelpBox("Se requieren al menos dos materiales o submeshes para generar el atlas.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!canGenerateAtlas))
            {
                if (GUILayout.Button("Generar atlas y material único"))
                {
                    ConvertSelection(renderer, filter, mesh, materials);
                }
            }
        }

        private static bool HasMultipleMaterials(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
            {
                return false;
            }

            int uniqueCount = 0;
            HashSet<Material> uniqueMaterials = new HashSet<Material>();
            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (uniqueMaterials.Add(material))
                {
                    uniqueCount++;
                    if (uniqueCount > 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ConvertSelection(MeshRenderer renderer, MeshFilter filter, Mesh mesh, Material[] materials)
        {
            List<Texture2D> tempGenerated = new List<Texture2D>();
            try
            {
                EditorUtility.DisplayProgressBar("Material Atlas", "Preparando texturas...", 0.1f);

                List<Texture2D> textureCopies = new List<Texture2D>(materials.Length);
                List<string> issues = new List<string>();
                string baseAssetName = string.IsNullOrWhiteSpace(outputName) ? "CombinedAtlas" : outputName;

                for (int i = 0; i < materials.Length; i++)
                {
                    Texture2D texture = ExtractTexture(materials[i], out string note);
                    bool createdTexture = false;
                    if (!string.IsNullOrEmpty(note))
                    {
                        issues.Add(note);
                    }

                    if (texture == null)
                    {
                        texture = CreateSolidTexture(Color.white);
                        createdTexture = true;
                        issues.Add($"Submesh {i + 1}: no se encontró textura, se usará un color blanco.");
                    }
                    else if (!texture.isReadable)
                    {
                        Texture2D readable = CreateReadableCopy(texture);
                        if (readable != null)
                        {
                            texture = readable;
                            createdTexture = true;
                        }
                        else
                        {
                            Texture2D fallback = CreateSolidTexture(Color.white);
                            texture = fallback;
                            createdTexture = true;
                            issues.Add($"Submesh {i + 1}: no se pudo leer la textura, se usará un color blanco.");
                        }
                    }
                    else
                    {
                        Texture2D duplicate = DuplicateTexture(texture);
                        if (duplicate != null)
                        {
                            texture = duplicate;
                            createdTexture = true;
                        }
                    }

                    if (createdTexture && texture != null)
                    {
                        tempGenerated.Add(texture);
                    }

                    textureCopies.Add(texture);
                }

                EditorUtility.DisplayProgressBar("Material Atlas", "Empaquetando texturas...", 0.35f);

                Texture2D atlasTexture = new Texture2D(atlasSizes[atlasSizeIndex], atlasSizes[atlasSizeIndex], TextureFormat.RGBA32, false);
                Rect[] rects = atlasTexture.PackTextures(textureCopies.ToArray(), atlasPadding, atlasSizes[atlasSizeIndex], false);

                EditorUtility.DisplayProgressBar("Material Atlas", "Reasignando UVs...", 0.6f);

                Mesh atlasMesh = BuildAtlasMesh(mesh, rects);
                if (atlasMesh == null)
                {
                    EditorUtility.DisplayDialog("Material Atlas", "No se pudo generar la malla con el nuevo atlas.", "Entendido");
                    return;
                }
                atlasMesh.name = baseAssetName + "_AtlasMesh";

                string folderPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : "Assets";
                if (string.IsNullOrEmpty(folderPath))
                {
                    folderPath = "Assets";
                }

                Texture2D finalAtlas = atlasTexture;
                string atlasPath = string.Empty;
                if (saveAtlasTexture)
                {
                    EditorUtility.DisplayProgressBar("Material Atlas", "Guardando atlas...", 0.75f);
                    atlasPath = SaveTextureAsset(atlasTexture, folderPath, baseAssetName);
                    if (!string.IsNullOrEmpty(atlasPath))
                    {
                        finalAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                        UnityEngine.Object.DestroyImmediate(atlasTexture);
                    }
                    else
                    {
                        issues.Add("No se pudo guardar el atlas en disco, se mantendrá en memoria.");
                    }
                }

                if (saveMeshAsset)
                {
                    EditorUtility.DisplayProgressBar("Material Atlas", "Guardando malla...", 0.85f);
                    SaveMeshAsset(atlasMesh, folderPath, baseAssetName);
                }

                Material sourceMaterial = materials[0];
                Material atlasMaterial = sourceMaterial != null ? new Material(sourceMaterial) : new Material(Shader.Find("Standard"));
                atlasMaterial.name = baseAssetName + "_Mat";

                if (finalAtlas != null)
                {
                    ApplyTextureToMaterial(atlasMaterial, finalAtlas);
                }

                if (saveMaterialAsset)
                {
                    EditorUtility.DisplayProgressBar("Material Atlas", "Guardando material...", 0.9f);
                    string materialPath = SaveMaterialAsset(atlasMaterial, folderPath, baseAssetName);
                    if (!string.IsNullOrEmpty(materialPath))
                    {
                        atlasMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    }
                    else
                    {
                        issues.Add("No se pudo guardar el material en disco.");
                    }
                }

                if ((saveAtlasTexture && !string.IsNullOrEmpty(atlasPath)) || saveMeshAsset || saveMaterialAsset)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();

                Undo.RecordObject(filter, "Asignar mesh atlaseado");
                Undo.RecordObject(renderer, "Asignar material atlaseado");

                filter.sharedMesh = atlasMesh;
                renderer.sharedMaterials = new[] { atlasMaterial };

                Undo.CollapseUndoOperations(group);

                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(renderer);

                if (issues.Count > 0)
                {
                    string summary = string.Join("\n", issues);
                    EditorUtility.DisplayDialog("Material Atlas", "Proceso completado con advertencias:\n\n" + summary, "Entendido");
                }
                else
                {
                    EditorUtility.DisplayDialog("Material Atlas", "Materiales combinados exitosamente en un atlas único.", "Listo");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("MeshAtlasBakerTool: " + ex.Message + "\n" + ex.StackTrace);
                EditorUtility.DisplayDialog("Material Atlas", "Ocurrió un error durante el proceso. Revisa la consola.", "Entendido");
            }
            finally
            {
                foreach (Texture2D generated in tempGenerated)
                {
                    if (generated != null)
                    {
                        UnityEngine.Object.DestroyImmediate(generated);
                    }
                }
                tempGenerated.Clear();
                EditorUtility.ClearProgressBar();
            }
        }

        private static Texture2D ExtractTexture(Material material, out string note)
        {
            note = string.Empty;
            if (material == null)
            {
                note = "Material nulo detectado, se usará un color blanco.";
                return null;
            }

            Texture texture = null;
            if (material.HasProperty("_BaseMap"))
            {
                texture = material.GetTexture("_BaseMap");
            }

            if (texture == null && material.HasProperty("_MainTex"))
            {
                texture = material.GetTexture("_MainTex");
            }

            if (texture == null)
            {
                return null;
            }

            if (texture is Texture2D tex2D)
            {
                return tex2D;
            }

            note = "Textura no es Texture2D, se usará un renderizado temporal.";
            return CreateReadableCopy(texture);
        }

        private static Texture2D CreateReadableCopy(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(texture, temporary);
            RenderTexture.active = temporary;

            Texture2D copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            return copy;
        }

        private static Texture2D DuplicateTexture(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            Texture2D duplicate = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            duplicate.SetPixels(source.GetPixels());
            duplicate.Apply();
            return duplicate;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static Mesh BuildAtlasMesh(Mesh sourceMesh, IReadOnlyList<Rect> rects)
        {
            if (sourceMesh == null)
            {
                return null;
            }

            Vector3[] vertices = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Color[] colors = sourceMesh.colors;
            Color32[] colors32 = sourceMesh.colors32;

            Vector2[] uv = sourceMesh.uv;
            if (uv == null || uv.Length == 0)
            {
                return null;
            }

            List<List<Vector4>> uvChannels = new List<List<Vector4>>();
            for (int channel = 1; channel < 8; channel++)
            {
                List<Vector4> data = new List<Vector4>();
                sourceMesh.GetUVs(channel, data);
                uvChannels.Add(data.Count > 0 ? data : null);
            }

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = normals is { Length: > 0 } ? new List<Vector3>() : null;
            List<Vector4> newTangents = tangents is { Length: > 0 } ? new List<Vector4>() : null;
            List<Color> newColors = colors is { Length: > 0 } ? new List<Color>() : null;
            List<Color32> newColors32 = colors32 is { Length: > 0 } ? new List<Color32>() : null;
            List<Vector2> newUV = new List<Vector2>();
            List<List<Vector4>> newUVChannels = new List<List<Vector4>>(uvChannels.Count);
            foreach (List<Vector4> channel in uvChannels)
            {
                newUVChannels.Add(channel != null ? new List<Vector4>() : null);
            }

            List<int> triangles = new List<int>();
            int submeshCount = Mathf.Min(sourceMesh.subMeshCount, rects.Count);
            for (int submesh = 0; submesh < submeshCount; submesh++)
            {
                Rect rect = rects[submesh];
                int[] subTriangles = sourceMesh.GetTriangles(submesh);
                for (int i = 0; i < subTriangles.Length; i++)
                {
                    int originalIndex = subTriangles[i];

                    newVertices.Add(vertices[originalIndex]);
                    if (newNormals != null)
                    {
                        newNormals.Add(normals[originalIndex]);
                    }
                    if (newTangents != null)
                    {
                        newTangents.Add(tangents[originalIndex]);
                    }
                    if (newColors != null)
                    {
                        newColors.Add(colors[originalIndex]);
                    }
                    if (newColors32 != null)
                    {
                        newColors32.Add(colors32[originalIndex]);
                    }

                    Vector2 uvCoord = uv[originalIndex];
                    uvCoord.x = rect.x + uvCoord.x * rect.width;
                    uvCoord.y = rect.y + uvCoord.y * rect.height;
                    newUV.Add(uvCoord);

                    for (int channel = 0; channel < newUVChannels.Count; channel++)
                    {
                        List<Vector4> channelData = newUVChannels[channel];
                        if (channelData != null)
                        {
                            Vector4 uvValue = uvChannels[channel][originalIndex];
                            channelData.Add(uvValue);
                        }
                    }

                    triangles.Add(newVertices.Count - 1);
                }
            }

            Mesh atlasMesh = new Mesh
            {
                name = sourceMesh.name + "_Atlas",
                indexFormat = sourceMesh.indexFormat
            };

            atlasMesh.SetVertices(newVertices);
            atlasMesh.SetTriangles(triangles, 0);
            atlasMesh.SetUVs(0, newUV);

            if (newNormals != null && newNormals.Count == newVertices.Count)
            {
                atlasMesh.SetNormals(newNormals);
            }
            if (newTangents != null && newTangents.Count == newVertices.Count)
            {
                atlasMesh.SetTangents(newTangents);
            }
            if (newColors != null && newColors.Count == newVertices.Count)
            {
                atlasMesh.SetColors(newColors);
            }
            if (newColors32 != null && newColors32.Count == newVertices.Count)
            {
                atlasMesh.colors32 = newColors32.ToArray();
            }

            for (int channel = 0; channel < newUVChannels.Count; channel++)
            {
                List<Vector4> channelData = newUVChannels[channel];
                if (channelData != null && channelData.Count == newVertices.Count)
                {
                    atlasMesh.SetUVs(channel + 1, channelData);
                }
            }

            atlasMesh.RecalculateBounds();
            return atlasMesh;
        }

        private static void ApplyTextureToMaterial(Material material, Texture2D texture)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static string SaveTextureAsset(Texture2D texture, string folderPath, string baseName)
        {
            try
            {
                string fileName = baseName + "_Atlas.png";
                string path = Path.Combine(folderPath, fileName);
                path = AssetDatabase.GenerateUniqueAssetPath(path);

                byte[] pngData = texture.EncodeToPNG();
                if (pngData == null)
                {
                    Debug.LogError("MeshAtlasBakerTool: EncodeToPNG devolvió null.");
                    return string.Empty;
                }

                File.WriteAllBytes(path, pngData);
                AssetDatabase.ImportAsset(path);

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.sRGBTexture = true;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError("MeshAtlasBakerTool: Error guardando textura - " + ex.Message);
                return string.Empty;
            }
        }

        private static void SaveMeshAsset(Mesh mesh, string folderPath, string baseName)
        {
            if (mesh == null)
            {
                return;
            }

            string fileName = baseName + "_AtlasMesh.asset";
            string path = Path.Combine(folderPath, fileName);
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        private static string SaveMaterialAsset(Material material, string folderPath, string baseName)
        {
            if (material == null)
            {
                return string.Empty;
            }

            string fileName = baseName + "_AtlasMat.mat";
            string path = Path.Combine(folderPath, fileName);
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(material, path);
            return path;
        }
    }
}
