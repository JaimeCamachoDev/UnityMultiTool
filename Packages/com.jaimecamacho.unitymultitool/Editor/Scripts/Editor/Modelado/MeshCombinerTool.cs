using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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

        public static void DrawTool()
        {
            GUILayout.Label("Advanced Mesh Combiner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Combina múltiples objetos estáticos o skinned en un único mesh listo para VR o videojuegos.", MessageType.Info);

            if (Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox("Selecciona al menos un objeto con MeshRenderer o SkinnedMeshRenderer.", MessageType.Warning);
                return;
            }

            includeChildren = EditorGUILayout.ToggleLeft("Incluir hijos de la selección", includeChildren);
            includeInactive = EditorGUILayout.ToggleLeft("Incluir objetos inactivos", includeInactive);
            includeSkinnedMeshes = EditorGUILayout.ToggleLeft("Convertir SkinnedMeshRenderers a mesh estático", includeSkinnedMeshes);
            mergeByMaterial = EditorGUILayout.ToggleLeft("Agrupar por material (reduce draw calls)", mergeByMaterial);
            alignToBoundsCenter = EditorGUILayout.ToggleLeft("Colocar el nuevo objeto en el centro del bound combinado", alignToBoundsCenter);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Opciones avanzadas", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                parentUnderActive = EditorGUILayout.ToggleLeft("Mantener el nuevo objeto bajo el padre del activo", parentUnderActive);
                addMeshCollider = EditorGUILayout.ToggleLeft("Añadir MeshCollider al resultado", addMeshCollider);
                copyLightmapSettings = EditorGUILayout.ToggleLeft("Copiar configuración de lightmap del primer renderer", copyLightmapSettings);
                disableOriginalRenderers = EditorGUILayout.ToggleLeft("Desactivar renderers originales tras combinar", disableOriginalRenderers);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6f);

            saveMeshAsset = EditorGUILayout.ToggleLeft("Guardar mesh combinado como asset", saveMeshAsset);
            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(!saveMeshAsset))
            {
                outputMeshName = EditorGUILayout.TextField("Nombre del mesh", outputMeshName);
                DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField("Carpeta destino", outputFolder, typeof(DefaultAsset), false);
                if (newFolder != outputFolder && newFolder != null)
                {
                    string path = AssetDatabase.GetAssetPath(newFolder);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        outputFolder = newFolder;
                    }
                }
                if (outputFolder == null && saveMeshAsset)
                {
                    EditorGUILayout.HelpBox("Si no se asigna carpeta se utilizará 'Assets/'.", MessageType.Info);
                }
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(6f);

            SelectionDiagnostics diagnostics = GatherSelectionDiagnostics();
            List<Renderer> gatheredRenderers = diagnostics.renderers;
            int meshCount = gatheredRenderers.Count;
            int vertexCount = diagnostics.totalVertices;
            EditorGUILayout.LabelField("Renderers a combinar", meshCount.ToString());
            EditorGUILayout.LabelField("Vértices estimados", vertexCount.ToString());

            if (vertexCount > 500000)
            {
                EditorGUILayout.HelpBox("La combinación supera los 500K vértices. Considera separar por materiales o dividir en bloques para evitar problemas de rendimiento.", MessageType.Warning);
            }

            if (diagnostics.skinnedRendererCount > 0 && !includeSkinnedMeshes)
            {
                EditorGUILayout.HelpBox("Hay SkinnedMeshRenderers seleccionados pero están deshabilitados en la combinación.", MessageType.Info);
            }

            foreach (string warning in diagnostics.warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            foreach (string note in diagnostics.notes)
            {
                EditorGUILayout.HelpBox(note, MessageType.None);
            }

            showSelectionInsights = EditorGUILayout.Foldout(showSelectionInsights, "Detalle de selección", true);
            if (showSelectionInsights)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("MeshRenderers", diagnostics.meshRendererCount.ToString());
                    EditorGUILayout.LabelField("SkinnedMeshRenderers", diagnostics.skinnedRendererCount.ToString());
                    if (diagnostics.estimatedSubmeshCount > 0)
                    {
                        EditorGUILayout.LabelField("Submeshes detectados", diagnostics.estimatedSubmeshCount.ToString());
                    }

                    if (diagnostics.sampleNames.Count > 0)
                    {
                        EditorGUILayout.LabelField("Primeros objetos:");
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < diagnostics.sampleNames.Count; i++)
                        {
                            EditorGUILayout.LabelField("• " + diagnostics.sampleNames[i]);
                        }
                        if (diagnostics.moreSamples)
                        {
                            EditorGUILayout.LabelField("…" + (meshCount - diagnostics.sampleNames.Count) + " objetos adicionales");
                        }
                        EditorGUI.indentLevel--;
                    }

                    if (diagnostics.skipped.Count > 0)
                    {
                        EditorGUILayout.Space(2f);
                        EditorGUILayout.LabelField("Omitidos:");
                        EditorGUI.indentLevel++;
                        foreach (string skipped in diagnostics.skipped)
                        {
                            EditorGUILayout.LabelField("• " + skipped, EditorStyles.miniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }
                }
            }

            if (meshCount == 0)
            {
                EditorGUILayout.HelpBox("No se encontraron renderers válidos en la selección.", MessageType.Warning);
                return;
            }

            GUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(vertexCount == 0))
            {
                if (GUILayout.Button("Combinar selección"))
                {
                    CombineSelection(gatheredRenderers, vertexCount);
                }
            }
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
            diagnostics.notes.Add("Los renderers se combinan en espacio mundo y adoptan la rotación del activo seleccionado.");

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

                combinedMesh.CombineMeshes(preparation.instances.ToArray(), false, false, false);
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
                int subMeshCount = Math.Min(mesh.subMeshCount, materials.Length);

                if (subMeshCount == 0)
                {
                    subMeshCount = mesh.subMeshCount;
                }

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
