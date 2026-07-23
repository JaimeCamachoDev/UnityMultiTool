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
    // Detecta caras (triángulos) completamente ocultas tras otra geometría -por ejemplo caras
    // internas entre piezas modulares unidas o superficies tapadas por otro objeto- y permite
    // eliminarlas para aligerar el mesh. La visibilidad se estima lanzando varios rayos por cara
    // hacia el hemisferio de su normal: es una heurística, no una solución exacta, y puede fallar
    // en geometría muy compleja, cóncava o con huecos diminutos entre piezas.
    public static class VertexOptimizationTool
    {
        private enum SampleQuality { Low, Medium, High }

        private static readonly List<GameObject> targetObjects = new List<GameObject>();
        private static readonly List<GameObject> extraOccluders = new List<GameObject>();
        private static bool includeTargetsAsOccluders = true;
        private static SampleQuality sampleQuality = SampleQuality.Medium;
        private static bool showHiddenFacesInSceneView = true;
        private static bool saveAsAsset = true;
        private static DefaultAsset outputFolder;

        private static readonly List<TargetAnalysis> analyses = new List<TargetAnalysis>();

        private sealed class TargetAnalysis
        {
            public GameObject GameObject;
            public MeshFilter Filter;
            public Mesh SourceMesh;
            public int FaceCount;
            public int HiddenCount;
            public bool[] Hidden;
            public Vector3[] FaceCentroids;
            public Vector3[] FaceNormals;
            public Vector3[] FaceVertexA;
            public Vector3[] FaceVertexB;
            public Vector3[] FaceVertexC;
        }

        private struct OccluderTriangle
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
        }

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();

            root.Add(new Label("Remove Hidden Faces") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Detecta y elimina caras que quedan completamente ocultas tras otra geometría (por ejemplo, caras internas entre piezas modulares unidas) muestreando rayos por cara. Es una heurística: revisa el resultado en la Scene View antes de guardar los cambios.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                targetObjects.Clear();
                targetObjects.AddRange(Selection.gameObjects);

                if (targetObjects.Count == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona uno o más objetos en la escena para analizar su visibilidad.", HelpBoxMessageType.Warning));
                    return;
                }

                var selectionPanel = new MTUIPanel("Objetos a optimizar");
                foreach (GameObject go in targetObjects)
                {
                    selectionPanel.Add(new MTUIInfoLabel("• " + go.name));
                }
                contentContainer.Add(selectionPanel);

                var occludersPanel = new MTUIPanel("Oclusores adicionales (opcional)") { style = { marginTop = 10 } };
                var includeTargetsToggle = new Toggle("Usar los propios objetos a optimizar como oclusores entre sí") { value = includeTargetsAsOccluders };
                includeTargetsToggle.RegisterValueChangedCallback(evt => includeTargetsAsOccluders = evt.newValue);
                occludersPanel.Add(includeTargetsToggle);

                var occluderListContainer = new VisualElement();
                occludersPanel.Add(occluderListContainer);

                void RefreshOccluderList()
                {
                    occluderListContainer.Clear();
                    for (int i = 0; i < extraOccluders.Count; i++)
                    {
                        int index = i;
                        var field = new ObjectField($"Oclusor extra {i + 1}") { objectType = typeof(GameObject), allowSceneObjects = true, value = extraOccluders[index] };
                        field.RegisterValueChangedCallback(evt => extraOccluders[index] = evt.newValue as GameObject);
                        occluderListContainer.Add(field);
                    }
                }

                var occluderButtonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
                occluderButtonsRow.Add(new MTUIActionButton("Añadir oclusor", () =>
                {
                    extraOccluders.Add(null);
                    RefreshOccluderList();
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));

                var removeOccluderButton = new MTUIActionButton("Quitar último oclusor", () =>
                {
                    if (extraOccluders.Count > 0)
                    {
                        extraOccluders.RemoveAt(extraOccluders.Count - 1);
                        RefreshOccluderList();
                    }
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                removeOccluderButton.style.marginLeft = 6;
                occluderButtonsRow.Add(removeOccluderButton);
                occludersPanel.Add(occluderButtonsRow);

                RefreshOccluderList();
                contentContainer.Add(occludersPanel);

                var samplingPanel = new MTUIPanel("Muestreo") { style = { marginTop = 10 } };
                var qualityField = new EnumField("Calidad", sampleQuality);
                qualityField.RegisterValueChangedCallback(evt => sampleQuality = (SampleQuality)evt.newValue);
                samplingPanel.Add(qualityField);

                var highlightToggle = new Toggle("Resaltar caras ocultas en la Scene View") { value = showHiddenFacesInSceneView };
                highlightToggle.RegisterValueChangedCallback(evt => showHiddenFacesInSceneView = evt.newValue);
                samplingPanel.Add(highlightToggle);
                contentContainer.Add(samplingPanel);

                var resultsPanel = new MTUIPanel("Resultados") { style = { marginTop = 10 } };
                var resultsContainer = new VisualElement();
                resultsPanel.Add(resultsContainer);

                var analyzeButton = new MTUIActionButton("Analizar visibilidad", () =>
                {
                    Analyze();
                    RefreshResults(resultsContainer);
                });
                analyzeButton.style.marginTop = 10;
                contentContainer.Add(analyzeButton);

                contentContainer.Add(resultsPanel);
                RefreshResults(resultsContainer);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void RefreshResults(VisualElement container)
        {
            container.Clear();

            if (analyses.Count == 0)
            {
                return;
            }

            int totalFaces = 0;
            int totalHidden = 0;
            foreach (TargetAnalysis analysis in analyses)
            {
                totalFaces += analysis.FaceCount;
                totalHidden += analysis.HiddenCount;
            }

            container.Add(new HelpBox($"{totalHidden} de {totalFaces} caras analizadas se consideran completamente ocultas.", totalHidden > 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.None));

            foreach (TargetAnalysis analysis in analyses)
            {
                string label = analysis.GameObject != null ? analysis.GameObject.name : "(objeto eliminado)";
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                row.Add(new Label(label));
                row.Add(new Label($"{analysis.HiddenCount}/{analysis.FaceCount} caras ocultas"));
                container.Add(row);
            }

            var saveToggle = new Toggle("Guardar mesh resultante como asset") { value = saveAsAsset, style = { marginTop = 10 } };
            container.Add(saveToggle);

            var folderField = new ObjectField("Carpeta destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder, style = { marginLeft = 15 } };
            var folderHelpBox = new HelpBox("Si no se asigna carpeta se usará la carpeta del mesh original (o 'Assets/' si no es un asset).", HelpBoxMessageType.None) { style = { marginLeft = 15 } };
            folderHelpBox.style.display = outputFolder == null ? DisplayStyle.Flex : DisplayStyle.None;
            folderField.SetEnabled(saveAsAsset);
            folderField.RegisterValueChangedCallback(evt =>
            {
                outputFolder = evt.newValue as DefaultAsset;
                folderHelpBox.style.display = outputFolder == null ? DisplayStyle.Flex : DisplayStyle.None;
            });
            saveToggle.RegisterValueChangedCallback(evt =>
            {
                saveAsAsset = evt.newValue;
                folderField.SetEnabled(saveAsAsset);
            });
            container.Add(folderField);
            container.Add(folderHelpBox);

            var removeButton = new MTUIActionButton("Eliminar caras ocultas", () =>
            {
                ApplyRemoval();
                RefreshResults(container);
            });
            removeButton.style.marginTop = 10;
            removeButton.SetAvailable(totalHidden > 0);
            container.Add(removeButton);
        }

        private static int GetSampleCount(SampleQuality quality)
        {
            switch (quality)
            {
                case SampleQuality.Low:
                    return 1;
                case SampleQuality.High:
                    return 9;
                default:
                    return 5;
            }
        }

        private static void Analyze()
        {
            analyses.Clear();

            List<GameObject> validTargets = new List<GameObject>();
            foreach (GameObject go in targetObjects)
            {
                if (go != null && !validTargets.Contains(go))
                {
                    validTargets.Add(go);
                }
            }

            if (validTargets.Count == 0)
            {
                Debug.LogWarning("Arrastra al menos un objeto para analizar.");
                return;
            }

            List<GameObject> occluderObjects = new List<GameObject>();
            if (includeTargetsAsOccluders)
            {
                occluderObjects.AddRange(validTargets);
            }
            foreach (GameObject go in extraOccluders)
            {
                if (go != null && !occluderObjects.Contains(go))
                {
                    occluderObjects.Add(go);
                }
            }

            if (occluderObjects.Count == 0)
            {
                Debug.LogWarning("No hay geometría contra la que comprobar visibilidad. Activa \"Usar los propios objetos como oclusores\" o añade oclusores.");
                return;
            }

            List<OccluderTriangle> soup = new List<OccluderTriangle>();
            Bounds combinedBounds = new Bounds();
            bool boundsInitialized = false;

            foreach (GameObject occluder in occluderObjects)
            {
                MeshFilter filter = occluder.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"'{occluder.name}' usa un mesh sin Read/Write habilitado; se ignora como oclusor.");
                    continue;
                }

                Renderer renderer = occluder.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }

                Vector3[] localVertices = mesh.vertices;
                Matrix4x4 localToWorld = occluder.transform.localToWorldMatrix;
                Vector3[] worldVertices = new Vector3[localVertices.Length];
                for (int i = 0; i < localVertices.Length; i++)
                {
                    worldVertices[i] = localToWorld.MultiplyPoint3x4(localVertices[i]);
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] tris = mesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        soup.Add(new OccluderTriangle
                        {
                            A = worldVertices[tris[i]],
                            B = worldVertices[tris[i + 1]],
                            C = worldVertices[tris[i + 2]]
                        });
                    }
                }
            }

            if (soup.Count == 0)
            {
                Debug.LogWarning("No se encontró geometría legible entre los oclusores (revisa Read/Write en sus Import Settings).");
                return;
            }

            if (!boundsInitialized)
            {
                combinedBounds = new Bounds(Vector3.zero, Vector3.one);
            }

            float boundsDiagonal = Mathf.Max(combinedBounds.size.magnitude, 0.01f);
            float escapeDistance = boundsDiagonal * 2f + 1f;
            float normalEpsilon = Mathf.Max(0.0001f, boundsDiagonal * 0.0005f);

            List<TargetAnalysis> newAnalyses = new List<TargetAnalysis>();
            int totalFaces = 0;

            foreach (GameObject target in validTargets)
            {
                MeshFilter filter = target.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    if (target.GetComponent<SkinnedMeshRenderer>() != null)
                    {
                        Debug.LogWarning($"'{target.name}' usa SkinnedMeshRenderer; esta herramienta solo soporta mallas estáticas (MeshFilter) y se omite.");
                    }
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"'{target.name}' usa un mesh sin Read/Write habilitado; se omite.");
                    continue;
                }

                Vector3[] localVertices = mesh.vertices;
                Vector3[] localNormals = mesh.normals;
                Matrix4x4 localToWorld = target.transform.localToWorldMatrix;
                Matrix4x4 normalMatrix = localToWorld.inverse.transpose;
                bool hasNormals = localNormals != null && localNormals.Length == localVertices.Length;

                List<Vector3> centroids = new List<Vector3>();
                List<Vector3> faceNormals = new List<Vector3>();
                List<Vector3> vertexA = new List<Vector3>();
                List<Vector3> vertexB = new List<Vector3>();
                List<Vector3> vertexC = new List<Vector3>();

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] tris = mesh.GetTriangles(s);
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        Vector3 a = localToWorld.MultiplyPoint3x4(localVertices[tris[i]]);
                        Vector3 b = localToWorld.MultiplyPoint3x4(localVertices[tris[i + 1]]);
                        Vector3 c = localToWorld.MultiplyPoint3x4(localVertices[tris[i + 2]]);

                        Vector3 normal;
                        if (hasNormals)
                        {
                            Vector3 n0 = normalMatrix.MultiplyVector(localNormals[tris[i]]);
                            Vector3 n1 = normalMatrix.MultiplyVector(localNormals[tris[i + 1]]);
                            Vector3 n2 = normalMatrix.MultiplyVector(localNormals[tris[i + 2]]);
                            Vector3 averaged = n0 + n1 + n2;
                            normal = averaged.sqrMagnitude > 0.0001f ? averaged.normalized : Vector3.Cross(b - a, c - a).normalized;
                        }
                        else
                        {
                            normal = Vector3.Cross(b - a, c - a).normalized;
                        }

                        centroids.Add((a + b + c) / 3f);
                        faceNormals.Add(normal);
                        vertexA.Add(a);
                        vertexB.Add(b);
                        vertexC.Add(c);
                    }
                }

                TargetAnalysis analysis = new TargetAnalysis
                {
                    GameObject = target,
                    Filter = filter,
                    SourceMesh = mesh,
                    FaceCount = centroids.Count,
                    FaceCentroids = centroids.ToArray(),
                    FaceNormals = faceNormals.ToArray(),
                    FaceVertexA = vertexA.ToArray(),
                    FaceVertexB = vertexB.ToArray(),
                    FaceVertexC = vertexC.ToArray(),
                    Hidden = new bool[centroids.Count]
                };

                newAnalyses.Add(analysis);
                totalFaces += analysis.FaceCount;
            }

            if (newAnalyses.Count == 0)
            {
                Debug.LogWarning("No hay objetos válidos para analizar (revisa que tengan MeshFilter y Read/Write habilitado).");
                return;
            }

            int sampleCount = GetSampleCount(sampleQuality);
            long estimatedTests = (long)totalFaces * soup.Count * sampleCount;

            if (estimatedTests > 50_000_000L)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Remove Hidden Faces",
                    $"El análisis estimado requiere ~{estimatedTests:N0} comprobaciones rayo-triángulo y puede tardar bastante. ¿Continuar de todas formas?",
                    "Continuar", "Cancelar");
                if (!proceed)
                {
                    return;
                }
            }

            bool cancelled = false;
            int processedFaces = 0;

            try
            {
                foreach (TargetAnalysis analysis in newAnalyses)
                {
                    for (int f = 0; f < analysis.FaceCount; f++)
                    {
                        if (processedFaces % 32 == 0)
                        {
                            float progress = totalFaces > 0 ? (float)processedFaces / totalFaces : 1f;
                            if (EditorUtility.DisplayCancelableProgressBar("Remove Hidden Faces", $"Analizando '{analysis.GameObject.name}' ({processedFaces}/{totalFaces})...", progress))
                            {
                                cancelled = true;
                                break;
                            }
                        }

                        bool visible = IsFaceVisible(analysis.FaceCentroids[f], analysis.FaceNormals[f], soup, escapeDistance, normalEpsilon, sampleCount);
                        analysis.Hidden[f] = !visible;
                        if (!visible)
                        {
                            analysis.HiddenCount++;
                        }

                        processedFaces++;
                    }

                    if (cancelled)
                    {
                        break;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (cancelled)
            {
                Debug.LogWarning("Análisis cancelado por el usuario.");
                return;
            }

            analyses.AddRange(newAnalyses);

            int totalHidden = 0;
            foreach (TargetAnalysis analysis in analyses)
            {
                totalHidden += analysis.HiddenCount;
            }

            Debug.Log($"Análisis completado: {totalHidden} de {totalFaces} caras se consideran completamente ocultas.");
            SceneView.RepaintAll();
        }

        private static bool IsFaceVisible(Vector3 centroid, Vector3 normal, List<OccluderTriangle> soup, float escapeDistance, float normalEpsilon, int sampleCount)
        {
            Vector3 origin = centroid + normal * normalEpsilon;
            List<Vector3> directions = GenerateHemisphereSamples(normal, sampleCount);

            foreach (Vector3 direction in directions)
            {
                float closestHit = float.MaxValue;
                for (int i = 0; i < soup.Count; i++)
                {
                    OccluderTriangle tri = soup[i];
                    if (RayIntersectsTriangle(origin, direction, tri.A, tri.B, tri.C, out float t) && t < closestHit)
                    {
                        closestHit = t;
                    }
                }

                if (closestHit > escapeDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Vector3> GenerateHemisphereSamples(Vector3 normal, int sampleCount)
        {
            List<Vector3> directions = new List<Vector3> { normal };

            int ringCount = Mathf.Max(0, sampleCount - 1);
            if (ringCount == 0)
            {
                return directions;
            }

            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
            Vector3 tangent = Vector3.Cross(normal, up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            const float coneAngleDegrees = 55f;
            float coneAngleRad = coneAngleDegrees * Mathf.Deg2Rad;
            float sinAngle = Mathf.Sin(coneAngleRad);
            float cosAngle = Mathf.Cos(coneAngleRad);

            for (int i = 0; i < ringCount; i++)
            {
                float theta = (2f * Mathf.PI * i) / ringCount;
                Vector3 dir = (normal * cosAngle) + ((tangent * Mathf.Cos(theta)) + (bitangent * Mathf.Sin(theta))) * sinAngle;
                directions.Add(dir.normalized);
            }

            return directions;
        }

        private const float RayEpsilon = 1e-6f;

        // Intersección rayo-triángulo de Möller-Trumbore. dir debe ir normalizado para que "t"
        // represente una distancia real y pueda compararse con escapeDistance.
        private static bool RayIntersectsTriangle(Vector3 origin, Vector3 dir, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0f;

            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            Vector3 pVec = Vector3.Cross(dir, edge2);
            float det = Vector3.Dot(edge1, pVec);
            if (Mathf.Abs(det) < RayEpsilon)
            {
                return false;
            }

            float invDet = 1f / det;
            Vector3 tVec = origin - a;
            float u = Vector3.Dot(tVec, pVec) * invDet;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 qVec = Vector3.Cross(tVec, edge1);
            float v = Vector3.Dot(dir, qVec) * invDet;
            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            t = Vector3.Dot(edge2, qVec) * invDet;
            return t > RayEpsilon;
        }

        private static void ApplyRemoval()
        {
            if (analyses.Count == 0)
            {
                Debug.LogWarning("Ejecuta \"Analizar visibilidad\" antes de eliminar caras ocultas.");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Hidden Faces");
            int processedCount = 0;

            foreach (TargetAnalysis analysis in analyses)
            {
                if (analysis.HiddenCount == 0 || analysis.GameObject == null || analysis.Filter == null)
                {
                    continue;
                }

                Mesh filtered = BuildFilteredMesh(analysis.SourceMesh, analysis.Hidden);
                if (filtered == null)
                {
                    Debug.LogWarning($"'{analysis.GameObject.name}': se omite porque no quedaría ninguna cara visible.");
                    continue;
                }

                filtered.name = analysis.SourceMesh.name + "_NoHiddenFaces";
                Undo.RegisterCreatedObjectUndo(filtered, "Remove Hidden Faces");
                Undo.RecordObject(analysis.Filter, "Asignar mesh sin caras ocultas");
                analysis.Filter.sharedMesh = filtered;

                MeshCollider collider = analysis.GameObject.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    Undo.RecordObject(collider, "Actualizar MeshCollider");
                    collider.sharedMesh = null;
                    collider.sharedMesh = filtered;
                }

                if (saveAsAsset)
                {
                    SaveFilteredMeshAsset(filtered, analysis.SourceMesh);
                }

                processedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (processedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Remove Hidden Faces aplicado a {processedCount} objeto(s).");
            }
            else
            {
                EditorUtility.DisplayDialog("Remove Hidden Faces", "No se eliminó ninguna cara (repite el análisis o revisa la selección de oclusores).", "Entendido");
            }

            analyses.Clear();
            SceneView.RepaintAll();
        }

        private static Mesh BuildFilteredMesh(Mesh source, bool[] hiddenPerFace)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            Vector2[] uv = source.uv;
            Vector2[] uv2 = source.uv2;
            Color[] colors = source.colors;
            Color32[] colors32 = source.colors32;

            int subMeshCount = source.subMeshCount;
            List<List<int>> keptTrianglesPerSubmesh = new List<List<int>>(subMeshCount);
            HashSet<int> usedVertices = new HashSet<int>();
            bool anyKept = false;
            int faceCursor = 0;

            for (int s = 0; s < subMeshCount; s++)
            {
                int[] originalTriangles = source.GetTriangles(s);
                List<int> kept = new List<int>(originalTriangles.Length);

                for (int i = 0; i < originalTriangles.Length; i += 3)
                {
                    int faceIndex = faceCursor + (i / 3);
                    bool hidden = faceIndex < hiddenPerFace.Length && hiddenPerFace[faceIndex];
                    if (!hidden)
                    {
                        int i0 = originalTriangles[i];
                        int i1 = originalTriangles[i + 1];
                        int i2 = originalTriangles[i + 2];
                        kept.Add(i0);
                        kept.Add(i1);
                        kept.Add(i2);
                        usedVertices.Add(i0);
                        usedVertices.Add(i1);
                        usedVertices.Add(i2);
                        anyKept = true;
                    }
                }

                keptTrianglesPerSubmesh.Add(kept);
                faceCursor += originalTriangles.Length / 3;
            }

            if (!anyKept)
            {
                return null;
            }

            // Solo se conservan (y re-indexan) los vértices que siguen siendo usados por al menos
            // una cara visible; el resto del buffer de vértices se descarta.
            List<int> orderedOldIndices = new List<int>(usedVertices);
            orderedOldIndices.Sort();

            Dictionary<int, int> oldToNew = new Dictionary<int, int>(orderedOldIndices.Count);
            for (int i = 0; i < orderedOldIndices.Count; i++)
            {
                oldToNew[orderedOldIndices[i]] = i;
            }

            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasTangents = tangents != null && tangents.Length == vertices.Length;
            bool hasUv = uv != null && uv.Length == vertices.Length;
            bool hasUv2 = uv2 != null && uv2.Length == vertices.Length;
            bool hasColors = colors != null && colors.Length == vertices.Length;
            bool hasColors32 = colors32 != null && colors32.Length == vertices.Length;

            List<Vector3> newVertices = new List<Vector3>(orderedOldIndices.Count);
            List<Vector3> newNormals = hasNormals ? new List<Vector3>(orderedOldIndices.Count) : null;
            List<Vector4> newTangents = hasTangents ? new List<Vector4>(orderedOldIndices.Count) : null;
            List<Vector2> newUv = hasUv ? new List<Vector2>(orderedOldIndices.Count) : null;
            List<Vector2> newUv2 = hasUv2 ? new List<Vector2>(orderedOldIndices.Count) : null;
            List<Color> newColors = hasColors ? new List<Color>(orderedOldIndices.Count) : null;
            List<Color32> newColors32 = hasColors32 ? new List<Color32>(orderedOldIndices.Count) : null;

            foreach (int oldIndex in orderedOldIndices)
            {
                newVertices.Add(vertices[oldIndex]);
                newNormals?.Add(normals[oldIndex]);
                newTangents?.Add(tangents[oldIndex]);
                newUv?.Add(uv[oldIndex]);
                newUv2?.Add(uv2[oldIndex]);
                newColors?.Add(colors[oldIndex]);
                newColors32?.Add(colors32[oldIndex]);
            }

            Mesh result = new Mesh
            {
                name = source.name,
                indexFormat = newVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            result.SetVertices(newVertices);
            result.subMeshCount = subMeshCount;

            for (int s = 0; s < subMeshCount; s++)
            {
                List<int> original = keptTrianglesPerSubmesh[s];
                List<int> remapped = new List<int>(original.Count);
                foreach (int oldIndex in original)
                {
                    remapped.Add(oldToNew[oldIndex]);
                }
                result.SetTriangles(remapped, s);
            }

            if (newNormals != null)
            {
                result.SetNormals(newNormals);
            }
            else
            {
                result.RecalculateNormals();
            }

            if (newTangents != null) result.SetTangents(newTangents);
            if (newUv != null) result.SetUVs(0, newUv);
            if (newUv2 != null) result.SetUVs(1, newUv2);
            if (newColors != null) result.SetColors(newColors);
            if (newColors32 != null) result.SetColors(newColors32);

            result.RecalculateBounds();

            return result;
        }

        private static void SaveFilteredMeshAsset(Mesh mesh, Mesh originalMesh)
        {
            string folderPath = "Assets";
            bool folderResolved = false;

            if (outputFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(outputFolder);
                if (AssetDatabase.IsValidFolder(path))
                {
                    folderPath = path;
                    folderResolved = true;
                }
            }

            if (!folderResolved)
            {
                string originalPath = AssetDatabase.GetAssetPath(originalMesh);
                if (!string.IsNullOrEmpty(originalPath))
                {
                    string originalDir = Path.GetDirectoryName(originalPath);
                    if (!string.IsNullOrEmpty(originalDir))
                    {
                        folderPath = originalDir;
                    }
                }
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, mesh.name + ".asset").Replace("\\", "/"));
            AssetDatabase.CreateAsset(mesh, assetPath);
            Debug.Log($"Mesh sin caras ocultas guardado en: {assetPath}");
        }

        public static void OnSceneGUI(SceneView sceneView)
        {
            if (!showHiddenFacesInSceneView || analyses.Count == 0)
            {
                return;
            }

            Handles.color = new Color(1f, 0.15f, 0.15f, 0.35f);
            foreach (TargetAnalysis analysis in analyses)
            {
                if (analysis.GameObject == null)
                {
                    continue;
                }

                for (int f = 0; f < analysis.FaceCount; f++)
                {
                    if (!analysis.Hidden[f])
                    {
                        continue;
                    }

                    Handles.DrawAAConvexPolygon(analysis.FaceVertexA[f], analysis.FaceVertexB[f], analysis.FaceVertexC[f]);
                }
            }
        }

        public static void EnableSceneView()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            SceneView.RepaintAll();
        }

        public static void DisableSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }
    }
}
