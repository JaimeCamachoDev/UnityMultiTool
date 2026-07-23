using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using JaimeCamachoDev.Multitool.UI;
using System.IO;

namespace JaimeCamachoDev.Multitool.Modeling
{
    public static class MultimaterialMeshSplitterTool
    {
        private static Mesh meshMultiMat; // Mesh a dividir en submeshes
        private static DefaultAsset destinationFolder; // Carpeta donde se guardarán los submeshes

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Separar Submeshes por Material") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox("Selecciona en la escena un objeto con MeshFilter (o una Mesh en la ventana Project) para rellenar el campo automáticamente, o arrástrala a mano.", HelpBoxMessageType.Info));

            var statusContainer = new VisualElement { style = { marginTop = 6 } };
            var splitButton = new MTUIActionButton("Dividir y Guardar Submeshes", SplitAndSaveSubmeshes);
            splitButton.style.marginTop = 10;

            root.Add(new Label("1. Malla a dividir") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            var meshField = new ObjectField("Mesh") { objectType = typeof(Mesh), allowSceneObjects = true, value = meshMultiMat };
            meshField.RegisterValueChangedCallback(evt =>
            {
                meshMultiMat = evt.newValue as Mesh;
                RefreshStatus(statusContainer, splitButton);
            });
            root.Add(meshField);

            root.Add(new Label("2. Carpeta de destino") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10 } });
            var folderField = new ObjectField("Carpeta de Destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = destinationFolder };
            folderField.RegisterValueChangedCallback(evt =>
            {
                destinationFolder = evt.newValue as DefaultAsset;
                RefreshStatus(statusContainer, splitButton);
            });
            root.Add(folderField);

            root.Add(statusContainer);
            root.Add(splitButton);

            // Si seleccionas un objeto con MeshFilter en la escena, o una Mesh en el Project,
            // rellena el campo automáticamente (igual que Advanced Mesh Combiner reacciona a la
            // selección), sin bloquear la posibilidad de arrastrar una malla manualmente.
            void SyncFromSelection()
            {
                Mesh candidate = Selection.activeObject as Mesh;
                if (candidate == null && Selection.activeGameObject != null)
                {
                    MeshFilter filter = Selection.activeGameObject.GetComponent<MeshFilter>();
                    candidate = filter != null ? filter.sharedMesh : null;
                }

                if (candidate != null && candidate != meshMultiMat)
                {
                    meshMultiMat = candidate;
                    meshField.SetValueWithoutNotify(meshMultiMat);
                    RefreshStatus(statusContainer, splitButton);
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += SyncFromSelection);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= SyncFromSelection);

            RefreshStatus(statusContainer, splitButton);

            return root;
        }

        private static void RefreshStatus(VisualElement container, MTUIActionButton splitButton)
        {
            container.Clear();

            if (meshMultiMat != null && meshMultiMat.blendShapeCount > 0)
            {
                container.Add(new HelpBox("Esta malla tiene blend shapes. Los submeshes generados no los incluirán.", HelpBoxMessageType.Warning));
            }

            bool hasMultipleSubmeshes = meshMultiMat != null && meshMultiMat.subMeshCount > 1;

            if (meshMultiMat == null)
            {
                container.Add(new HelpBox("Arrastra una Mesh en el paso 1 para continuar.", HelpBoxMessageType.Info));
            }
            else if (!hasMultipleSubmeshes)
            {
                container.Add(new HelpBox("La malla seleccionada no tiene varios submeshes; no hay nada que separar.", HelpBoxMessageType.Warning));
            }
            else if (destinationFolder == null)
            {
                container.Add(new HelpBox("Arrastra una carpeta del proyecto en el paso 2 para continuar.", HelpBoxMessageType.Info));
            }

            splitButton.SetAvailable(hasMultipleSubmeshes && destinationFolder != null);
        }

        private static void SplitAndSaveSubmeshes()
        {
            string path = AssetDatabase.GetAssetPath(destinationFolder);
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogError("Carpeta de destino inválida.");
                return;
            }

            Mesh originalMesh = meshMultiMat;

            if (originalMesh.subMeshCount <= 1)
            {
                Debug.LogWarning("La malla no tiene submeshes para dividir.");
                return;
            }

            UnityEngine.Rendering.IndexFormat indexFormat = originalMesh.vertexCount > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            for (int i = 0; i < originalMesh.subMeshCount; i++)
            {
                Mesh submesh = new Mesh
                {
                    name = originalMesh.name + "_Submesh_" + (i + 1),
                    indexFormat = indexFormat,
                    vertices = originalMesh.vertices,
                    normals = originalMesh.normals,
                    tangents = originalMesh.tangents,
                    boneWeights = originalMesh.boneWeights,
                    bindposes = originalMesh.bindposes
                };

                submesh.SetIndices(originalMesh.GetIndices(i), MeshTopology.Triangles, 0);

                // Copiar y centrar UVs
                Vector2[] originalUVs = originalMesh.uv;
                if (originalUVs.Length > 0)
                {
                    Vector2[] newUVs = new Vector2[originalUVs.Length];
                    System.Array.Copy(originalUVs, newUVs, originalUVs.Length);

                    // Centrar las UVs del submesh
                    CenterUVsHorizontally(submesh, newUVs);
                }

                string submeshName = originalMesh.name + "_Submesh_" + (i + 1);
                string savePath = Path.Combine(path, submeshName + ".asset");
                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

                AssetDatabase.CreateAsset(submesh, savePath);
                AssetDatabase.SaveAssets();

                Debug.Log($"Submesh {i + 1} guardado en {savePath}");
            }

            AssetDatabase.Refresh();
            Debug.Log("Todos los submeshes han sido divididos y guardados correctamente.");
        }

        private static void CenterUVsHorizontally(Mesh mesh, Vector2[] uvs)
        {
            // Obtener los índices del submesh
            int[] indices = mesh.GetIndices(0);

            // Calcular los valores mínimos y máximos de UV en los vértices de este submesh solo en el eje X
            float minU = float.MaxValue;
            float maxU = float.MinValue;

            foreach (int index in indices)
            {
                float u = uvs[index].x;
                minU = Mathf.Min(minU, u);
                maxU = Mathf.Max(maxU, u);
            }

            // Calcular el centro de las UVs en el eje X
            float uCenter = (minU + maxU) / 2f;

            // Desplazar UVs para que el centro esté en X = 0.5, sin modificar Y
            float offsetX = 0.5f - uCenter;

            for (int i = 0; i < uvs.Length; i++)
            {
                uvs[i].x += offsetX; // Solo movemos en el eje X
            }

            // Asignar las UVs desplazadas a la malla
            mesh.uv = uvs;
        }
    }
}
