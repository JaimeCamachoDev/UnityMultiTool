using System;
using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Segundo paso al preparar un VAT Multiple Mesh: pinta en la escena, con un pincel,
    // copias de las mallas/materiales ya preparadas en VAT UV Visual, sobre una superficie
    // con MeshCollider. El resultado (varias instancias compartiendo un mismo material VAT)
    // queda listo para combinarse en un único draw call con VAT Combiner.
    public static class VATPainterTool
    {
        private const string PaintRootName = "VATPaintRoot";

        [Serializable]
        private class PaintGroup
        {
            public string groupName = "Grupo";
            public string id = Guid.NewGuid().ToString("N");
            public readonly List<MeshFilter> meshFilters = new List<MeshFilter>();
            public readonly List<Material> vatMaterials = new List<Material>();
            public bool isExpanded = true;
        }

        private static readonly List<PaintGroup> paintGroups = new List<PaintGroup>();
        private static readonly Dictionary<PaintGroup, List<Transform>> paintGroupParents = new Dictionary<PaintGroup, List<Transform>>();

        private static Transform painterFocusTarget;
        private static GameObject painterSurface;
        private static MeshCollider painterSurfaceCollider;
        private static bool painterPaintingMode;
        private static GameObject painterRoot;
        private static float painterBrushRadius = 2f;
        private static int painterBrushDensity = 5;
        private static float painterMinDistance = 0.5f;

        private static readonly Color BrushFillColor = new Color(0f, 0.5f, 1f, 0.25f);
        private static readonly Color BrushOutlineColor = Color.cyan;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT Painter") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Pinta con un pincel, directamente en la escena, copias de las mallas/materiales VAT que preparaste en VAT UV Visual sobre una superficie con MeshCollider. Cada grupo combina varias mallas y materiales para generar variedad automática. Cuando termines, combínalas con VAT Combiner.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();
                BuildBody(contentContainer, RefreshContent);
            }

            RefreshContent();

            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (painterPaintingMode)
                {
                    TogglePaintingMode(false);
                }
            });

            return root;
        }

        private static void BuildBody(VisualElement container, Action refresh)
        {
            var groupsButtonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            groupsButtonsRow.Add(new MTUIActionButton("Añadir grupo de pintado", () =>
            {
                paintGroups.Add(new PaintGroup { groupName = GenerateUniqueGroupName() });
                refresh();
            }));

            var clearButton = new MTUIActionButton("Limpiar instancias pintadas", () =>
            {
                ClearPaintedInstances();
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            clearButton.style.marginLeft = 6;
            clearButton.SetAvailable(GetPaintRoot(false) != null);
            groupsButtonsRow.Add(clearButton);
            container.Add(groupsButtonsRow);

            for (int i = 0; i < paintGroups.Count; i++)
            {
                container.Add(BuildPaintGroupPanel(paintGroups[i], refresh));
            }

            if (paintGroups.Count == 0)
            {
                container.Add(new HelpBox("No hay grupos de pintado definidos. Añade uno para comenzar a colocar VATs.", HelpBoxMessageType.Info));
            }

            var setupPanel = new MTUIPanel("Configuración del pincel") { style = { marginTop = 10 } };

            var focusField = new ObjectField("Objetivo de enfoque") { objectType = typeof(Transform), allowSceneObjects = true, value = painterFocusTarget };
            focusField.RegisterValueChangedCallback(evt => painterFocusTarget = evt.newValue as Transform);
            setupPanel.Add(focusField);

            var surfaceField = new ObjectField("Superficie de pintado") { objectType = typeof(GameObject), allowSceneObjects = true, value = painterSurface };
            surfaceField.RegisterValueChangedCallback(evt =>
            {
                painterSurface = evt.newValue as GameObject;
                UpdatePaintSurfaceCollider();
                refresh();
            });
            setupPanel.Add(surfaceField);

            if (painterSurface == null)
            {
                setupPanel.Add(new HelpBox("Asigna una superficie con MeshCollider para recibir los trazos del pincel.", HelpBoxMessageType.Info));
            }
            else if (painterSurfaceCollider == null)
            {
                setupPanel.Add(new HelpBox("La superficie seleccionada no tiene un MeshCollider.", HelpBoxMessageType.Warning));
            }

            var radiusSlider = new Slider("Radio del pincel", 0.05f, 25f) { value = painterBrushRadius };
            radiusSlider.RegisterValueChangedCallback(evt => painterBrushRadius = evt.newValue);
            setupPanel.Add(radiusSlider);

            var densitySlider = new SliderInt("Densidad del pincel", 1, 64) { value = painterBrushDensity };
            densitySlider.RegisterValueChangedCallback(evt => painterBrushDensity = evt.newValue);
            setupPanel.Add(densitySlider);

            var minDistanceSlider = new Slider("Distancia mínima entre instancias", 0f, 10f) { value = painterMinDistance };
            minDistanceSlider.RegisterValueChangedCallback(evt => painterMinDistance = evt.newValue);
            setupPanel.Add(minDistanceSlider);

            if (!HasAnyValidPaintGroup())
            {
                setupPanel.Add(new HelpBox("Crea al menos un grupo con Mesh Filters y materiales VAT válidos para poder pintar.", HelpBoxMessageType.Warning));
            }

            if (painterFocusTarget == null)
            {
                setupPanel.Add(new HelpBox("Sin objetivo de enfoque las instancias pintadas conservarán su orientación original.", HelpBoxMessageType.Info));
            }

            container.Add(setupPanel);

            bool canPaint = painterSurfaceCollider != null && HasAnyValidPaintGroup();
            var paintToggle = new Toggle("Activar modo de pintado") { value = painterPaintingMode };
            paintToggle.SetEnabled(canPaint || painterPaintingMode);
            paintToggle.RegisterValueChangedCallback(evt =>
            {
                TogglePaintingMode(evt.newValue);
                refresh();
            });
            paintToggle.style.marginTop = 6;
            container.Add(paintToggle);

            if (painterPaintingMode)
            {
                container.Add(new HelpBox("Haz clic izquierdo en la vista de escena para pintar instancias VAT. Mantén Alt para seguir navegando con la cámara.", HelpBoxMessageType.Info));
            }
        }

        private static VisualElement BuildPaintGroupPanel(PaintGroup group, Action refresh)
        {
            var panel = new MTUIPanel(string.Empty) { style = { marginTop = 6 } };

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var foldout = new Foldout { text = group.groupName, value = group.isExpanded, style = { flexGrow = 1 } };
            foldout.RegisterValueChangedCallback(evt => group.isExpanded = evt.newValue);
            headerRow.Add(foldout);

            var removeButton = new MTUIActionButton("Eliminar", () =>
            {
                paintGroupParents.Remove(group);
                paintGroups.Remove(group);
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            removeButton.style.width = 70;
            headerRow.Add(removeButton);
            panel.Add(headerRow);

            var nameField = new TextField("Nombre del grupo") { value = group.groupName, style = { marginTop = 4 } };
            nameField.RegisterValueChangedCallback(evt =>
            {
                group.groupName = evt.newValue;
                foldout.text = evt.newValue;
                paintGroupParents.Remove(group);
            });
            foldout.Add(nameField);

            foldout.Add(BuildMeshFilterList(group, refresh));
            foldout.Add(BuildMaterialList(group, refresh));

            panel.Add(foldout);
            return panel;
        }

        private static VisualElement BuildMeshFilterList(PaintGroup group, Action refresh)
        {
            var container = new VisualElement { style = { marginTop = 6 } };
            container.Add(new Label("Mesh Filters (mallas origen)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            for (int i = 0; i < group.meshFilters.Count; i++)
            {
                int index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 10 } };

                var field = new ObjectField { objectType = typeof(MeshFilter), allowSceneObjects = true, value = group.meshFilters[index], style = { flexGrow = 1 } };
                field.RegisterValueChangedCallback(evt =>
                {
                    group.meshFilters[index] = evt.newValue as MeshFilter;
                    paintGroupParents.Remove(group);
                });
                row.Add(field);

                var removeButton = new MTUIActionButton("X", () =>
                {
                    group.meshFilters.RemoveAt(index);
                    paintGroupParents.Remove(group);
                    refresh();
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                removeButton.style.width = 24;
                removeButton.style.marginLeft = 4;
                row.Add(removeButton);

                container.Add(row);
            }

            var buttonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 10, marginTop = 2 } };
            buttonsRow.Add(new MTUIActionButton("Añadir Mesh Filter", () =>
            {
                group.meshFilters.Add(null);
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));

            var addSelectionButton = new MTUIActionButton("Añadir selección", () =>
            {
                bool added = false;
                foreach (GameObject go in Selection.gameObjects)
                {
                    MeshFilter filter = ResolveMeshFilter(go);
                    if (filter != null && !group.meshFilters.Contains(filter))
                    {
                        group.meshFilters.Add(filter);
                        added = true;
                    }
                }

                if (added)
                {
                    paintGroupParents.Remove(group);
                    refresh();
                }
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            addSelectionButton.style.marginLeft = 6;
            buttonsRow.Add(addSelectionButton);
            container.Add(buttonsRow);

            return container;
        }

        private static VisualElement BuildMaterialList(PaintGroup group, Action refresh)
        {
            var container = new VisualElement { style = { marginTop = 6 } };
            container.Add(new Label("Materiales VAT") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            for (int i = 0; i < group.vatMaterials.Count; i++)
            {
                int index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 10 } };

                var field = new ObjectField { objectType = typeof(Material), allowSceneObjects = false, value = group.vatMaterials[index], style = { flexGrow = 1 } };
                field.RegisterValueChangedCallback(evt => group.vatMaterials[index] = evt.newValue as Material);
                row.Add(field);

                var removeButton = new MTUIActionButton("X", () =>
                {
                    group.vatMaterials.RemoveAt(index);
                    paintGroupParents.Remove(group);
                    refresh();
                }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                removeButton.style.width = 24;
                removeButton.style.marginLeft = 4;
                row.Add(removeButton);

                container.Add(row);
            }

            var buttonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginLeft = 10, marginTop = 2 } };
            buttonsRow.Add(new MTUIActionButton("Añadir material", () =>
            {
                group.vatMaterials.Add(null);
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));

            var addSelectionButton = new MTUIActionButton("Añadir selección", () =>
            {
                bool added = false;
                foreach (GameObject go in Selection.gameObjects)
                {
                    Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
                    if (renderer == null)
                    {
                        continue;
                    }

                    foreach (Material shared in renderer.sharedMaterials)
                    {
                        if (shared != null && !group.vatMaterials.Contains(shared))
                        {
                            group.vatMaterials.Add(shared);
                            added = true;
                        }
                    }
                }

                if (added)
                {
                    paintGroupParents.Remove(group);
                    refresh();
                }
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            addSelectionButton.style.marginLeft = 6;
            buttonsRow.Add(addSelectionButton);
            container.Add(buttonsRow);

            return container;
        }

        private static MeshFilter ResolveMeshFilter(GameObject go)
        {
            if (go == null)
            {
                return null;
            }

            return go.GetComponent<MeshFilter>() ?? go.GetComponentInChildren<MeshFilter>(true);
        }

        private static string GenerateUniqueGroupName()
        {
            const string baseName = "Grupo";
            int index = paintGroups.Count + 1;

            while (true)
            {
                string candidate = $"{baseName} {index}";
                bool exists = false;
                foreach (PaintGroup group in paintGroups)
                {
                    if (string.Equals(group.groupName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    return candidate;
                }

                index++;
            }
        }

        private static bool HasAnyValidPaintGroup()
        {
            foreach (PaintGroup group in paintGroups)
            {
                if (GroupHasValidMesh(group) && GroupHasValidMaterial(group))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GroupHasValidMesh(PaintGroup group)
        {
            foreach (MeshFilter filter in group.meshFilters)
            {
                if (filter != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool GroupHasValidMaterial(PaintGroup group)
        {
            foreach (Material material in group.vatMaterials)
            {
                if (material != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdatePaintSurfaceCollider()
        {
            painterSurfaceCollider = painterSurface != null ? painterSurface.GetComponent<MeshCollider>() : null;
        }

        private static GameObject GetPaintRoot(bool createIfMissing)
        {
            if (painterRoot != null)
            {
                return painterRoot;
            }

            painterRoot = GameObject.Find(PaintRootName);
            if (painterRoot == null && createIfMissing)
            {
                painterRoot = new GameObject(PaintRootName);
                Undo.RegisterCreatedObjectUndo(painterRoot, "Crear raíz de pintado VAT");
            }

            return painterRoot;
        }

        private static void ClearPaintedInstances()
        {
            GameObject root = GetPaintRoot(false);
            if (root == null)
            {
                return;
            }

            Undo.DestroyObjectImmediate(root);
            painterRoot = null;
            paintGroupParents.Clear();
        }

        private static void TogglePaintingMode(bool enable)
        {
            if (painterPaintingMode == enable)
            {
                return;
            }

            painterPaintingMode = enable;

            if (enable)
            {
                UpdatePaintSurfaceCollider();
                SceneView.duringSceneGui += HandlePainterSceneGUI;
            }
            else
            {
                SceneView.duringSceneGui -= HandlePainterSceneGUI;
                painterRoot = null;
                paintGroupParents.Clear();
            }

            SceneView.RepaintAll();
        }

        private static bool PainterHasValidSetup()
        {
            return painterSurfaceCollider != null && HasAnyValidPaintGroup();
        }

        private static void HandlePainterSceneGUI(SceneView sceneView)
        {
            if (!painterPaintingMode)
            {
                return;
            }

            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (!PainterHasValidSetup())
            {
                return;
            }

            Ray guiRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!TryGetPaintHit(guiRay, out RaycastHit hit))
            {
                return;
            }

            DrawBrushPreview(hit);

            if (current.alt)
            {
                return;
            }

            bool shouldPaint = (current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0;
            if (shouldPaint && PaintAtRayHit(hit))
            {
                current.Use();
            }
        }

        private static bool TryGetPaintHit(Ray ray, out RaycastHit hit)
        {
            if (painterSurfaceCollider != null)
            {
                return painterSurfaceCollider.Raycast(ray, out hit, 10000f);
            }

            hit = default;
            return false;
        }

        private static void DrawBrushPreview(RaycastHit hit)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = BrushFillColor;
            Handles.DrawSolidDisc(hit.point, hit.normal, painterBrushRadius);
            Handles.color = BrushOutlineColor;
            Handles.DrawWireDisc(hit.point, hit.normal, painterBrushRadius);
        }

        private static void BuildBrushFrame(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-6f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }

            tangent.Normalize();
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        private static bool PaintAtRayHit(RaycastHit hit)
        {
            bool paintedAny = false;

            BuildBrushFrame(hit.normal, out Vector3 tangent, out Vector3 bitangent);

            for (int i = 0; i < painterBrushDensity; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * painterBrushRadius;
                Vector3 samplePoint = hit.point + tangent * offset.x + bitangent * offset.y;
                Ray offsetRay = new Ray(samplePoint + hit.normal * 0.5f, -hit.normal);

                if (!TryGetPaintHit(offsetRay, out RaycastHit offsetHit))
                {
                    continue;
                }

                if (painterMinDistance > 0f && IsTooClose(offsetHit.point))
                {
                    continue;
                }

                if (PaintInstanceAt(offsetHit.point, offsetHit.normal))
                {
                    paintedAny = true;
                }
            }

            if (paintedAny)
            {
                SceneView.RepaintAll();
            }

            return paintedAny;
        }

        private static bool PaintInstanceAt(Vector3 position, Vector3 normal)
        {
            PaintGroup group = GetRandomValidGroup();
            if (group == null)
            {
                return false;
            }

            MeshFilter sourceFilter = GetRandomMeshFilter(group);
            if (sourceFilter == null)
            {
                return false;
            }

            Material material = GetRandomMaterial(group, out int materialIndex);
            if (material == null)
            {
                return false;
            }

            GameObject instance = CreateInstanceFromSource(sourceFilter.gameObject);
            if (instance == null)
            {
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Pintar instancia VAT");

            AlignPaintedInstance(instance.transform, position, normal);

            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Asignar material VAT");
                renderer.sharedMaterial = material;
            }

            Transform parent = GetOrCreateGroupParent(group, materialIndex);
            if (parent != null)
            {
                Undo.SetTransformParent(instance.transform, parent, "Asignar contenedor de pintado");
            }

            instance.transform.position = position;

            return true;
        }

        private static readonly List<PaintGroup> reusablePaintGroups = new List<PaintGroup>();
        private static readonly List<int> reusableMeshFilterIndices = new List<int>();
        private static readonly List<int> reusableMaterialIndices = new List<int>();

        private static PaintGroup GetRandomValidGroup()
        {
            reusablePaintGroups.Clear();

            foreach (PaintGroup group in paintGroups)
            {
                if (GroupHasValidMesh(group) && GroupHasValidMaterial(group))
                {
                    reusablePaintGroups.Add(group);
                }
            }

            if (reusablePaintGroups.Count == 0)
            {
                return null;
            }

            return reusablePaintGroups[UnityEngine.Random.Range(0, reusablePaintGroups.Count)];
        }

        private static MeshFilter GetRandomMeshFilter(PaintGroup group)
        {
            reusableMeshFilterIndices.Clear();

            for (int i = 0; i < group.meshFilters.Count; i++)
            {
                if (group.meshFilters[i] != null)
                {
                    reusableMeshFilterIndices.Add(i);
                }
            }

            if (reusableMeshFilterIndices.Count == 0)
            {
                return null;
            }

            int selected = reusableMeshFilterIndices[UnityEngine.Random.Range(0, reusableMeshFilterIndices.Count)];
            return group.meshFilters[selected];
        }

        private static Material GetRandomMaterial(PaintGroup group, out int materialIndex)
        {
            reusableMaterialIndices.Clear();

            for (int i = 0; i < group.vatMaterials.Count; i++)
            {
                if (group.vatMaterials[i] != null)
                {
                    reusableMaterialIndices.Add(i);
                }
            }

            if (reusableMaterialIndices.Count == 0)
            {
                materialIndex = -1;
                return null;
            }

            materialIndex = reusableMaterialIndices[UnityEngine.Random.Range(0, reusableMaterialIndices.Count)];
            return group.vatMaterials[materialIndex];
        }

        private static GameObject CreateInstanceFromSource(GameObject source)
        {
            if (source == null)
            {
                return null;
            }

            GameObject instance = null;

            if (PrefabUtility.IsPartOfPrefabAsset(source))
            {
                instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            }
            else if (PrefabUtility.IsPartOfPrefabInstance(source))
            {
                GameObject prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(source);
                if (prefabRoot != null)
                {
                    instance = PrefabUtility.InstantiatePrefab(prefabRoot) as GameObject;
                }
            }

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(source);
            }

            instance.name = source.name;
            return instance;
        }

        private static void AlignPaintedInstance(Transform instanceTransform, Vector3 position, Vector3 surfaceNormal)
        {
            if (instanceTransform == null)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            if (painterFocusTarget != null)
            {
                Bounds bounds = GetFocusTargetBounds();
                Vector3 lookAtPoint = bounds.ClosestPoint(position);
                Vector3 direction = lookAtPoint - position;
                direction.y = 0f;

                if (direction.sqrMagnitude > 1e-4f)
                {
                    rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            Quaternion normalAlignment = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            instanceTransform.rotation = normalAlignment * rotation;
        }

        private static Bounds GetFocusTargetBounds()
        {
            if (painterFocusTarget == null)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Renderer[] renderers = painterFocusTarget.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }

                return combined;
            }

            return new Bounds(painterFocusTarget.position, Vector3.one);
        }

        private static bool IsTooClose(Vector3 point)
        {
            GameObject root = GetPaintRoot(false);
            if (root == null)
            {
                return false;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                if (child == null || child == root.transform)
                {
                    continue;
                }

                if (child.GetComponent<MeshRenderer>() == null && child.GetComponent<MeshFilter>() == null)
                {
                    continue;
                }

                if (Vector3.Distance(child.position, point) < painterMinDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform GetOrCreateGroupParent(PaintGroup group, int materialIndex)
        {
            GameObject root = GetPaintRoot(true);
            if (root == null)
            {
                return null;
            }

            if (!paintGroupParents.TryGetValue(group, out List<Transform> parents))
            {
                parents = new List<Transform>();
                paintGroupParents[group] = parents;
            }

            while (parents.Count <= materialIndex)
            {
                parents.Add(null);
            }

            string suffix = $"_{materialIndex + 1}_{group.id}";
            string baseName = string.IsNullOrWhiteSpace(group.groupName) ? "Group" : group.groupName.Trim();
            string targetName = $"{baseName}{suffix}";

            Transform parent = parents[materialIndex];
            if (parent == null)
            {
                parent = FindChildBySuffix(root.transform, suffix);
                if (parent == null)
                {
                    var container = new GameObject(targetName);
                    Undo.RegisterCreatedObjectUndo(container, "Crear contenedor de grupo VAT");
                    container.transform.SetParent(root.transform, false);
                    parent = container.transform;
                }
            }

            parent.name = targetName;
            parent.SetParent(root.transform, false);
            parents[materialIndex] = parent;

            return parent;
        }

        private static Transform FindChildBySuffix(Transform root, string suffix)
        {
            if (root == null)
            {
                return null;
            }

            foreach (Transform child in root)
            {
                if (child != null && child.name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
