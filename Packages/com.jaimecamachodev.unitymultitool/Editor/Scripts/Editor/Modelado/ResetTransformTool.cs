using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class ResetTransformTool
    {
        private static bool duplicateMeshBeforeApplying = true;
        private static bool saveDuplicatedMeshAsAsset = true;
        private static DefaultAsset meshAssetFolder;
        private static bool preserveChildrenWorldTransform = true;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Reset XForm") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Convierte la transformación actual de la selección en parte de la geometría para dejar el Transform en valores por defecto sin mover los objetos en escena.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                if (Selection.gameObjects.Length == 0)
                {
                    contentContainer.Add(new HelpBox("Selecciona uno o más objetos para aplicar el Reset XForm.", HelpBoxMessageType.Warning));
                    return;
                }

                var duplicateToggle = new Toggle("Duplicar el Mesh antes de aplicar") { value = duplicateMeshBeforeApplying };
                contentContainer.Add(duplicateToggle);

                var saveToggle = new Toggle("Guardar mesh duplicado como asset") { value = saveDuplicatedMeshAsAsset, style = { marginLeft = 15 } };
                var folderField = new ObjectField("Carpeta destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = meshAssetFolder, style = { marginLeft = 30 } };
                saveToggle.SetEnabled(duplicateMeshBeforeApplying);
                folderField.SetEnabled(duplicateMeshBeforeApplying && saveDuplicatedMeshAsAsset);

                var sharedWarning = new HelpBox(
                    "El mesh original se modificará directamente y afectará a todas las instancias que lo compartan.",
                    HelpBoxMessageType.Warning);
                sharedWarning.style.display = duplicateMeshBeforeApplying ? DisplayStyle.None : DisplayStyle.Flex;

                duplicateToggle.RegisterValueChangedCallback(evt =>
                {
                    duplicateMeshBeforeApplying = evt.newValue;
                    saveToggle.SetEnabled(duplicateMeshBeforeApplying);
                    folderField.SetEnabled(duplicateMeshBeforeApplying && saveDuplicatedMeshAsAsset);
                    sharedWarning.style.display = duplicateMeshBeforeApplying ? DisplayStyle.None : DisplayStyle.Flex;
                });

                saveToggle.RegisterValueChangedCallback(evt =>
                {
                    saveDuplicatedMeshAsAsset = evt.newValue;
                    folderField.SetEnabled(duplicateMeshBeforeApplying && saveDuplicatedMeshAsAsset);
                });

                folderField.RegisterValueChangedCallback(evt => meshAssetFolder = evt.newValue as DefaultAsset);

                contentContainer.Add(saveToggle);
                contentContainer.Add(folderField);
                contentContainer.Add(sharedWarning);

                var preserveToggle = new Toggle("Mantener la transformación global de los hijos") { value = preserveChildrenWorldTransform, style = { marginTop = 6 } };
                preserveToggle.RegisterValueChangedCallback(evt => preserveChildrenWorldTransform = evt.newValue);
                contentContainer.Add(preserveToggle);

                contentContainer.Add(new Button(ApplyResetToSelection) { text = "Aplicar Reset XForm a la selección", style = { marginTop = 8 } });
            }

            // Sigue la selección de la escena mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void ApplyResetToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                return;
            }

            List<string> processed = new List<string>();

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Reset XForm");

            foreach (GameObject gameObject in selection)
            {
                if (ApplyResetToObject(gameObject))
                {
                    processed.Add(gameObject.name);
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (processed.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Reset XForm aplicado a: {string.Join(", ", processed)}");
            }
            else
            {
                EditorUtility.DisplayDialog("Reset XForm", "No se encontró ninguna geometría compatible en la selección.", "Entendido");
            }
        }

        private static bool ApplyResetToObject(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            Transform targetTransform = target.transform;
            Matrix4x4 localMatrix = Matrix4x4.TRS(targetTransform.localPosition, targetTransform.localRotation, targetTransform.localScale);

            List<TransformState> childrenStates = null;
            if (preserveChildrenWorldTransform)
            {
                childrenStates = new List<TransformState>();
                foreach (Transform child in targetTransform)
                {
                    childrenStates.Add(new TransformState(child));
                }
            }

            bool meshProcessed = ProcessMeshComponents(target, localMatrix);

            if (!meshProcessed)
            {
                SkinnedMeshRenderer skinnedMesh = target.GetComponent<SkinnedMeshRenderer>();
                if (skinnedMesh != null && skinnedMesh.sharedMesh != null)
                {
                    Debug.LogWarning($"Reset XForm no es compatible con SkinnedMeshRenderer en '{target.name}'. El objeto se omitirá.", skinnedMesh);
                }
                return false;
            }

            Undo.RecordObject(targetTransform, "Reset XForm");
            targetTransform.localPosition = Vector3.zero;
            targetTransform.localRotation = Quaternion.identity;
            targetTransform.localScale = Vector3.one;

            if (childrenStates != null)
            {
                foreach (TransformState state in childrenStates)
                {
                    state.Restore(targetTransform);
                }
            }

            return true;
        }

        private static bool ProcessMeshComponents(GameObject target, Matrix4x4 localMatrix)
        {
            MeshFilter meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return false;
            }

            Mesh workingMesh = PrepareWritableMesh(meshFilter.sharedMesh, meshFilter);
            if (workingMesh == null)
            {
                return false;
            }

            ApplyMatrixToMesh(workingMesh, localMatrix);
            meshFilter.sharedMesh = workingMesh;

            MeshCollider meshCollider = target.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                Undo.RecordObject(meshCollider, "Reset XForm");
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = workingMesh;
            }

            return true;
        }

        private static Mesh PrepareWritableMesh(Mesh sourceMesh, Component owner)
        {
            if (sourceMesh == null)
            {
                return null;
            }

            Mesh meshToEdit;
            if (duplicateMeshBeforeApplying)
            {
                meshToEdit = Object.Instantiate(sourceMesh);
                meshToEdit.name = sourceMesh.name + "_ResetXForm";
                Undo.RegisterCreatedObjectUndo(meshToEdit, "Reset XForm");

                if (owner != null)
                {
                    Undo.RecordObject(owner, "Reset XForm");
                }

                if (saveDuplicatedMeshAsAsset)
                {
                    if (meshAssetFolder != null)
                    {
                        string assetFolderPath = AssetDatabase.GetAssetPath(meshAssetFolder);
                        if (!string.IsNullOrEmpty(assetFolderPath) && AssetDatabase.IsValidFolder(assetFolderPath))
                        {
                            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetFolderPath, meshToEdit.name + ".asset"));
                            AssetDatabase.CreateAsset(meshToEdit, assetPath);
                        }
                        else
                        {
                            Debug.LogWarning("La carpeta seleccionada para guardar el mesh duplicado no es válida. Se usará el mesh en memoria.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No se asignó una carpeta destino: el mesh duplicado no se guardará como asset y permanecerá solo en memoria.");
                    }
                }
            }
            else
            {
                meshToEdit = sourceMesh;
                Undo.RecordObject(meshToEdit, "Reset XForm");
            }

            return meshToEdit;
        }

        private static void ApplyMatrixToMesh(Mesh mesh, Matrix4x4 localMatrix)
        {
            Undo.RecordObject(mesh, "Reset XForm");
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = localMatrix.MultiplyPoint3x4(vertices[i]);
            }
            mesh.vertices = vertices;

            Vector3[] normals = mesh.normals;
            if (normals != null && normals.Length > 0)
            {
                Matrix4x4 normalMatrix = localMatrix.inverse.transpose;
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                }
                mesh.normals = normals;
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length > 0)
            {
                Matrix4x4 normalMatrix = localMatrix.inverse.transpose;
                for (int i = 0; i < tangents.Length; i++)
                {
                    Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangent = normalMatrix.MultiplyVector(tangent).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
                }
                mesh.tangents = tangents;
            }

            // Una escala espejada (determinante negativo) invierte la orientación de las caras;
            // sin corregir el winding order las caras quedarían con culling invertido.
            if (Determinant3x3(localMatrix) < 0f)
            {
                ReverseTriangleWinding(mesh);
            }

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
        }

        private static float Determinant3x3(Matrix4x4 matrix)
        {
            Vector3 column0 = matrix.GetColumn(0);
            Vector3 column1 = matrix.GetColumn(1);
            Vector3 column2 = matrix.GetColumn(2);
            return Vector3.Dot(column0, Vector3.Cross(column1, column2));
        }

        private static void ReverseTriangleWinding(Mesh mesh)
        {
            for (int subMeshIndex = 0; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
            {
                int[] triangles = mesh.GetTriangles(subMeshIndex);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }
                mesh.SetTriangles(triangles, subMeshIndex);
            }
        }

        private readonly struct TransformState
        {
            private readonly Transform transform;
            private readonly Matrix4x4 worldMatrix;

            public TransformState(Transform transform)
            {
                this.transform = transform;
                worldMatrix = transform.localToWorldMatrix;
            }

            public void Restore(Transform newParent)
            {
                if (transform == null)
                {
                    return;
                }

                Matrix4x4 localMatrix = newParent.worldToLocalMatrix * worldMatrix;
                DecomposeMatrix(localMatrix, out Vector3 position, out Quaternion rotation, out Vector3 scale);

                Undo.RecordObject(transform, "Reset XForm");
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }

        private static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);

            Vector3 column0 = matrix.GetColumn(0);
            Vector3 column1 = matrix.GetColumn(1);
            Vector3 column2 = matrix.GetColumn(2);

            scale = new Vector3(column0.magnitude, column1.magnitude, column2.magnitude);
            if (scale.x != 0f) column0 /= scale.x;
            if (scale.y != 0f) column1 /= scale.y;
            if (scale.z != 0f) column2 /= scale.z;

            // Quaternion.LookRotation solo puede representar rotaciones propias (determinante +1).
            // Si la matriz original es una reflexión (determinante negativo), volcamos esa reflexión
            // sobre la escala en X en vez de perderla al construir la rotación.
            if (Vector3.Dot(column0, Vector3.Cross(column1, column2)) < 0f)
            {
                scale.x = -scale.x;
            }

            rotation = Quaternion.LookRotation(column2, column1);
        }
    }
}
