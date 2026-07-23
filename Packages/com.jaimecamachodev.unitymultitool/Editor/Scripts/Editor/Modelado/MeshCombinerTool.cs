using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class MeshCombinerTool
    {
        private static bool includeChildren = true;
        private static bool includeInactive = false;
        private static bool includeSkinnedMeshes = true;
        private static bool mergeByMaterial = true;
        private static bool alignToBoundsCenter = true;
        private static bool parentUnderActive = true;
        private static bool addMeshCollider = false;
        private static bool copyLightmapSettings = true;
        private static bool disableOriginalRenderers = false;
        private static bool saveMeshAsset = true;
        private static string outputMeshName = "CombinedMesh";
        private static DefaultAsset outputFolder;
        private static readonly HashSet<int> rendererIds = new HashSet<int>();
        private static bool showSelectionInsights = true;
        private static bool showAdvancedSettings;
        private static readonly List<string> reusableBuffer = new List<string>();

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Advanced Mesh Combiner") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Combina múltiples objetos estáticos o skinned en un único mesh listo para VR o videojuegos.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            List<Renderer> currentRenderers = new List<Renderer>();
            int currentVertexCount = 0;

            void RefreshContent()
            {
                contentContainer.Clear();

                if (Selection.gameObjects.Length == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona al menos un objeto con MeshRenderer o SkinnedMeshRenderer.", HelpBoxMessageType.Warning));
                    return;
                }

                var diagnosticsPanel = new MTUIPanel("Diagnóstico de la selección") { style = { marginTop = 10 } };
                var diagnosticsContainer = new VisualElement();
                diagnosticsPanel.Add(diagnosticsContainer);
                MTUIActionButton combineButton = null;

                void RefreshDiagnostics()
                {
                    diagnosticsContainer.Clear();

                    SelectionDiagnostics diagnostics = GatherSelectionDiagnostics();
                    currentRenderers = diagnostics.renderers;
                    currentVertexCount = diagnostics.totalVertices;
                    int meshCount = currentRenderers.Count;

                    var renderersRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                    renderersRow.Add(new Label("Renderers a combinar"));
                    renderersRow.Add(new Label(meshCount.ToString()));
                    diagnosticsContainer.Add(renderersRow);

                    var vertexRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                    vertexRow.Add(new Label("Vértices estimados"));
                    vertexRow.Add(new Label(currentVertexCount.ToString()));
                    diagnosticsContainer.Add(vertexRow);

                    if (currentVertexCount > 500000)
                    {
                        diagnosticsContainer.Add(new HelpBox("La combinación supera los 500K vértices. Considera separar por materiales o dividir en bloques para evitar problemas de rendimiento.", HelpBoxMessageType.Warning));
                    }

                    if (diagnostics.skinnedRendererCount > 0 && !includeSkinnedMeshes)
                    {
                        diagnosticsContainer.Add(new HelpBox("Hay SkinnedMeshRenderers seleccionados pero están deshabilitados en la combinación.", HelpBoxMessageType.Info));
                    }

                    foreach (string warning in diagnostics.warnings)
                    {
                        diagnosticsContainer.Add(new HelpBox(warning, HelpBoxMessageType.Warning));
                    }

                    foreach (string note in diagnostics.notes)
                    {
                        diagnosticsContainer.Add(new HelpBox(note, HelpBoxMessageType.None));
                    }

                    var insightsFoldout = new Foldout { text = "Detalle de selección", value = showSelectionInsights, style = { marginTop = 6 } };
                    insightsFoldout.RegisterValueChangedCallback(evt => showSelectionInsights = evt.newValue);

                    insightsFoldout.Add(new Label($"MeshRenderers: {diagnostics.meshRendererCount}"));
                    insightsFoldout.Add(new Label($"SkinnedMeshRenderers: {diagnostics.skinnedRendererCount}"));
                    if (diagnostics.estimatedSubmeshCount > 0)
                    {
                        insightsFoldout.Add(new Label($"Submeshes detectados: {diagnostics.estimatedSubmeshCount}"));
                    }

                    if (diagnostics.sampleNames.Count > 0)
                    {
                        insightsFoldout.Add(new Label("Primeros objetos:") { style = { marginTop = 4 } });
                        foreach (string sampleName in diagnostics.sampleNames)
                        {
                            insightsFoldout.Add(new Label("• " + sampleName) { style = { marginLeft = 10 } });
                        }
                        if (diagnostics.moreSamples)
                        {
                            insightsFoldout.Add(new Label("…" + (meshCount - diagnostics.sampleNames.Count) + " objetos adicionales") { style = { marginLeft = 10 } });
                        }
                    }

                    if (diagnostics.skipped.Count > 0)
                    {
                        insightsFoldout.Add(new Label("Omitidos:") { style = { marginTop = 4 } });
                        foreach (string skipped in diagnostics.skipped)
                        {
                            insightsFoldout.Add(new Label("• " + skipped) { style = { marginLeft = 10, fontSize = 10 } });
                        }
                    }

                    diagnosticsContainer.Add(insightsFoldout);

                    if (meshCount == 0)
                    {
                        diagnosticsContainer.Add(new HelpBox("No se encontraron renderers válidos en la selección.", HelpBoxMessageType.Warning));
                        combineButton.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        combineButton.style.display = DisplayStyle.Flex;
                        combineButton.SetAvailable(currentVertexCount != 0);
                    }
                }

                var sourcePanel = new MTUIPanel("Origen");
                var includeChildrenToggle = new Toggle("Incluir hijos de la selección") { value = includeChildren };
                var includeInactiveToggle = new Toggle("Incluir objetos inactivos") { value = includeInactive };
                var includeSkinnedToggle = new Toggle("Convertir SkinnedMeshRenderers a mesh estático") { value = includeSkinnedMeshes };
                includeChildrenToggle.RegisterValueChangedCallback(evt => { includeChildren = evt.newValue; RefreshDiagnostics(); });
                includeInactiveToggle.RegisterValueChangedCallback(evt => { includeInactive = evt.newValue; RefreshDiagnostics(); });
                includeSkinnedToggle.RegisterValueChangedCallback(evt => { includeSkinnedMeshes = evt.newValue; RefreshDiagnostics(); });
                sourcePanel.Add(includeChildrenToggle);
                sourcePanel.Add(includeInactiveToggle);
                sourcePanel.Add(includeSkinnedToggle);
                contentContainer.Add(sourcePanel);

                var resultPanel = new MTUIPanel("Resultado") { style = { marginTop = 10 } };
                var mergeByMaterialToggle = new Toggle("Agrupar por material (reduce draw calls)") { value = mergeByMaterial };
                var alignToBoundsToggle = new Toggle("Colocar el nuevo objeto en el centro del bound combinado") { value = alignToBoundsCenter };
                mergeByMaterialToggle.RegisterValueChangedCallback(evt => { mergeByMaterial = evt.newValue; RefreshDiagnostics(); });
                alignToBoundsToggle.RegisterValueChangedCallback(evt => { alignToBoundsCenter = evt.newValue; RefreshDiagnostics(); });
                resultPanel.Add(mergeByMaterialToggle);
                resultPanel.Add(alignToBoundsToggle);
                contentContainer.Add(resultPanel);

                var advancedFoldout = new Foldout { text = "Opciones avanzadas", value = showAdvancedSettings, style = { marginTop = 10 } };
                advancedFoldout.RegisterValueChangedCallback(evt => showAdvancedSettings = evt.newValue);

                var parentUnderActiveToggle = new Toggle("Mantener el nuevo objeto bajo el padre del activo") { value = parentUnderActive };
                parentUnderActiveToggle.RegisterValueChangedCallback(evt => { parentUnderActive = evt.newValue; RefreshDiagnostics(); });
                advancedFoldout.Add(parentUnderActiveToggle);

                var addMeshColliderToggle = new Toggle("Añadir MeshCollider al resultado") { value = addMeshCollider };
                addMeshColliderToggle.RegisterValueChangedCallback(evt => { addMeshCollider = evt.newValue; RefreshDiagnostics(); });
                advancedFoldout.Add(addMeshColliderToggle);

                var copyLightmapToggle = new Toggle("Copiar configuración de lightmap del primer renderer") { value = copyLightmapSettings };
                copyLightmapToggle.RegisterValueChangedCallback(evt => copyLightmapSettings = evt.newValue);
                advancedFoldout.Add(copyLightmapToggle);

                var disableOriginalToggle = new Toggle("Desactivar renderers originales tras combinar") { value = disableOriginalRenderers };
                disableOriginalToggle.RegisterValueChangedCallback(evt => { disableOriginalRenderers = evt.newValue; RefreshDiagnostics(); });
                advancedFoldout.Add(disableOriginalToggle);

                contentContainer.Add(advancedFoldout);

                var savePanel = new MTUIPanel("Guardado") { style = { marginTop = 10 } };
                var saveMeshToggle = new Toggle("Guardar mesh combinado como asset") { value = saveMeshAsset };
                var meshNameField = new TextField("Nombre del mesh") { value = outputMeshName, style = { marginLeft = 15 } };
                var folderField = new ObjectField("Carpeta destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder, style = { marginLeft = 15 } };
                var folderHelpBox = new HelpBox("Si no se asigna carpeta se utilizará 'Assets/'.", HelpBoxMessageType.Info) { style = { marginLeft = 15 } };

                void RefreshFolderHelpBox()
                {
                    folderHelpBox.style.display = outputFolder == null && saveMeshAsset ? DisplayStyle.Flex : DisplayStyle.None;
                }

                meshNameField.SetEnabled(saveMeshAsset);
                folderField.SetEnabled(saveMeshAsset);
                RefreshFolderHelpBox();

                saveMeshToggle.RegisterValueChangedCallback(evt =>
                {
                    saveMeshAsset = evt.newValue;
                    meshNameField.SetEnabled(saveMeshAsset);
                    folderField.SetEnabled(saveMeshAsset);
                    RefreshFolderHelpBox();
                    RefreshDiagnostics();
                });
                meshNameField.RegisterValueChangedCallback(evt => outputMeshName = evt.newValue);
                folderField.RegisterValueChangedCallback(evt =>
                {
                    DefaultAsset newFolder = evt.newValue as DefaultAsset;
                    if (newFolder != null)
                    {
                        string path = AssetDatabase.GetAssetPath(newFolder);
                        if (AssetDatabase.IsValidFolder(path))
                        {
                            outputFolder = newFolder;
                        }
                    }
                    else
                    {
                        outputFolder = null;
                    }
                    folderField.SetValueWithoutNotify(outputFolder);
                    RefreshFolderHelpBox();
                    RefreshDiagnostics();
                });

                savePanel.Add(saveMeshToggle);
                savePanel.Add(meshNameField);
                savePanel.Add(folderField);
                savePanel.Add(folderHelpBox);
                contentContainer.Add(savePanel);

                contentContainer.Add(diagnosticsPanel);

                combineButton = new MTUIActionButton("Combinar selección", () => CombineSelection(currentRenderers, currentVertexCount));
                combineButton.style.marginTop = 10;
                contentContainer.Add(combineButton);

                RefreshDiagnostics();
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static SelectionDiagnostics GatherSelectionDiagnostics()
        {
            SelectionDiagnostics diagnostics = new SelectionDiagnostics();
            rendererIds.Clear();

            foreach (GameObject root in Selection.gameObjects)
            {
                if (root == null)
                {
                    continue;
                }

                IEnumerable<Renderer> candidates = includeChildren
                    ? root.GetComponentsInChildren<Renderer>(includeInactive)
                    : root.GetComponents<Renderer>();

                foreach (Renderer renderer in candidates)
                {
                    if (renderer == null || rendererIds.Contains(renderer.GetInstanceID()))
                    {
                        continue;
                    }

                    if (renderer is MeshRenderer)
                    {
                        MeshFilter filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null)
                        {
                            diagnostics.skipped.Add(renderer.name + " (MeshFilter vacío)");
                            continue;
                        }
                        if (!filter.sharedMesh.isReadable)
                        {
                            diagnostics.skipped.Add(renderer.name + " (mesh no legible: activa Read/Write en su Import Settings)");
                            continue;
                        }
                        diagnostics.meshRendererCount++;
                        diagnostics.estimatedSubmeshCount += filter.sharedMesh.subMeshCount;
                    }
                    else if (renderer is SkinnedMeshRenderer)
                    {
                        if (!includeSkinnedMeshes)
                        {
                            diagnostics.skipped.Add(renderer.name + " (Skinned deshabilitado)");
                            continue;
                        }

                        SkinnedMeshRenderer skinned = (SkinnedMeshRenderer)renderer;
                        if (skinned.sharedMesh == null)
                        {
                            diagnostics.skipped.Add(renderer.name + " (mesh vacío)");
                            continue;
                        }
                        if (!skinned.sharedMesh.isReadable)
                        {
                            diagnostics.skipped.Add(renderer.name + " (mesh no legible: activa Read/Write en su Import Settings)");
                            continue;
                        }
                        diagnostics.skinnedRendererCount++;
                        diagnostics.estimatedSubmeshCount += skinned.sharedMesh.subMeshCount;
                    }
                    else
                    {
                        diagnostics.skipped.Add(renderer.name + " (renderer no soportado)");
                        continue;
                    }

                    diagnostics.renderers.Add(renderer);
                    rendererIds.Add(renderer.GetInstanceID());
                }
            }

            diagnostics.totalVertices = CalculateVertexCount(diagnostics.renderers);
            diagnostics.CaptureSamples();
            diagnostics.notes.Add("Los vértices se combinan en espacio mundo; el objeto resultante queda con rotación y escala identidad.");

            if (!alignToBoundsCenter)
            {
                diagnostics.notes.Add("El objeto resultante usará la posición del activo seleccionado como pivote.");
            }

            if (!parentUnderActive)
            {
                diagnostics.notes.Add("El combinado se creará en la raíz de la escena.");
            }

            if (addMeshCollider)
            {
                diagnostics.notes.Add("Se añadirá un MeshCollider al objeto combinado.");
            }

            if (disableOriginalRenderers)
            {
                diagnostics.notes.Add("Los objetos originales se desactivarán tras combinar.");
            }

            if (saveMeshAsset)
            {
                string folderInfo = "Assets";
                if (outputFolder != null)
                {
                    string path = AssetDatabase.GetAssetPath(outputFolder);
                    if (!string.IsNullOrEmpty(path))
                    {
                        folderInfo = path;
                    }
                }
                diagnostics.notes.Add($"Se generará un asset en '{folderInfo}'.");
            }

            if (diagnostics.skipped.Count > 0)
            {
                diagnostics.warnings.Add("Algunos renderers se omitieron (revisa el listado inferior).");
            }

            if (!mergeByMaterial && diagnostics.estimatedSubmeshCount > diagnostics.renderers.Count)
            {
                diagnostics.warnings.Add("Hay múltiples submeshes sin agrupar; considera activar 'Agrupar por material'.");
            }

            return diagnostics;
        }

        private static int CalculateVertexCount(List<Renderer> renderers)
        {
            int total = 0;
            foreach (Renderer renderer in renderers)
            {
                switch (renderer)
                {
                    case MeshRenderer meshRenderer:
                        MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                        {
                            total += filter.sharedMesh.vertexCount;
                        }
                        break;
                    case SkinnedMeshRenderer skinnedRenderer:
                        if (skinnedRenderer.sharedMesh != null)
                        {
                            total += skinnedRenderer.sharedMesh.vertexCount;
                        }
                        break;
                }
            }

            return total;
        }

        private static void CombineSelection(List<Renderer> renderers, int estimatedVertices)
        {
            if (renderers.Count == 0)
            {
                return;
            }

            Transform reference = Selection.activeTransform != null ? Selection.activeTransform : renderers[0].transform;
            Bounds combinedBounds = CalculateCombinedBounds(renderers);

            Vector3 targetPosition = alignToBoundsCenter ? combinedBounds.center : reference.position;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            GameObject combinedObject = new GameObject(string.IsNullOrWhiteSpace(outputMeshName) ? "CombinedMesh" : outputMeshName);
            Undo.RegisterCreatedObjectUndo(combinedObject, "Combine Meshes");
            combinedObject.transform.SetPositionAndRotation(targetPosition, Quaternion.identity);
            combinedObject.transform.localScale = Vector3.one;

            if (parentUnderActive && reference.parent != null)
            {
                Undo.SetTransformParent(combinedObject.transform, reference.parent, "Set combined parent");
            }

            CombinePreparationResult preparation;
            try
            {
                EditorUtility.DisplayProgressBar("Mesh Combiner", "Preparando instancias...", 0.25f);
                preparation = PrepareCombineData(renderers, combinedObject.transform);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (preparation.instances.Count == 0)
            {
                UnityEngine.Object.DestroyImmediate(combinedObject);
                Undo.CollapseUndoOperations(undoGroup);
                EditorUtility.DisplayDialog("Mesh Combiner", "No se pudo generar ninguna instancia combinable.", "Entendido");
                return;
            }

            Mesh combinedMesh;
            try
            {
                EditorUtility.DisplayProgressBar("Mesh Combiner", "Generando mesh combinado...", 0.65f);
                combinedMesh = new Mesh
                {
                    name = string.IsNullOrWhiteSpace(outputMeshName) ? "CombinedMesh" : outputMeshName
                };

                if (estimatedVertices > 65535)
                {
                    combinedMesh.indexFormat = IndexFormat.UInt32;
                }

                combinedMesh.CombineMeshes(preparation.instances.ToArray(), false, true, false);
                combinedMesh.RecalculateBounds();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            MeshFilter combinedFilter = combinedObject.AddComponent<MeshFilter>();
            combinedFilter.sharedMesh = combinedMesh;

            MeshRenderer combinedRenderer = combinedObject.AddComponent<MeshRenderer>();
            combinedRenderer.sharedMaterials = preparation.materials.ToArray();

            if (copyLightmapSettings)
            {
                CopyLightmapSettings(renderers, combinedRenderer);
            }

            if (addMeshCollider)
            {
                MeshCollider collider = combinedObject.AddComponent<MeshCollider>();
                collider.sharedMesh = combinedMesh;
            }

            if (saveMeshAsset)
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Mesh Combiner", "Guardando asset...", 0.9f);
                    SaveMeshAsset(combinedMesh);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (disableOriginalRenderers)
            {
                foreach (Renderer renderer in renderers)
                {
                    Undo.RecordObject(renderer.gameObject, "Disable original renderer");
                    renderer.gameObject.SetActive(false);
                }
            }

            try
            {
                EditorUtility.DisplayProgressBar("Mesh Combiner", "Limpiando temporales...", 0.98f);
                foreach (Mesh mesh in preparation.temporaryMeshes)
                {
                    if (mesh != null)
                    {
                        UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = combinedObject;
            SceneView.RepaintAll();

            Debug.Log($"[Mesh Combiner] Se generó '{combinedObject.name}' con {preparation.materials.Count} materiales y {combinedMesh.vertexCount} vértices.");
        }

        private static CombinePreparationResult PrepareCombineData(List<Renderer> renderers, Transform combinedTransform)
        {
            CombinePreparationResult result = new CombinePreparationResult();
            Dictionary<Material, List<CombineInstance>> perMaterial = new Dictionary<Material, List<CombineInstance>>();
            List<CombineInstance> perSubmesh = new List<CombineInstance>();
            Matrix4x4 worldToCombined = combinedTransform.worldToLocalMatrix;

            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = null;
                Matrix4x4 transformMatrix = worldToCombined * renderer.localToWorldMatrix;

                if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    mesh = filter.sharedMesh;
                }
                else if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh
                    {
                        name = skinned.sharedMesh != null ? skinned.sharedMesh.name + "_Baked" : "BakedMesh"
                    };
                    skinned.BakeMesh(mesh);
                    result.temporaryMeshes.Add(mesh);
                }

                if (mesh == null)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                int subMeshCount = mesh.subMeshCount;

                if (subMeshCount == 0)
                {
                    continue;
                }

                for (int i = 0; i < subMeshCount; i++)
                {
                    Material material = materials.Length > 0 ? materials[Mathf.Min(i, materials.Length - 1)] : null;
                    CombineInstance instance = new CombineInstance
                    {
                        mesh = mesh,
                        subMeshIndex = Mathf.Min(i, mesh.subMeshCount - 1),
                        transform = transformMatrix
                    };

                    if (mergeByMaterial)
                    {
                        if (!perMaterial.TryGetValue(material, out List<CombineInstance> list))
                        {
                            list = new List<CombineInstance>();
                            perMaterial.Add(material, list);
                        }

                        list.Add(instance);
                    }
                    else
                    {
                        perSubmesh.Add(instance);
                        result.materials.Add(material);
                    }
                }
            }

            if (mergeByMaterial)
            {
                foreach (KeyValuePair<Material, List<CombineInstance>> kvp in perMaterial)
                {
                    Mesh materialMesh = new Mesh
                    {
                        name = (kvp.Key != null ? kvp.Key.name : "NoMaterial") + "_Combined"
                    };
                    materialMesh.CombineMeshes(kvp.Value.ToArray(), true, true, false);
                    result.temporaryMeshes.Add(materialMesh);

                    result.instances.Add(new CombineInstance
                    {
                        mesh = materialMesh,
                        subMeshIndex = 0,
                        transform = Matrix4x4.identity
                    });

                    result.materials.Add(kvp.Key);
                }
            }
            else
            {
                result.instances.AddRange(perSubmesh);
            }

            return result;
        }

        private static void CopyLightmapSettings(List<Renderer> sourceRenderers, Renderer target)
        {
            foreach (Renderer renderer in sourceRenderers)
            {
                if (renderer.lightmapIndex >= 0)
                {
                    target.lightmapIndex = renderer.lightmapIndex;
                    target.lightmapScaleOffset = renderer.lightmapScaleOffset;
                    target.receiveShadows = renderer.receiveShadows;
                    target.shadowCastingMode = renderer.shadowCastingMode;
                    return;
                }
            }
        }

        private static Bounds CalculateCombinedBounds(List<Renderer> renderers)
        {
            Bounds bounds = new Bounds(renderers[0].bounds.center, Vector3.zero);
            for (int i = 0; i < renderers.Count; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void SaveMeshAsset(Mesh mesh)
        {
            string folderPath = "Assets";
            if (outputFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(outputFolder);
                if (AssetDatabase.IsValidFolder(path))
                {
                    folderPath = path;
                }
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string safeName = string.IsNullOrWhiteSpace(outputMeshName) ? "CombinedMesh" : outputMeshName;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, safeName + ".asset"));
            assetPath = assetPath.Replace("\\", "/");

            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private class CombinePreparationResult
        {
            public readonly List<CombineInstance> instances = new List<CombineInstance>();
            public readonly List<Material> materials = new List<Material>();
            public readonly List<Mesh> temporaryMeshes = new List<Mesh>();
        }

        private class SelectionDiagnostics
        {
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<string> warnings = new List<string>();
            public readonly List<string> notes = new List<string>();
            public readonly List<string> skipped = new List<string>();
            public readonly List<string> sampleNames = new List<string>();
            public int meshRendererCount;
            public int skinnedRendererCount;
            public int totalVertices;
            public int estimatedSubmeshCount;
            public bool moreSamples;

            public void CaptureSamples()
            {
                sampleNames.Clear();
                reusableBuffer.Clear();
                for (int i = 0; i < renderers.Count; i++)
                {
                    reusableBuffer.Add(renderers[i].name);
                }
                reusableBuffer.Sort();

                int sampleLimit = Mathf.Min(5, reusableBuffer.Count);
                for (int i = 0; i < sampleLimit; i++)
                {
                    sampleNames.Add(reusableBuffer[i]);
                }
                moreSamples = reusableBuffer.Count > sampleLimit;
            }
        }
    }
}
