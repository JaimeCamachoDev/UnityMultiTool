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
            root.Add(new HelpBox("Selecciona una Mesh en el Project, o un objeto con MeshFilter en la escena, para dividir sus submeshes por material.", HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                meshMultiMat = ResolveMeshFromSelection();

                if (meshMultiMat == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona una Mesh o un objeto con MeshFilter para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                var meshPanel = new MTUIPanel("Malla seleccionada");
                meshPanel.Add(new MTUIInfoLabel(meshMultiMat.name));

                if (meshMultiMat.blendShapeCount > 0)
                {
                    meshPanel.Add(new HelpBox("Esta malla tiene blend shapes. Los submeshes generados no los incluirán.", HelpBoxMessageType.Warning));
                }
                contentContainer.Add(meshPanel);

                bool hasMultipleSubmeshes = meshMultiMat.subMeshCount > 1;
                if (!hasMultipleSubmeshes)
                {
                    meshPanel.Add(new HelpBox("La malla seleccionada no tiene varios submeshes; no hay nada que separar.", HelpBoxMessageType.Warning));
                    return;
                }

                var destinationPanel = new MTUIPanel("Carpeta de destino") { style = { marginTop = 10 } };
                var folderField = new ObjectField("Carpeta de Destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = destinationFolder };
                var statusContainer = new VisualElement { style = { marginTop = 6 } };
                var splitButton = new MTUIActionButton("Dividir y Guardar Submeshes", SplitAndSaveSubmeshes);
                splitButton.style.marginTop = 10;

                void RefreshFolderStatus()
                {
                    statusContainer.Clear();
                    if (destinationFolder == null)
                    {
                        statusContainer.Add(new HelpBox("Arrastra una carpeta del proyecto para continuar.", HelpBoxMessageType.Info));
                    }
                    splitButton.SetAvailable(destinationFolder != null);
                }

                folderField.RegisterValueChangedCallback(evt =>
                {
                    destinationFolder = evt.newValue as DefaultAsset;
                    RefreshFolderStatus();
                });

                destinationPanel.Add(folderField);
                destinationPanel.Add(statusContainer);
                contentContainer.Add(destinationPanel);
                contentContainer.Add(splitButton);

                RefreshFolderStatus();
            }

            // Sigue la selección de la escena/proyecto mientras la herramienta esté abierta
            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static Mesh ResolveMeshFromSelection()
        {
            Mesh candidate = Selection.activeObject as Mesh;
            if (candidate == null && Selection.activeGameObject != null)
            {
                MeshFilter filter = Selection.activeGameObject.GetComponent<MeshFilter>();
                candidate = filter != null ? filter.sharedMesh : null;
            }

            return candidate;
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
