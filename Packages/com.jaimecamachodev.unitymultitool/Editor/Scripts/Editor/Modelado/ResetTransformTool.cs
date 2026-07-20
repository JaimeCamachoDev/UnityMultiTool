using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class ResetTransformTool
    {
        private static bool duplicateMeshBeforeApplying = true;
        private static bool saveDuplicatedMeshAsAsset = true;
        private static DefaultAsset meshAssetFolder;
        private static bool preserveChildrenWorldTransform = true;

        public static void DrawTool()
        {
            GUILayout.Label("Reset XForm", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Convierte la transformación actual de la selección en parte de la geometría para dejar el Transform en valores por defecto sin mover los objetos en escena.",
                MessageType.Info);

            if (Selection.gameObjects.Length == 0)
            {
                EditorGUILayout.HelpBox("Selecciona uno o más objetos para aplicar el Reset XForm.", MessageType.Warning);
                return;
            }

            duplicateMeshBeforeApplying = EditorGUILayout.ToggleLeft(
                "Duplicar el Mesh antes de aplicar",
                duplicateMeshBeforeApplying);

            using (new EditorGUI.DisabledScope(!duplicateMeshBeforeApplying))
            {
                saveDuplicatedMeshAsAsset = EditorGUILayout.ToggleLeft(
                    "Guardar mesh duplicado como asset",
                    saveDuplicatedMeshAsAsset);
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!saveDuplicatedMeshAsAsset))
                {
                    meshAssetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                        "Carpeta destino",
                        meshAssetFolder,
                        typeof(DefaultAsset),
                        false);
                }
                EditorGUI.indentLevel--;
            }

            if (!duplicateMeshBeforeApplying)
            {
                EditorGUILayout.HelpBox(
                    "El mesh original se modificará directamente y afectará a todas las instancias que lo compartan.",
                    MessageType.Warning);
            }

            preserveChildrenWorldTransform = EditorGUILayout.ToggleLeft(
                "Mantener la transformación global de los hijos",
                preserveChildrenWorldTransform);

            GUILayout.Space(8f);

            if (GUILayout.Button("Aplicar Reset XForm a la selección"))
            {
                ApplyResetToSelection();
            }
        }

        private static void ApplyResetToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection.Length == 0)
            {
                return;
            }

            List<string> processed = new List<string>();

            foreach (GameObject gameObject in selection)
            {
                if (ApplyResetToObject(gameObject))
                {
                    processed.Add(gameObject.name);
                }
            }

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

                if (owner != null)
                {
                    Undo.RecordObject(owner, "Reset XForm");
                }

                if (saveDuplicatedMeshAsAsset && meshAssetFolder != null)
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

            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
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

            rotation = Quaternion.LookRotation(column2, column1);
        }
    }
}
