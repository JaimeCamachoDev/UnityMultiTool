using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class HollowShellMeshTool
    {
        private static readonly List<GameObject> gameObjectsToModify = new List<GameObject>(); // Lista de GameObjects para modificar
        private static Transform clippingPlane; // El plano de recorte
        private enum ClipDirection { Below, Above } // Direcciones de recorte
        private static ClipDirection clipDirection = ClipDirection.Below; // Opción seleccionada del dropdown

        private static readonly Dictionary<GameObject, Mesh> originalMeshes = new Dictionary<GameObject, Mesh>(); // Diccionario para almacenar las mallas originales antes de la vista previa

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();

            root.Add(new Label("1. Drag objects with MeshRenderer") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new HelpBox("Drag the objects here to modify their meshes.", HelpBoxMessageType.Info));

            var listContainer = new VisualElement { style = { marginTop = 6 } };
            var statusContainer = new VisualElement { style = { marginTop = 10 } };

            Button previewButton = null;
            Button undoButton = null;
            Button saveButton = null;

            void RefreshStatus()
            {
                statusContainer.Clear();

                bool hasValidObjects = gameObjectsToModify.Any(o => o != null);
                bool hasPreview = originalMeshes.Count > 0;

                if (!hasValidObjects)
                {
                    statusContainer.Add(new HelpBox("Arrastra al menos un objeto en el paso 1 antes de previsualizar.", HelpBoxMessageType.Info));
                }
                else if (clippingPlane == null)
                {
                    statusContainer.Add(new HelpBox("Arrastra un plano de recorte en el paso 2 antes de previsualizar.", HelpBoxMessageType.Info));
                }
                else if (!hasPreview)
                {
                    statusContainer.Add(new HelpBox("Pulsa \"Preview clip\" antes de guardar los cambios.", HelpBoxMessageType.Info));
                }

                previewButton.SetEnabled(hasValidObjects && clippingPlane != null);
                undoButton.SetEnabled(hasPreview);
                saveButton.SetEnabled(hasPreview);
            }

            void RefreshList()
            {
                listContainer.Clear();
                for (int i = 0; i < gameObjectsToModify.Count; i++)
                {
                    int index = i;
                    var field = new ObjectField($"Object {i + 1}") { objectType = typeof(GameObject), allowSceneObjects = true, value = gameObjectsToModify[index] };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        gameObjectsToModify[index] = evt.newValue as GameObject;
                        RefreshStatus();
                    });
                    listContainer.Add(field);
                }
            }

            root.Add(listContainer);

            // Botones para agregar/eliminar objetos de la lista
            var addRemoveRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            addRemoveRow.Add(new Button(() =>
            {
                gameObjectsToModify.Add(null);
                RefreshList();
                RefreshStatus();
            })
            { text = "Add object" });

            addRemoveRow.Add(new Button(() =>
            {
                if (gameObjectsToModify.Count > 0)
                {
                    gameObjectsToModify.RemoveAt(gameObjectsToModify.Count - 1);
                    RefreshList();
                    RefreshStatus();
                }
            })
            { text = "Remove last object", style = { marginLeft = 6 } });
            root.Add(addRemoveRow);

            root.Add(new Label("2. Drag the Clipping Plane") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });
            var clippingPlaneField = new ObjectField("Clipping Plane") { objectType = typeof(Transform), allowSceneObjects = true, value = clippingPlane };
            clippingPlaneField.RegisterValueChangedCallback(evt =>
            {
                clippingPlane = evt.newValue as Transform;
                RefreshStatus();
            });
            root.Add(clippingPlaneField);

            root.Add(new Label("3. Select clip direction") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });
            var clipDirectionField = new EnumField("Clip Direction", clipDirection);
            clipDirectionField.RegisterValueChangedCallback(evt => clipDirection = (ClipDirection)evt.newValue);
            root.Add(clipDirectionField);

            root.Add(statusContainer);

            // Botón para previsualizar el recorte
            previewButton = new Button(() =>
            {
                PreviewMeshModification();
                RefreshStatus();
            })
            { text = "Preview clip", style = { marginTop = 10 } };
            root.Add(previewButton);

            // Botón para deshacer la vista previa y restaurar las mallas originales
            undoButton = new Button(() =>
            {
                RestoreOriginalMeshes();
                RefreshStatus();
            })
            { text = "Undo preview" };
            root.Add(undoButton);

            // Botón para guardar los cambios y reemplazar las mallas originales
            saveButton = new Button(() =>
            {
                SaveModifiedMeshes();
                RefreshStatus();
            })
            { text = "Save changes", style = { marginTop = 10 } };
            root.Add(saveButton);

            RefreshList();
            RefreshStatus();

            return root;
        }

        private static void PreviewMeshModification()
        {
            if (clippingPlane == null)
            {
                Debug.LogError("Clipping Plane not assigned.");
                return;
            }

            // Eliminamos del backup las entradas de objetos que ya no están en la lista
            var stillTracked = gameObjectsToModify.Where(o => o != null).ToHashSet();
            foreach (var staleKey in originalMeshes.Keys.Where(k => !stillTracked.Contains(k)).ToList())
            {
                originalMeshes.Remove(staleKey);
            }

            // Almacenamos la malla original de cada objeto la primera vez que aparece en la lista
            foreach (var obj in gameObjectsToModify)
            {
                if (obj == null || originalMeshes.ContainsKey(obj)) continue;

                var meshFilter = obj.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    originalMeshes.Add(obj, meshFilter.sharedMesh);
                }
                else if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    originalMeshes.Add(obj, skinnedMeshRenderer.sharedMesh);
                }
            }

            // Aplicamos el recorte basado en la dirección seleccionada
            foreach (var obj in gameObjectsToModify)
            {
                if (obj == null) continue;

                var meshFilter = obj.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    Mesh modifiedMesh = CreateClippedMesh(meshFilter.sharedMesh, clippingPlane, clipDirection, obj.transform);
                    Undo.RegisterCreatedObjectUndo(modifiedMesh, "Create Hollow Shell Mesh");
                    Undo.RecordObject(meshFilter, "Preview Hollow Shell");
                    meshFilter.sharedMesh = modifiedMesh;
                }
                else if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    Mesh modifiedMesh = CreateClippedMesh(skinnedMeshRenderer.sharedMesh, clippingPlane, clipDirection, obj.transform);
                    Undo.RegisterCreatedObjectUndo(modifiedMesh, "Create Hollow Shell Mesh");
                    Undo.RecordObject(skinnedMeshRenderer, "Preview Hollow Shell");
                    skinnedMeshRenderer.sharedMesh = modifiedMesh;
                }
            }
        }

        private static void RestoreOriginalMeshes()
        {
            // Restauramos las mallas originales desde el diccionario
            foreach (var entry in originalMeshes)
            {
                var obj = entry.Key;
                var meshFilter = obj.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null)
                {
                    Undo.RecordObject(meshFilter, "Restore Original Mesh");
                    meshFilter.sharedMesh = entry.Value;
                }
                else if (skinnedMeshRenderer != null)
                {
                    Undo.RecordObject(skinnedMeshRenderer, "Restore Original Mesh");
                    skinnedMeshRenderer.sharedMesh = entry.Value;
                }
            }
        }

        private static void SaveModifiedMeshes()
        {
            foreach (var obj in gameObjectsToModify)
            {
                if (obj == null) continue;

                var meshFilter = obj.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    SaveMesh(meshFilter.sharedMesh, obj);
                }
                else if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
                {
                    SaveMesh(skinnedMeshRenderer.sharedMesh, obj);
                }
            }
        }

        private static void SaveMesh(Mesh modifiedMesh, GameObject obj)
        {
            if (!originalMeshes.TryGetValue(obj, out Mesh originalMesh))
            {
                Debug.LogError($"No original mesh backup found for '{obj.name}'. Run \"Preview clip\" before saving.");
                return;
            }

            // Obtener la ruta de la malla original
            string originalMeshPath = AssetDatabase.GetAssetPath(originalMesh);
            string directory = !string.IsNullOrEmpty(originalMeshPath) ? Path.GetDirectoryName(originalMeshPath) : null;
            if (string.IsNullOrEmpty(directory))
            {
                directory = "Assets";
                Debug.LogWarning($"The original mesh for '{obj.name}' is not a project asset (e.g. a built-in or imported mesh). Saving the hollow shell mesh to '{directory}' instead.");
            }

            string newMeshName = modifiedMesh.name + "_HollowShell.asset";
            newMeshName = newMeshName.Replace("(Clone)", "");
            string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newMeshName));

            // Guardar la nueva malla
            AssetDatabase.CreateAsset(modifiedMesh, newPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Hollow shell saved: {newPath}");
        }

        private static Mesh CreateClippedMesh(Mesh originalMesh, Transform plane, ClipDirection direction, Transform meshTransform)
        {
            // Creamos una copia de la malla original
            Mesh newMesh = Object.Instantiate(originalMesh);

            // Obtenemos los vértices y triángulos originales
            Vector3[] vertices = newMesh.vertices;
            Vector2[] uvs = newMesh.uv;
            Vector3[] normals = newMesh.normals;
            Vector4[] tangents = newMesh.tangents;
            List<int> newTriangles = new List<int>(newMesh.triangles);

            // Obtener la posición y normal del plano
            Vector3 planePos = plane.position;
            Vector3 planeNormal = plane.up;

            // Transformación inversa para llevar los vértices al espacio local del plano
            Matrix4x4 localToWorld = meshTransform.localToWorldMatrix;

            // Lista para almacenar los vértices que se mantienen
            HashSet<int> verticesToKeep = new HashSet<int>();

            // Filtramos los vértices y eliminamos los que están en la dirección seleccionada
            for (int i = newTriangles.Count - 1; i >= 0; i -= 3)
            {
                // Obtenemos los índices de los tres vértices del triángulo actual
                int index0 = newTriangles[i];
                int index1 = newTriangles[i - 1];
                int index2 = newTriangles[i - 2];

                // Convertir vértices al espacio del mundo
                Vector3 worldPos0 = localToWorld.MultiplyPoint3x4(vertices[index0]);
                Vector3 worldPos1 = localToWorld.MultiplyPoint3x4(vertices[index1]);
                Vector3 worldPos2 = localToWorld.MultiplyPoint3x4(vertices[index2]);

                // Chequeamos si los vértices deben eliminarse solo si los tres están por debajo del plano
                bool remove = ShouldRemoveVertex(worldPos0, planePos, planeNormal, direction) &&
                              ShouldRemoveVertex(worldPos1, planePos, planeNormal, direction) &&
                              ShouldRemoveVertex(worldPos2, planePos, planeNormal, direction);

                // Si todos los vértices están por debajo del plano, eliminamos el triángulo
                if (remove)
                {
                    newTriangles.RemoveAt(i);
                    newTriangles.RemoveAt(i - 1);
                    newTriangles.RemoveAt(i - 2);
                }
                else
                {
                    // Si no se elimina, agregamos estos vértices a la lista de vértices que se mantienen
                    verticesToKeep.Add(index0);
                    verticesToKeep.Add(index1);
                    verticesToKeep.Add(index2);
                }
            }

            // Crear nuevas listas de vértices y datos asociados que solo contengan los vértices que se utilizan
            List<Vector3> optimizedVertices = new List<Vector3>();
            List<Vector2> optimizedUVs = new List<Vector2>();
            List<Vector3> optimizedNormals = new List<Vector3>();
            List<Vector4> optimizedTangents = new List<Vector4>();
            Dictionary<int, int> oldToNewIndexMap = new Dictionary<int, int>();

            // Crear nuevos índices de triángulo ajustados a los nuevos vértices
            List<int> optimizedTriangles = new List<int>();

            int newIndex = 0;
            foreach (int oldIndex in verticesToKeep)
            {
                // Copiamos los datos de los vértices, uvs, normales y tangentes de los vértices que se mantienen
                optimizedVertices.Add(vertices[oldIndex]);
                if (uvs.Length > 0) optimizedUVs.Add(uvs[oldIndex]);
                if (normals.Length > 0) optimizedNormals.Add(normals[oldIndex]);
                if (tangents.Length > 0) optimizedTangents.Add(tangents[oldIndex]);

                // Mapear los índices antiguos a los nuevos
                oldToNewIndexMap[oldIndex] = newIndex;
                newIndex++;
            }

            // Rehacer los triángulos utilizando el nuevo mapeo de índices
            for (int i = 0; i < newTriangles.Count; i++)
            {
                optimizedTriangles.Add(oldToNewIndexMap[newTriangles[i]]);
            }

            // Asignar los nuevos vértices, triángulos, UVs, etc. a la nueva malla
            newMesh.Clear();
            newMesh.indexFormat = optimizedVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            newMesh.vertices = optimizedVertices.ToArray();
            newMesh.triangles = optimizedTriangles.ToArray();
            if (optimizedUVs.Count > 0) newMesh.uv = optimizedUVs.ToArray();
            if (optimizedNormals.Count > 0) newMesh.normals = optimizedNormals.ToArray();
            if (optimizedTangents.Count > 0) newMesh.tangents = optimizedTangents.ToArray();

            // Recalcular las propiedades finales de la malla
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();

            return newMesh;
        }

        private static bool ShouldRemoveVertex(Vector3 vertex, Vector3 planePos, Vector3 planeNormal, ClipDirection direction)
        {
            // Proyectamos la posición del vértice respecto al plano y determinamos si eliminarlo según la dirección de recorte
            Vector3 relativePos = vertex - planePos;

            switch (direction)
            {
                case ClipDirection.Below:
                    return Vector3.Dot(relativePos, planeNormal) < 0;
                case ClipDirection.Above:
                    return Vector3.Dot(relativePos, planeNormal) > 0;
                default:
                    return false;
            }
        }
    }
}
