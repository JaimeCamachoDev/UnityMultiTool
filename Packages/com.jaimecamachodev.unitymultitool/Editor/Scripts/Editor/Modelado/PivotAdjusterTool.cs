using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class PivotAdjusterTool
    {
        private enum PivotAnchor
        {
            Custom,
            BoundsCenter,
            BottomCenter,
            TopCenter,
            Left,
            Right,
            Front,
            Back
        }

        private static PivotAnchor pivotAnchor = PivotAnchor.BoundsCenter;
        private static bool includeChildrenBounds = true;
        private static bool applyPerObject = true;
        private static bool preserveChildren = true;
        private static bool updateMeshColliders = true;
        private static bool createNewMeshInstance = true;
        private static bool saveNewMeshAsAsset = true;
        private static bool alignHandleToActiveRotation = false;
        private static bool showAdvancedOptions;
        private static bool showReferenceOptions = true;
        private static bool enablePivotGridSnap;
        private static float pivotGridSize = 0.1f;
        private static DefaultAsset pivotAssetFolder;
        private static bool useReferenceObject;
        private static bool autoFollowReference = true;
        private static bool referenceUseBounds = true;
        private static Vector3 referenceLocalOffset = Vector3.zero;
        private static GameObject pivotReferenceObject;

        private static Vector3 customPivotWorld;
        private static bool pivotInitialized;
        private static bool sceneHooked;
        private static bool assetSaveRequested;

        public static void EnableSceneView()
        {
            if (sceneHooked)
            {
                return;
            }

            sceneHooked = true;
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
            UpdateHandleFromSelection(true);
        }

        public static void DisableSceneView()
        {
            if (!sceneHooked)
            {
                return;
            }

            sceneHooked = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            Selection.selectionChanged -= OnSelectionChanged;
        }

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Pivot mover & aligner") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Mueve el pivote de props estáticos usando el gizmo cyan en la escena o presets alineados al bound.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                if (Selection.gameObjects.Length == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona uno o más objetos para modificar su pivote.", HelpBoxMessageType.Warning));
                    return;
                }

                var customPivotContainer = new VisualElement();
                void RefreshCustomPivotSection()
                {
                    customPivotContainer.Clear();
                    if (pivotAnchor == PivotAnchor.Custom)
                    {
                        var pivotField = new Vector3Field("Pivote personalizado (mundo)") { value = customPivotWorld };
                        pivotField.RegisterValueChangedCallback(evt => customPivotWorld = evt.newValue);
                        customPivotContainer.Add(pivotField);

                        customPivotContainer.Add(new Button(() =>
                        {
                            UpdateHandleFromSelection(true);
                            var field = customPivotContainer.Q<Vector3Field>();
                            field?.SetValueWithoutNotify(customPivotWorld);
                        })
                        { text = "Centrar gizmo en objeto activo" });
                    }
                    else
                    {
                        customPivotContainer.Add(new HelpBox("El pivote se tomará del preset seleccionado. Puedes refinarlo moviendo el gizmo.", HelpBoxMessageType.None));
                    }
                }

                var pivotAnchorField = new EnumField("Preset de pivote", pivotAnchor);
                pivotAnchorField.RegisterValueChangedCallback(evt =>
                {
                    PivotAnchor newAnchor = (PivotAnchor)evt.newValue;
                    if (newAnchor != pivotAnchor)
                    {
                        pivotAnchor = newAnchor;
                        if (pivotAnchor != PivotAnchor.Custom)
                        {
                            UpdateHandleFromSelection(false);
                        }
                        RefreshCustomPivotSection();
                    }
                });
                contentContainer.Add(pivotAnchorField);

                var includeChildrenBoundsToggle = new Toggle("Calcular bounds incluyendo hijos") { value = includeChildrenBounds };
                includeChildrenBoundsToggle.RegisterValueChangedCallback(evt => includeChildrenBounds = evt.newValue);
                contentContainer.Add(includeChildrenBoundsToggle);

                var applyPerObjectToggle = new Toggle("Calcular preset por objeto") { value = applyPerObject };
                applyPerObjectToggle.RegisterValueChangedCallback(evt => applyPerObject = evt.newValue);
                contentContainer.Add(applyPerObjectToggle);

                var advancedFoldout = new Foldout { text = "Ajustes avanzados", value = showAdvancedOptions, style = { marginTop = 6 } };
                advancedFoldout.RegisterValueChangedCallback(evt => showAdvancedOptions = evt.newValue);

                var preserveChildrenToggle = new Toggle("Mantener posición global de los hijos") { value = preserveChildren };
                preserveChildrenToggle.RegisterValueChangedCallback(evt => preserveChildren = evt.newValue);
                advancedFoldout.Add(preserveChildrenToggle);

                var updateMeshCollidersToggle = new Toggle("Actualizar MeshCollider si existe") { value = updateMeshColliders };
                updateMeshCollidersToggle.RegisterValueChangedCallback(evt => updateMeshColliders = evt.newValue);
                advancedFoldout.Add(updateMeshCollidersToggle);

                var createNewMeshInstanceToggle = new Toggle("Duplicar mesh antes de editar") { value = createNewMeshInstance };
                advancedFoldout.Add(createNewMeshInstanceToggle);

                var saveNewMeshAsAssetToggle = new Toggle("Guardar mesh duplicado como asset") { value = saveNewMeshAsAsset, style = { marginLeft = 15 } };
                var pivotAssetFolderField = new ObjectField("Carpeta destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = pivotAssetFolder, style = { marginLeft = 30 } };
                saveNewMeshAsAssetToggle.SetEnabled(createNewMeshInstance);
                pivotAssetFolderField.SetEnabled(createNewMeshInstance);
                createNewMeshInstanceToggle.RegisterValueChangedCallback(evt =>
                {
                    createNewMeshInstance = evt.newValue;
                    saveNewMeshAsAssetToggle.SetEnabled(createNewMeshInstance);
                    pivotAssetFolderField.SetEnabled(createNewMeshInstance);
                });
                saveNewMeshAsAssetToggle.RegisterValueChangedCallback(evt => saveNewMeshAsAsset = evt.newValue);
                pivotAssetFolderField.RegisterValueChangedCallback(evt => pivotAssetFolder = evt.newValue as DefaultAsset);
                advancedFoldout.Add(saveNewMeshAsAssetToggle);
                advancedFoldout.Add(pivotAssetFolderField);

                var alignHandleToggle = new Toggle("Alinear gizmo a la rotación del activo") { value = alignHandleToActiveRotation };
                alignHandleToggle.RegisterValueChangedCallback(evt => alignHandleToActiveRotation = evt.newValue);
                advancedFoldout.Add(alignHandleToggle);

                var enableSnapToggle = new Toggle("Activar snap en la escena") { value = enablePivotGridSnap };
                var gridSizeField = new FloatField("Tamaño del snap (m)") { value = Mathf.Max(0.001f, pivotGridSize) };
                gridSizeField.SetEnabled(enablePivotGridSnap);
                enableSnapToggle.RegisterValueChangedCallback(evt =>
                {
                    enablePivotGridSnap = evt.newValue;
                    gridSizeField.SetEnabled(enablePivotGridSnap);
                });
                gridSizeField.RegisterValueChangedCallback(evt => pivotGridSize = Mathf.Max(0.001f, evt.newValue));
                advancedFoldout.Add(enableSnapToggle);
                advancedFoldout.Add(gridSizeField);

                contentContainer.Add(advancedFoldout);

                RefreshCustomPivotSection();
                contentContainer.Add(customPivotContainer);

                var referenceHelpBoxContainer = new VisualElement();
                void RefreshReferenceHelpBox()
                {
                    referenceHelpBoxContainer.Clear();
                    if (useReferenceObject && pivotReferenceObject != null && !autoFollowReference)
                    {
                        referenceHelpBoxContainer.Add(new HelpBox("El gizmo usa la posición de la referencia actual pero el seguimiento automático está desactivado.", HelpBoxMessageType.Info));
                    }
                }

                var referenceFoldout = new Foldout { text = "Referencia externa", value = showReferenceOptions, style = { marginTop = 6 } };
                referenceFoldout.RegisterValueChangedCallback(evt => showReferenceOptions = evt.newValue);

                var useReferenceToggle = new Toggle("Usar objeto como referencia") { value = useReferenceObject };
                referenceFoldout.Add(useReferenceToggle);

                var referenceObjectField = new ObjectField("Objeto de referencia") { objectType = typeof(GameObject), allowSceneObjects = true, value = pivotReferenceObject, style = { marginLeft = 15 } };
                var referenceUseBoundsToggle = new Toggle("Tomar centro del bound de la referencia") { value = referenceUseBounds, style = { marginLeft = 15 } };
                var referenceOffsetField = new Vector3Field("Offset local adicional") { value = referenceLocalOffset, style = { marginLeft = 15 } };
                var autoFollowToggle = new Toggle("Mantener gizmo sincronizado con la referencia") { value = autoFollowReference, style = { marginLeft = 15 } };
                var alignButton = new Button(() => AlignGizmoToReference(true)) { text = "Alinear gizmo a la referencia", style = { marginLeft = 15 } };

                void SetReferenceControlsEnabled(bool enabled)
                {
                    referenceObjectField.SetEnabled(enabled);
                    referenceUseBoundsToggle.SetEnabled(enabled);
                    referenceOffsetField.SetEnabled(enabled);
                    autoFollowToggle.SetEnabled(enabled);
                    alignButton.SetEnabled(enabled);
                }

                SetReferenceControlsEnabled(useReferenceObject);

                useReferenceToggle.RegisterValueChangedCallback(evt =>
                {
                    bool wasEnabled = useReferenceObject;
                    useReferenceObject = evt.newValue;
                    SetReferenceControlsEnabled(useReferenceObject);
                    if (useReferenceObject && !wasEnabled)
                    {
                        AlignGizmoToReference(true);
                    }
                    RefreshReferenceHelpBox();
                });

                referenceObjectField.RegisterValueChangedCallback(evt =>
                {
                    GameObject previousReference = pivotReferenceObject;
                    pivotReferenceObject = evt.newValue as GameObject;
                    if (pivotReferenceObject != null && pivotReferenceObject != previousReference)
                    {
                        AlignGizmoToReference(true);
                    }
                    RefreshReferenceHelpBox();
                });

                referenceUseBoundsToggle.RegisterValueChangedCallback(evt =>
                {
                    bool previous = referenceUseBounds;
                    referenceUseBounds = evt.newValue;
                    if (autoFollowReference && referenceUseBounds != previous)
                    {
                        AlignGizmoToReference(true);
                    }
                });

                referenceOffsetField.RegisterValueChangedCallback(evt =>
                {
                    Vector3 previous = referenceLocalOffset;
                    referenceLocalOffset = evt.newValue;
                    if (autoFollowReference && previous != referenceLocalOffset)
                    {
                        AlignGizmoToReference(true);
                    }
                });

                autoFollowToggle.RegisterValueChangedCallback(evt =>
                {
                    bool previous = autoFollowReference;
                    autoFollowReference = evt.newValue;
                    if (autoFollowReference && !previous)
                    {
                        AlignGizmoToReference(true);
                    }
                    RefreshReferenceHelpBox();
                });

                referenceFoldout.Add(referenceObjectField);
                referenceFoldout.Add(referenceUseBoundsToggle);
                referenceFoldout.Add(referenceOffsetField);
                referenceFoldout.Add(autoFollowToggle);
                referenceFoldout.Add(alignButton);

                contentContainer.Add(referenceFoldout);

                RefreshReferenceHelpBox();
                contentContainer.Add(referenceHelpBoxContainer);

                contentContainer.Add(new Button(ApplyPivotToSelection)
                {
                    text = "Aplicar pivote a la selección",
                    style = { marginTop = 10 }
                });
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Selection.activeTransform == null)
            {
                return;
            }

            if (!pivotInitialized)
            {
                UpdateHandleFromSelection(true);
            }

            if (useReferenceObject && pivotReferenceObject != null && autoFollowReference && pivotAnchor == PivotAnchor.Custom)
            {
                AlignGizmoToReference(false, false);
            }

            Handles.color = new Color(0f, 0.82f, 0.98f, 0.95f);
            Quaternion handleRotation = alignHandleToActiveRotation && Selection.activeTransform != null
                ? Selection.activeTransform.rotation
                : Quaternion.identity;

            EditorGUI.BeginChangeCheck();
            Vector3 newPosition = Handles.PositionHandle(customPivotWorld, handleRotation);
            if (enablePivotGridSnap && pivotGridSize > 0f)
            {
                newPosition = SnapVector(newPosition, pivotGridSize);
            }
            if (EditorGUI.EndChangeCheck())
            {
                customPivotWorld = newPosition;
                pivotAnchor = PivotAnchor.Custom;
                autoFollowReference = false;
                SceneView.RepaintAll();
            }

            Handles.SphereHandleCap(0, customPivotWorld, Quaternion.identity, HandleUtility.GetHandleSize(customPivotWorld) * 0.08f, EventType.Repaint);

            if (useReferenceObject && pivotReferenceObject != null)
            {
                Handles.color = new Color(0f, 0.82f, 0.98f, 0.4f);
                Handles.DrawDottedLine(pivotReferenceObject.transform.position, customPivotWorld, 4f);
            }
        }

        private static void OnSelectionChanged()
        {
            UpdateHandleFromSelection(true);
            SceneView.RepaintAll();
        }

        private static void UpdateHandleFromSelection(bool resetCustom)
        {
            if (Selection.activeGameObject == null)
            {
                pivotInitialized = false;
                return;
            }

            if (resetCustom && pivotAnchor == PivotAnchor.Custom && (!useReferenceObject || pivotReferenceObject == null))
            {
                pivotAnchor = PivotAnchor.BoundsCenter;
            }

            customPivotWorld = ResolvePivotWorld(Selection.activeGameObject, pivotAnchor, includeChildrenBounds);
            pivotInitialized = true;
        }

        private static Vector3 ResolvePivotWorld(GameObject target, PivotAnchor anchor, bool includeChildren)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            if (anchor == PivotAnchor.Custom)
            {
                return ResolveCustomPivotWorld();
            }

            Bounds? bounds = CalculateObjectBounds(target, includeChildren);
            if (!bounds.HasValue)
            {
                return target.transform.position;
            }

            Bounds b = bounds.Value;
            switch (anchor)
            {
                case PivotAnchor.BoundsCenter:
                    return b.center;
                case PivotAnchor.BottomCenter:
                    return new Vector3(b.center.x, b.min.y, b.center.z);
                case PivotAnchor.TopCenter:
                    return new Vector3(b.center.x, b.max.y, b.center.z);
                case PivotAnchor.Left:
                    return new Vector3(b.min.x, b.center.y, b.center.z);
                case PivotAnchor.Right:
                    return new Vector3(b.max.x, b.center.y, b.center.z);
                case PivotAnchor.Front:
                    return new Vector3(b.center.x, b.center.y, b.max.z);
                case PivotAnchor.Back:
                    return new Vector3(b.center.x, b.center.y, b.min.z);
                default:
                    return target.transform.position;
            }
        }

        private static Bounds? CalculateObjectBounds(GameObject target, bool includeChildren)
        {
            Bounds? result = null;
            Renderer[] renderers = includeChildren ? target.GetComponentsInChildren<Renderer>() : target.GetComponents<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                if (result.HasValue)
                {
                    Bounds b = result.Value;
                    b.Encapsulate(renderer.bounds);
                    result = b;
                }
                else
                {
                    result = renderer.bounds;
                }
            }

            if (result.HasValue)
            {
                return result;
            }

            MeshFilter[] filters = includeChildren ? target.GetComponentsInChildren<MeshFilter>() : target.GetComponents<MeshFilter>();
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                if (vertices == null || vertices.Length == 0)
                {
                    continue;
                }

                Transform t = filter.transform;
                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = t.TransformPoint(vertex);
                    if (result.HasValue)
                    {
                        Bounds b = result.Value;
                        b.Encapsulate(world);
                        result = b;
                    }
                    else
                    {
                        result = new Bounds(world, Vector3.zero);
                    }
                }
            }

            return result;
        }

        private static void ApplyPivotToSelection()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                return;
            }

            Transform reference = Selection.activeTransform != null ? Selection.activeTransform : selected[0].transform;
            Vector3 sharedPivot = ResolvePivotWorld(reference.gameObject, pivotAnchor, includeChildrenBounds);
            assetSaveRequested = false;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (GameObject go in selected)
            {
                if (go == null)
                {
                    continue;
                }

                Vector3 targetPivot = pivotAnchor == PivotAnchor.Custom ? ResolveCustomPivotWorld() : ResolvePivotWorld(go, pivotAnchor, includeChildrenBounds);
                if (pivotAnchor != PivotAnchor.Custom && !applyPerObject)
                {
                    targetPivot = sharedPivot;
                }

                AdjustPivot(go, targetPivot);
            }

            Undo.CollapseUndoOperations(undoGroup);
            if (assetSaveRequested)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            customPivotWorld = pivotAnchor == PivotAnchor.Custom ? ResolveCustomPivotWorld() : ResolvePivotWorld(reference.gameObject, pivotAnchor, includeChildrenBounds);
            SceneView.RepaintAll();
        }

        private static void AdjustPivot(GameObject go, Vector3 pivotWorld)
        {
            Transform transform = go.transform;

            List<Transform> children = new List<Transform>();
            List<Vector3> childPositions = new List<Vector3>();
            List<Quaternion> childRotations = new List<Quaternion>();
            if (preserveChildren)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    children.Add(child);
                    childPositions.Add(child.position);
                    childRotations.Add(child.rotation);
                }
            }

            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            Mesh sharedMesh = meshFilter != null ? meshFilter.sharedMesh : null;

            Vector3 worldOffset;
            if (sharedMesh != null)
            {
                if (!sharedMesh.isReadable)
                {
                    Debug.LogWarning($"[Pivot] '{go.name}' usa un mesh sin Read/Write habilitado. No se puede ajustar el pivote.");
                    return;
                }

                Mesh meshToEdit;
                if (createNewMeshInstance)
                {
                    meshToEdit = Object.Instantiate(sharedMesh);
                    meshToEdit.name = string.IsNullOrEmpty(sharedMesh.name) ? go.name + "_Pivot" : sharedMesh.name + "_Pivot";
                    Undo.RegisterCreatedObjectUndo(meshToEdit, "Duplicar mesh pivot");
                    Undo.RecordObject(meshFilter, "Asignar mesh pivot");
                    meshFilter.sharedMesh = meshToEdit;

                    if (saveNewMeshAsAsset)
                    {
                        SavePivotMeshAsset(meshToEdit, sharedMesh, go);
                    }
                }
                else
                {
                    meshToEdit = sharedMesh;
                    Undo.RecordObject(meshToEdit, "Editar mesh pivot");
                }

                Vector3 localPivot = transform.InverseTransformPoint(pivotWorld);
                Vector3[] vertices = meshToEdit.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] -= localPivot;
                }
                meshToEdit.vertices = vertices;
                meshToEdit.RecalculateBounds();

                if (!createNewMeshInstance)
                {
                    EditorUtility.SetDirty(meshToEdit);
                }

                if (updateMeshColliders)
                {
                    MeshCollider collider = go.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        Undo.RecordObject(collider, "Actualizar MeshCollider");
                        collider.sharedMesh = null;
                        collider.sharedMesh = meshFilter.sharedMesh;
                    }
                }

                worldOffset = transform.TransformVector(localPivot);
            }
            else
            {
                // Sin un MeshFilter con mesh no hay geometría que compensar: mover el transform
                // directamente (como hacía antes) teletransportaría el objeto a la posición del
                // pivote en vez de mantenerlo quieto. Se omite y se avisa (p.ej. SkinnedMeshRenderer,
                // que esta herramienta todavía no soporta, u objetos sin geometría).
                SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMeshRenderer != null)
                {
                    Debug.LogWarning($"[Pivot] '{go.name}' usa un SkinnedMeshRenderer; esta herramienta aún no lo soporta y el objeto se omite.");
                }
                else
                {
                    Debug.LogWarning($"[Pivot] '{go.name}' no tiene un MeshFilter con mesh asignado; se omite para no desplazar el objeto sin geometría que compensarlo.");
                }
                return;
            }

            ApplyTransformOffset(transform, worldOffset, children, childPositions, childRotations);
            customPivotWorld = pivotWorld;

            Debug.Log($"[Pivot] '{go.name}' pivot actualizado en {pivotWorld}.");
        }

        private static void ApplyTransformOffset(Transform transform, Vector3 worldOffset, List<Transform> children, List<Vector3> childPositions, List<Quaternion> childRotations)
        {
            Undo.RecordObject(transform, "Mover pivote");
            transform.position += worldOffset;

            if (!preserveChildren)
            {
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                Transform child = children[i];
                if (child == null)
                {
                    continue;
                }

                Undo.RecordObject(child, "Restaurar hijo tras mover pivote");
                child.position = childPositions[i];
                child.rotation = childRotations[i];
            }
        }

        private static void SavePivotMeshAsset(Mesh mesh, Mesh sourceMesh, GameObject owner)
        {
            string folderPath = "Assets";
            if (pivotAssetFolder != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(pivotAssetFolder);
                if (AssetDatabase.IsValidFolder(selectedPath))
                {
                    folderPath = selectedPath;
                }
            }
            else
            {
                string sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
                if (!string.IsNullOrEmpty(sourcePath))
                {
                    string directory = Path.GetDirectoryName(sourcePath);
                    if (!string.IsNullOrEmpty(directory) && directory.StartsWith("Assets"))
                    {
                        folderPath = directory;
                    }
                }
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string meshName = string.IsNullOrEmpty(mesh.name) ? owner.name + "_Pivot" : mesh.name;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, meshName + ".asset"));
            assetPath = assetPath.Replace("\\", "/");

            AssetDatabase.CreateAsset(mesh, assetPath);
            assetSaveRequested = true;
        }

        private static void AlignGizmoToReference(bool forceCustom, bool requestRepaint = true)
        {
            if (!useReferenceObject || pivotReferenceObject == null)
            {
                return;
            }

            Vector3 referencePivot = GetReferencePivotWorld();
            customPivotWorld = referencePivot;
            if (forceCustom)
            {
                pivotAnchor = PivotAnchor.Custom;
            }
            pivotInitialized = true;
            if (requestRepaint)
            {
                SceneView.RepaintAll();
            }
        }

        private static Vector3 ResolveCustomPivotWorld()
        {
            if (useReferenceObject && pivotReferenceObject != null && autoFollowReference)
            {
                return GetReferencePivotWorld();
            }

            return customPivotWorld;
        }

        private static Vector3 GetReferencePivotWorld()
        {
            if (pivotReferenceObject == null)
            {
                return customPivotWorld;
            }

            Vector3 basePosition;
            if (referenceUseBounds)
            {
                Bounds? bounds = CalculateObjectBounds(pivotReferenceObject, true);
                basePosition = bounds?.center ?? pivotReferenceObject.transform.position;
            }
            else
            {
                basePosition = pivotReferenceObject.transform.position;
            }

            if (referenceLocalOffset != Vector3.zero)
            {
                basePosition += pivotReferenceObject.transform.TransformVector(referenceLocalOffset);
            }

            return basePosition;
        }

        private static Vector3 SnapVector(Vector3 value, float snap)
        {
            float Step(float component)
            {
                return Mathf.Round(component / snap) * snap;
            }

            return new Vector3(Step(value.x), Step(value.y), Step(value.z));
        }
    }
}
