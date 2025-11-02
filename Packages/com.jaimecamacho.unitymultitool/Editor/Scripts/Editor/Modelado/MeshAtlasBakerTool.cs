using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

        private static bool useCustomAtlasWorkflow;
        private static bool showCustomAtlasBuilder = true;
        private static int customAtlasImageCount = 1;
        private static readonly List<Texture2D> customAtlasSourceTextures = new List<Texture2D>();
        private static readonly string[] customAtlasResolutionLabels = { "256", "512", "1024", "2048" };
        private static readonly int[] customAtlasResolutionSizes = { 256, 512, 1024, 2048 };
        private static int customAtlasCellResolution = 1024;
        private static Texture2D customGeneratedAtlas;
        private static Texture2D customAtlasTexture;
        private static Vector2 customUvPosition = Vector2.zero;
        private static Vector2 customUvScale = Vector2.one;
        private static float customUvRotation = 0f;
        private static bool customLockUniformScale = true;
        private static string customStatusMessage = string.Empty;
        private static MessageType customStatusType = MessageType.Info;

        public static void DrawTool()
        {
            GUILayout.Label("Atlasizador de materiales", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Convierte mallas combinadas con múltiples materiales en un único material con atlas.", MessageType.Info);

            if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox("Selecciona al menos un objeto con MeshRenderer para continuar.", MessageType.Warning);
                return;
            }

            SelectionContext context = BuildSelectionContext(out string selectionMessage, out MessageType messageType);
            if (context == null)
            {
                EditorGUILayout.HelpBox(selectionMessage, messageType);
                return;
            }

            Material[] materials = context.MaterialArray;
            if (materials == null || materials.Length == 0)
            {
                EditorGUILayout.HelpBox("No se detectaron materiales en los MeshRenderers seleccionados.", MessageType.Warning);
                return;
            }

            if (!context.RequiresTemporaryMesh)
            {
                Mesh mesh = context.TargetFilter.sharedMesh;
                if (mesh == null)
                {
                    EditorGUILayout.HelpBox("El MeshFilter no tiene mesh asignado.", MessageType.Warning);
                    return;
                }

                if (mesh.uv == null || mesh.uv.Length == 0)
                {
                    EditorGUILayout.HelpBox("La malla necesita UVs en el canal principal para generar el atlas.", MessageType.Error);
                    return;
                }

                if (mesh.subMeshCount != materials.Length)
                {
                    EditorGUILayout.HelpBox("El número de submeshes no coincide con la cantidad de materiales. El resultado puede no ser el esperado.", MessageType.Warning);
                }
            }
            else if (context.SubMeshCount == 0)
            {
                EditorGUILayout.HelpBox("No se detectaron submeshes válidos en la selección.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("MeshRenderers detectados", context.Renderers.Count.ToString());
            EditorGUILayout.LabelField("Submeshes combinados", context.SubMeshCount.ToString());
            EditorGUILayout.LabelField("Materiales totales", materials.Length.ToString());
            EditorGUILayout.LabelField("Materiales únicos", context.UniqueMaterialCount.ToString());
            EditorGUILayout.LabelField("Vértices estimados", context.VertexCount.ToString());

            if (!HasMultipleMaterials(materials) && context.SubMeshCount <= 1)
            {
                EditorGUILayout.HelpBox("La selección ya utiliza un único material.", MessageType.Info);
            }

            if (context.RequiresTemporaryMesh)
            {
                string infoMessage = $"Se combinarán temporalmente {context.Renderers.Count} MeshRenderers y el resultado se aplicará al objeto activo '{context.TargetRenderer.name}'.";
                EditorGUILayout.HelpBox(infoMessage, MessageType.Info);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Objetos incluidos:");
                    EditorGUI.indentLevel++;
                    List<string> samples = context.GetRendererSamples();
                    for (int i = 0; i < samples.Count; i++)
                    {
                        EditorGUILayout.LabelField("• " + samples[i], EditorStyles.miniLabel);
                    }

                    if (context.Renderers.Count > samples.Count)
                    {
                        EditorGUILayout.LabelField($"…{context.Renderers.Count - samples.Count} adicionales", EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            if (context.Skipped.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Omitidos:");
                    EditorGUI.indentLevel++;
                    foreach (string skipped in context.Skipped)
                    {
                        EditorGUILayout.LabelField("• " + skipped, EditorStyles.miniLabel);
                    }
                    EditorGUI.indentLevel--;
                }
            }

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

            DrawCustomAtlasWorkflowControls();

            if (!saveAtlasTexture)
            {
                EditorGUILayout.HelpBox("El atlas y el material permanecerán en memoria hasta que guardes la escena o exportes manualmente.", MessageType.Info);
            }

            GUILayout.Space(10f);

            bool canGenerateAtlas = HasMultipleMaterials(materials) || context.SubMeshCount > 1;

            if (!canGenerateAtlas)
            {
                EditorGUILayout.HelpBox("Se requieren al menos dos materiales o submeshes para generar el atlas.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!canGenerateAtlas))
            {
                if (GUILayout.Button("Generar atlas y material único"))
                {
                    ConvertSelection(context);
                }
            }
        }

        private static void DrawCustomAtlasWorkflowControls()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Flujo manual de atlas (VAT UV Visual)", EditorStyles.boldLabel);
            bool newToggle = EditorGUILayout.ToggleLeft("Usar constructor manual de atlas y transformaciones UV", useCustomAtlasWorkflow);
            if (newToggle != useCustomAtlasWorkflow)
            {
                useCustomAtlasWorkflow = newToggle;
                if (!useCustomAtlasWorkflow)
                {
                    ClearCustomStatus();
                }
            }

            if (!useCustomAtlasWorkflow)
            {
                return;
            }

            if (!string.IsNullOrEmpty(customStatusMessage))
            {
                EditorGUILayout.HelpBox(customStatusMessage, customStatusType);
            }

            showCustomAtlasBuilder = EditorGUILayout.BeginFoldoutHeaderGroup(showCustomAtlasBuilder, "Atlas personalizado");
            if (showCustomAtlasBuilder)
            {
                EditorGUILayout.HelpBox("Combina varias texturas en una cuadrícula uniforme para construir un atlas de referencia similar al flujo VAT UV Visual.", MessageType.None);

                int previousCount = customAtlasImageCount;
                customAtlasImageCount = EditorGUILayout.IntSlider("Número de imágenes", customAtlasImageCount, 1, 16);
                if (customAtlasImageCount != previousCount)
                {
                    EnsureCustomAtlasSourceListSize();
                }
                EnsureCustomAtlasSourceListSize();

                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = 0; i < customAtlasSourceTextures.Count; i++)
                    {
                        customAtlasSourceTextures[i] = (Texture2D)EditorGUILayout.ObjectField($"Imagen {i + 1}", customAtlasSourceTextures[i], typeof(Texture2D), false);
                    }
                }

                customAtlasCellResolution = EditorGUILayout.IntPopup("Resolución por imagen", customAtlasCellResolution, customAtlasResolutionLabels, customAtlasResolutionSizes);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Generar atlas"))
                    {
                        GenerateCustomAtlasTexture();
                    }

                    using (new EditorGUI.DisabledScope(customGeneratedAtlas == null))
                    {
                        if (GUILayout.Button("Asignar atlas generado"))
                        {
                            customAtlasTexture = customGeneratedAtlas;
                            SetCustomStatus($"Atlas asignado ({customGeneratedAtlas.width}x{customGeneratedAtlas.height}).", MessageType.Info);
                        }
                    }
                }

                if (customGeneratedAtlas != null)
                {
                    Rect previewRect = GUILayoutUtility.GetRect(100f, 140f, GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.DrawTexture(previewRect, customGeneratedAtlas, ScaleMode.ScaleToFit);
                    }
                    EditorGUILayout.LabelField($"Atlas generado: {customGeneratedAtlas.width}x{customGeneratedAtlas.height}", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            customAtlasTexture = (Texture2D)EditorGUILayout.ObjectField("Atlas en uso", customAtlasTexture, typeof(Texture2D), false);
            if (customAtlasTexture == null)
            {
                EditorGUILayout.HelpBox("Asigna un atlas generado o existente para utilizarlo durante la conversión.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"Dimensiones del atlas: {customAtlasTexture.width}x{customAtlasTexture.height}", EditorStyles.miniLabel);
            }

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Transformación UV", EditorStyles.boldLabel);

            customUvPosition = EditorGUILayout.Vector2Field("Posición", customUvPosition);

            EditorGUILayout.BeginHorizontal();
            Vector2 newScale = EditorGUILayout.Vector2Field("Escala", customUvScale);
            bool newLock = GUILayout.Toggle(customLockUniformScale, new GUIContent("Uniforme"), "Button", GUILayout.Width(90f));
            if (!customLockUniformScale && newLock)
            {
                float uniform = Mathf.Max(0.01f, (newScale.x + newScale.y) * 0.5f);
                newScale = new Vector2(uniform, uniform);
            }
            if (newLock)
            {
                float uniform = Mathf.Max(0.01f, newScale.x);
                newScale = new Vector2(uniform, uniform);
            }
            EditorGUILayout.EndHorizontal();

            newScale.x = Mathf.Clamp(newScale.x, 0.01f, 100f);
            newScale.y = Mathf.Clamp(newScale.y, 0.01f, 100f);

            customUvScale = newScale;
            customLockUniformScale = newLock;

            customUvRotation = EditorGUILayout.Slider("Rotación", customUvRotation, -360f, 360f);

            EditorGUILayout.HelpBox("La transformación se aplicará a la malla combinada antes de guardar el atlas, permitiendo alinear UV de forma similar a la herramienta VAT UV Visual.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Restablecer transformaciones"))
                {
                    ResetCustomUvTransform();
                }

                if (GUILayout.Button("Limpiar mensaje"))
                {
                    ClearCustomStatus();
                }
            }
        }

        private static void EnsureCustomAtlasSourceListSize()
        {
            if (customAtlasImageCount < 1)
            {
                customAtlasImageCount = 1;
            }

            while (customAtlasSourceTextures.Count < customAtlasImageCount)
            {
                customAtlasSourceTextures.Add(null);
            }

            while (customAtlasSourceTextures.Count > customAtlasImageCount)
            {
                customAtlasSourceTextures.RemoveAt(customAtlasSourceTextures.Count - 1);
            }
        }

        private static void GenerateCustomAtlasTexture()
        {
            EnsureCustomAtlasSourceListSize();

            if (customAtlasSourceTextures.Count == 0)
            {
                SetCustomStatus("Asigna al menos una imagen para generar el atlas.", MessageType.Warning);
                return;
            }

            List<Texture2D> readableCopies = new List<Texture2D>();
            List<Texture2D> sources = new List<Texture2D>(customAtlasSourceTextures.Count);

            try
            {
                for (int i = 0; i < customAtlasSourceTextures.Count; i++)
                {
                    Texture2D source = customAtlasSourceTextures[i];
                    if (source == null)
                    {
                        SetCustomStatus("Todos los espacios de imagen deben estar asignados antes de generar el atlas.", MessageType.Warning);
                        return;
                    }

                    Texture2D readable = source;
                    if (!source.isReadable)
                    {
                        readable = CreateReadableCopy(source);
                        if (readable == null)
                        {
                            SetCustomStatus($"No se pudo crear una copia legible de '{source.name}'.", MessageType.Error);
                            return;
                        }
                        readableCopies.Add(readable);
                    }
                    sources.Add(readable);
                }

                int cellResolution = Mathf.Max(1, customAtlasCellResolution);
                int textureCount = sources.Count;
                int columns = Mathf.CeilToInt(Mathf.Sqrt(textureCount));
                int rows = Mathf.CeilToInt((float)textureCount / columns);
                int atlasWidth = Mathf.Max(1, columns * cellResolution);
                int atlasHeight = Mathf.Max(1, rows * cellResolution);

                Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false)
                {
                    name = "CustomAtlas",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                Color32[] clearPixels = new Color32[atlasWidth * atlasHeight];
                for (int i = 0; i < clearPixels.Length; i++)
                {
                    clearPixels[i] = new Color32(0, 0, 0, 0);
                }
                atlas.SetPixels32(clearPixels);

                for (int index = 0; index < textureCount; index++)
                {
                    int column = index % columns;
                    int row = index / columns;
                    int offsetX = column * cellResolution;
                    int offsetY = row * cellResolution;

                    CopyTextureToAtlas(sources[index], atlas, offsetX, offsetY, cellResolution);
                }

                atlas.Apply();

                if (customGeneratedAtlas != null)
                {
                    UnityEngine.Object.DestroyImmediate(customGeneratedAtlas);
                }

                customGeneratedAtlas = atlas;
                customAtlasTexture = customGeneratedAtlas;
                SetCustomStatus($"Atlas generado correctamente ({atlas.width}x{atlas.height}).", MessageType.Info);
            }
            finally
            {
                foreach (Texture2D copy in readableCopies)
                {
                    if (copy != null && copy != customGeneratedAtlas)
                    {
                        UnityEngine.Object.DestroyImmediate(copy);
                    }
                }
            }
        }

        private static void CopyTextureToAtlas(Texture2D source, Texture2D atlas, int offsetX, int offsetY, int targetResolution)
        {
            if (source == null || atlas == null)
            {
                return;
            }

            int maxX = Mathf.Min(targetResolution, atlas.width - offsetX);
            int maxY = Mathf.Min(targetResolution, atlas.height - offsetY);

            for (int y = 0; y < maxY; y++)
            {
                float v = targetResolution > 1 ? (float)y / (targetResolution - 1) : 0f;

                for (int x = 0; x < maxX; x++)
                {
                    float u = targetResolution > 1 ? (float)x / (targetResolution - 1) : 0f;
                    Color sampled = source.GetPixelBilinear(u, v);
                    atlas.SetPixel(offsetX + x, offsetY + y, sampled);
                }
            }
        }

        private static void ResetCustomUvTransform()
        {
            customUvPosition = Vector2.zero;
            customUvScale = Vector2.one;
            customUvRotation = 0f;
        }

        private static void SetCustomStatus(string message, MessageType type)
        {
            customStatusMessage = message;
            customStatusType = type;
        }

        private static void ClearCustomStatus()
        {
            customStatusMessage = string.Empty;
        }

        private static bool TryBuildCustomAtlas(Mesh workingMesh, string baseAssetName, List<string> issues, out Mesh atlasMesh, out Texture2D atlasTexture)
        {
            atlasMesh = null;
            atlasTexture = null;

            if (workingMesh == null)
            {
                EditorUtility.DisplayDialog("Material Atlas", "No se encontró una malla válida para aplicar el atlas personalizado.", "Entendido");
                return false;
            }

            if (customAtlasTexture == null)
            {
                SetCustomStatus("Genera o asigna un atlas personalizado antes de ejecutar la conversión.", MessageType.Warning);
                return false;
            }

            Texture2D atlasCopy;
            if (customAtlasTexture.isReadable)
            {
                atlasCopy = DuplicateTexture(customAtlasTexture) ?? customAtlasTexture;
            }
            else
            {
                atlasCopy = CreateReadableCopy(customAtlasTexture);
                if (atlasCopy == null)
                {
                    EditorUtility.DisplayDialog("Material Atlas", "No se pudo crear una copia legible del atlas personalizado.", "Entendido");
                    return false;
                }
                issues?.Add("El atlas personalizado no era legible, se utilizó una copia temporal en memoria.");
            }

            if (atlasCopy != null)
            {
                atlasCopy.wrapMode = TextureWrapMode.Clamp;
                atlasCopy.filterMode = FilterMode.Bilinear;
            }

            Mesh duplicatedMesh = UnityEngine.Object.Instantiate(workingMesh);
            if (duplicatedMesh == null)
            {
                EditorUtility.DisplayDialog("Material Atlas", "No se pudo duplicar la malla para aplicar el atlas personalizado.", "Entendido");
                if (atlasCopy != customAtlasTexture)
                {
                    UnityEngine.Object.DestroyImmediate(atlasCopy);
                }
                return false;
            }

            duplicatedMesh.name = baseAssetName + "_AtlasMesh";
            ApplyCustomUvTransform(duplicatedMesh);
            duplicatedMesh.RecalculateBounds();

            atlasMesh = duplicatedMesh;
            atlasTexture = atlasCopy;
            return true;
        }

        private static void ApplyCustomUvTransform(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            Vector2[] uv = mesh.uv;
            if (uv == null || uv.Length == 0)
            {
                return;
            }

            Vector2[] transformed = new Vector2[uv.Length];
            Matrix4x4 transformMatrix = Matrix4x4.TRS(customUvPosition, Quaternion.Euler(0f, 0f, customUvRotation), new Vector3(customUvScale.x, customUvScale.y, 1f));

            for (int i = 0; i < uv.Length; i++)
            {
                Vector3 uvPoint = new Vector3(uv[i].x, uv[i].y, 0f);
                Vector3 result = transformMatrix.MultiplyPoint3x4(uvPoint);
                transformed[i] = new Vector2(result.x, result.y);
            }

            mesh.uv = transformed;
        }

        private static SelectionContext BuildSelectionContext(out string message, out MessageType messageType)
        {
            message = string.Empty;
            messageType = MessageType.None;

            GameObject active = Selection.activeGameObject;
            if (active == null)
            {
                message = "Selecciona un objeto con MeshRenderer para continuar.";
                messageType = MessageType.Warning;
                return null;
            }

            MeshRenderer targetRenderer = active.GetComponent<MeshRenderer>();
            MeshFilter targetFilter = active.GetComponent<MeshFilter>();
            if (targetRenderer == null || targetFilter == null)
            {
                message = "El objeto activo necesita MeshRenderer y MeshFilter.";
                messageType = MessageType.Warning;
                return null;
            }

            SelectionContext context = new SelectionContext(targetRenderer, targetFilter);
            HashSet<int> processed = new HashSet<int>();
            bool skippedForUv = false;

            foreach (GameObject root in Selection.gameObjects)
            {
                if (root == null)
                {
                    continue;
                }

                MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer renderer in renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (!processed.Add(renderer.GetInstanceID()))
                    {
                        continue;
                    }

                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null)
                    {
                        context.Skipped.Add(renderer.name + " (sin MeshFilter)");
                        continue;
                    }

                    Mesh sharedMesh = filter.sharedMesh;
                    if (sharedMesh == null)
                    {
                        context.Skipped.Add(renderer.name + " (mesh vacío)");
                        continue;
                    }

                    if (sharedMesh.subMeshCount == 0)
                    {
                        context.Skipped.Add(renderer.name + " (sin submeshes)");
                        continue;
                    }

                    if (sharedMesh.uv == null || sharedMesh.uv.Length == 0)
                    {
                        context.Skipped.Add(renderer.name + " (sin UVs en canal principal)");
                        skippedForUv = true;
                        continue;
                    }

                    context.Renderers.Add(renderer);
                    context.SubMeshCount += sharedMesh.subMeshCount;
                    context.VertexCount += sharedMesh.vertexCount;

                    Material[] rendererMaterials = renderer.sharedMaterials;
                    for (int i = 0; i < sharedMesh.subMeshCount; i++)
                    {
                        Material material = rendererMaterials != null && rendererMaterials.Length > 0
                            ? rendererMaterials[Mathf.Min(i, rendererMaterials.Length - 1)]
                            : null;
                        context.AddMaterial(material);
                    }
                }
            }

            if (context.Renderers.Count == 0)
            {
                message = skippedForUv
                    ? "Todos los MeshRenderers seleccionados fueron omitidos porque no tienen UVs en el canal principal."
                    : "No se encontraron MeshRenderers válidos en la selección.";
                messageType = MessageType.Warning;
                return null;
            }

            context.FinalizeData();
            return context;
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

        private static void ConvertSelection(SelectionContext context)
        {
            if (context == null)
            {
                return;
            }

            MeshRenderer renderer = context.TargetRenderer;
            MeshFilter filter = context.TargetFilter;

            Mesh workingMesh;
            Material[] materials;
            bool disposeWorkingMesh = false;

            if (!context.RequiresTemporaryMesh)
            {
                workingMesh = filter.sharedMesh;
                materials = renderer.sharedMaterials;
            }
            else
            {
                if (!TryBuildTemporaryMesh(context, out workingMesh, out materials))
                {
                    EditorUtility.DisplayDialog("Material Atlas", "No se pudo preparar la malla combinada de la selección.", "Entendido");
                    return;
                }
                disposeWorkingMesh = true;
            }

            if (workingMesh == null)
            {
                EditorUtility.DisplayDialog("Material Atlas", "No se encontró una malla válida para procesar.", "Entendido");
                if (disposeWorkingMesh && workingMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(workingMesh);
                }
                return;
            }

            if (workingMesh.uv == null || workingMesh.uv.Length == 0)
            {
                EditorUtility.DisplayDialog("Material Atlas", "La malla no tiene coordenadas UV en el canal principal.", "Entendido");
                if (disposeWorkingMesh && workingMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(workingMesh);
                }
                return;
            }

            List<Texture2D> tempGenerated = new List<Texture2D>();
            try
            {
                EditorUtility.DisplayProgressBar("Material Atlas", "Preparando texturas...", 0.1f);

                List<string> issues = new List<string>();
                string baseAssetName = string.IsNullOrWhiteSpace(outputName) ? "CombinedAtlas" : outputName;
                Texture2D atlasTexture = null;
                Mesh atlasMesh = null;

                materials ??= Array.Empty<Material>();

                if (useCustomAtlasWorkflow)
                {
                    EditorUtility.DisplayProgressBar("Material Atlas", "Generando atlas personalizado...", 0.35f);
                    if (!TryBuildCustomAtlas(workingMesh, baseAssetName, issues, out atlasMesh, out atlasTexture))
                    {
                        return;
                    }
                }
                else
                {
                    int subMeshCount = workingMesh.subMeshCount;
                    List<Texture2D> textureCopies = new List<Texture2D>(subMeshCount);

                    for (int i = 0; i < subMeshCount; i++)
                    {
                        int materialIndex = materials.Length > 0 ? Mathf.Clamp(i, 0, materials.Length - 1) : -1;
                        Material currentMaterial = materialIndex >= 0 ? materials[materialIndex] : null;
                        Texture2D texture = ExtractTexture(currentMaterial, out string note);
                        bool createdTexture = false;
                        if (!string.IsNullOrEmpty(note))
                        {
                            issues.Add($"Submesh {i + 1}: {note}");
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

                    atlasTexture = new Texture2D(atlasSizes[atlasSizeIndex], atlasSizes[atlasSizeIndex], TextureFormat.RGBA32, false);
                    Rect[] rects = atlasTexture.PackTextures(textureCopies.ToArray(), atlasPadding, atlasSizes[atlasSizeIndex], false);

                    EditorUtility.DisplayProgressBar("Material Atlas", "Reasignando UVs...", 0.6f);

                    atlasMesh = rects.Length == subMeshCount ? BuildAtlasMesh(workingMesh, rects) : null;
                    if (atlasMesh == null)
                    {
                        EditorUtility.DisplayDialog("Material Atlas", "No se pudo generar la malla con el nuevo atlas.", "Entendido");
                        if (atlasTexture != null)
                        {
                            UnityEngine.Object.DestroyImmediate(atlasTexture);
                        }
                        return;
                    }
                    atlasMesh.name = baseAssetName + "_AtlasMesh";
                }

                if (atlasMesh == null || atlasTexture == null)
                {
                    EditorUtility.DisplayDialog("Material Atlas", "No se pudo generar el atlas o la malla resultante.", "Entendido");
                    return;
                }

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
                        if (atlasTexture != null && atlasTexture != finalAtlas && atlasTexture != customAtlasTexture)
                        {
                            UnityEngine.Object.DestroyImmediate(atlasTexture);
                        }
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

                Material baseMaterial = materials.Length > 0 ? materials[0] : null;
                Material atlasMaterial = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Standard"));
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

                if (disposeWorkingMesh && workingMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(workingMesh);
                }

                EditorUtility.ClearProgressBar();
            }
        }

        private static bool TryBuildTemporaryMesh(SelectionContext context, out Mesh combinedMesh, out Material[] materials)
        {
            combinedMesh = null;
            materials = Array.Empty<Material>();

            if (context.Renderers.Count == 0)
            {
                return false;
            }

            List<CombineInstance> combineInstances = new List<CombineInstance>();
            List<Material> materialList = new List<Material>();
            Transform reference = context.TargetRenderer != null ? context.TargetRenderer.transform : context.Renderers[0].transform;
            Matrix4x4 worldToLocal = reference.worldToLocalMatrix;
            int totalVertices = 0;

            foreach (MeshRenderer renderer in context.Renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.subMeshCount == 0)
                {
                    continue;
                }

                totalVertices += mesh.vertexCount;
                Matrix4x4 transformMatrix = worldToLocal * renderer.localToWorldMatrix;
                Material[] rendererMaterials = renderer.sharedMaterials;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    CombineInstance instance = new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = i,
                        transform = transformMatrix
                    };
                    combineInstances.Add(instance);

                    Material material = rendererMaterials != null && rendererMaterials.Length > 0
                        ? rendererMaterials[Mathf.Min(i, rendererMaterials.Length - 1)]
                        : null;
                    materialList.Add(material);
                }
            }

            if (combineInstances.Count == 0)
            {
                return false;
            }

            combinedMesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(outputName) ? "CombinedSelection" : outputName + "_Selection"
            };

            if (totalVertices > 65535)
            {
                combinedMesh.indexFormat = IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(combineInstances.ToArray(), false, true, false);
            combinedMesh.RecalculateBounds();
            materials = materialList.ToArray();
            return true;
        }

        private class CustomUvPreviewEntry
        {
            public string DisplayName;
            public Vector2[] Uvs;
            public int[] Triangles;
            public Color FillColor;
            public Color OutlineColor;

            public bool IsValidIndex(int index)
            {
                return Uvs != null && index >= 0 && index < Uvs.Length;
            }
        }

        private class SelectionContext
        {
            private const int SampleCount = 5;
            private readonly List<Material> materials = new List<Material>();

            public SelectionContext(MeshRenderer targetRenderer, MeshFilter targetFilter)
            {
                TargetRenderer = targetRenderer;
                TargetFilter = targetFilter;
            }

            public MeshRenderer TargetRenderer { get; }
            public MeshFilter TargetFilter { get; }
            public readonly List<MeshRenderer> Renderers = new List<MeshRenderer>();
            public readonly List<string> Skipped = new List<string>();
            public int SubMeshCount;
            public int VertexCount;
            public int UniqueMaterialCount { get; private set; }
            public Material[] MaterialArray { get; private set; } = Array.Empty<Material>();
            public bool RequiresTemporaryMesh { get; private set; }

            public void AddMaterial(Material material)
            {
                materials.Add(material);
            }

            public List<string> GetRendererSamples()
            {
                List<string> names = new List<string>(Renderers.Count);
                for (int i = 0; i < Renderers.Count; i++)
                {
                    MeshRenderer renderer = Renderers[i];
                    if (renderer != null)
                    {
                        names.Add(renderer.name);
                    }
                }

                names.Sort(StringComparer.Ordinal);
                int limit = Mathf.Min(SampleCount, names.Count);
                if (limit < names.Count)
                {
                    return names.GetRange(0, limit);
                }

                return names;
            }

            public void FinalizeData()
            {
                MaterialArray = materials.ToArray();
                HashSet<Material> unique = new HashSet<Material>();
                foreach (Material material in MaterialArray)
                {
                    if (material != null)
                    {
                        unique.Add(material);
                    }
                }

                UniqueMaterialCount = unique.Count;
                RequiresTemporaryMesh = Renderers.Count > 1 || (Renderers.Count == 1 && Renderers[0] != TargetRenderer);
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

            if (rects == null)
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
            int submeshCount = sourceMesh.subMeshCount;
            if (rects.Count < submeshCount)
            {
                return null;
            }
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
