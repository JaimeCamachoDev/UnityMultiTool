using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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

        public static void DrawTool()
        {
            GUILayout.Label("Pivot mover & aligner", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Mueve el pivote de props estáticos usando el gizmo cyan en la escena o presets alineados al bound.", MessageType.Info);

            if (Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox("Selecciona uno o más objetos para modificar su pivote.", MessageType.Warning);
                return;
            }

            PivotAnchor newAnchor = (PivotAnchor)EditorGUILayout.EnumPopup("Preset de pivote", pivotAnchor);
            if (newAnchor != pivotAnchor)
            {
                pivotAnchor = newAnchor;
                if (pivotAnchor != PivotAnchor.Custom)
                {
                    UpdateHandleFromSelection(false);
                }
            }

            includeChildrenBounds = EditorGUILayout.ToggleLeft("Calcular bounds incluyendo hijos", includeChildrenBounds);
            applyPerObject = EditorGUILayout.ToggleLeft("Calcular preset por objeto", applyPerObject);

            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Ajustes avanzados", true);
            if (showAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                preserveChildren = EditorGUILayout.ToggleLeft("Mantener posición global de los hijos", preserveChildren);
                updateMeshColliders = EditorGUILayout.ToggleLeft("Actualizar MeshCollider si existe", updateMeshColliders);
                createNewMeshInstance = EditorGUILayout.ToggleLeft("Duplicar mesh antes de editar", createNewMeshInstance);
                using (new EditorGUI.DisabledScope(!createNewMeshInstance))
                {
                    saveNewMeshAsAsset = EditorGUILayout.ToggleLeft("Guardar mesh duplicado como asset", saveNewMeshAsAsset);
                    EditorGUI.indentLevel++;
                    pivotAssetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Carpeta destino", pivotAssetFolder, typeof(DefaultAsset), false);
                    EditorGUI.indentLevel--;
                }
                alignHandleToActiveRotation = EditorGUILayout.ToggleLeft("Alinear gizmo a la rotación del activo", alignHandleToActiveRotation);
                enablePivotGridSnap = EditorGUILayout.ToggleLeft("Activar snap en la escena", enablePivotGridSnap);
                using (new EditorGUI.DisabledScope(!enablePivotGridSnap))
                {
                    pivotGridSize = EditorGUILayout.FloatField("Tamaño del snap (m)", Mathf.Max(0.001f, pivotGridSize));
                }
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(6f);

            if (pivotAnchor == PivotAnchor.Custom)
            {
                customPivotWorld = EditorGUILayout.Vector3Field("Pivote personalizado (mundo)", customPivotWorld);
                if (GUILayout.Button("Centrar gizmo en objeto activo"))
                {
                    UpdateHandleFromSelection(true);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("El pivote se tomará del preset seleccionado. Puedes refinarlo moviendo el gizmo.", MessageType.None);
            }

            GUILayout.Space(6f);

            showReferenceOptions = EditorGUILayout.Foldout(showReferenceOptions, "Referencia externa", true);
            if (showReferenceOptions)
            {
                EditorGUI.indentLevel++;
                bool useReferenceBefore = useReferenceObject;
                useReferenceObject = EditorGUILayout.ToggleLeft("Usar objeto como referencia", useReferenceObject);
                if (useReferenceObject && !useReferenceBefore)
                {
                    AlignGizmoToReference(true);
                }

                using (new EditorGUI.DisabledScope(!useReferenceObject))
                {
                    GameObject previousReference = pivotReferenceObject;
                    pivotReferenceObject = (GameObject)EditorGUILayout.ObjectField("Objeto de referencia", pivotReferenceObject, typeof(GameObject), true);
                    if (pivotReferenceObject != null && pivotReferenceObject != previousReference)
                    {
                        AlignGizmoToReference(true);
                    }
                    bool previousReferenceBounds = referenceUseBounds;
                    referenceUseBounds = EditorGUILayout.ToggleLeft("Tomar centro del bound de la referencia", referenceUseBounds);
                    if (autoFollowReference && referenceUseBounds != previousReferenceBounds)
                    {
                        AlignGizmoToReference(true);
                    }

                    Vector3 previousOffset = referenceLocalOffset;
                    referenceLocalOffset = EditorGUILayout.Vector3Field("Offset local adicional", referenceLocalOffset);
                    if (autoFollowReference && previousOffset != referenceLocalOffset)
                    {
                        AlignGizmoToReference(true);
                    }
                    bool previousAutoFollow = autoFollowReference;
                    autoFollowReference = EditorGUILayout.ToggleLeft("Mantener gizmo sincronizado con la referencia", autoFollowReference);
                    if (autoFollowReference && !previousAutoFollow)
                    {
                        AlignGizmoToReference(true);
                    }
                    if (GUILayout.Button("Alinear gizmo a la referencia"))
                    {
                        AlignGizmoToReference(true);
                    }
                }
                EditorGUI.indentLevel--;
            }

            if (useReferenceObject && pivotReferenceObject != null && !autoFollowReference)
            {
                EditorGUILayout.HelpBox("El gizmo usa la posición de la referencia actual pero el seguimiento automático está desactivado.", MessageType.Info);
            }

            GUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
            {
                if (GUILayout.Button("Aplicar pivote a la selección"))
                {
                    ApplyPivotToSelection();
                }
            }
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
                worldOffset = pivotWorld - transform.position;
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
