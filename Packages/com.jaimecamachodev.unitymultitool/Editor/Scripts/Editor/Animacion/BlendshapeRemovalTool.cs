using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    public static class BlendshapeRemovalTool
    {
        private static Mesh meshToRemoveBlendshapes;
        private static DefaultAsset destinationFolderForCleanMesh;
        private static List<bool> blendshapeSelection;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Remove Blendshapes") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Crea una copia de la malla sin blendshapes (todos o solo los seleccionados) y la guarda como un nuevo asset.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Origen");

                var meshField = new ObjectField("Malla para limpiar") { objectType = typeof(Mesh), allowSceneObjects = true, value = meshToRemoveBlendshapes };
                meshField.RegisterValueChangedCallback(evt =>
                {
                    meshToRemoveBlendshapes = evt.newValue as Mesh;
                    blendshapeSelection = null;
                    RefreshContent();
                });
                sourcePanel.Add(meshField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = destinationFolderForCleanMesh };
                folderField.RegisterValueChangedCallback(evt => { destinationFolderForCleanMesh = evt.newValue as DefaultAsset; RefreshContent(); });
                sourcePanel.Add(folderField);

                contentContainer.Add(sourcePanel);

                bool hasRequirements = meshToRemoveBlendshapes != null && destinationFolderForCleanMesh != null;
                if (!hasRequirements)
                {
                    contentContainer.Add(new HelpBox("Selecciona una malla y una carpeta de destino.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                    return;
                }

                var removeAllButton = new MTUIActionButton("Eliminar Todos los Blendshapes", () =>
                {
                    RemoveAllBlendshapes();
                    RefreshContent();
                });
                removeAllButton.style.marginTop = 10;
                contentContainer.Add(removeAllButton);

                if (meshToRemoveBlendshapes.blendShapeCount > 0)
                {
                    var selectionPanel = new MTUIPanel("Eliminar Blendshapes Específicos") { style = { marginTop = 10 } };

                    if (blendshapeSelection == null || blendshapeSelection.Count != meshToRemoveBlendshapes.blendShapeCount)
                    {
                        blendshapeSelection = new List<bool>(new bool[meshToRemoveBlendshapes.blendShapeCount]);
                    }

                    var scroll = new ScrollView { style = { maxHeight = 240 } };
                    for (int i = 0; i < meshToRemoveBlendshapes.blendShapeCount; i++)
                    {
                        int index = i;
                        var toggle = new Toggle(meshToRemoveBlendshapes.GetBlendShapeName(i)) { value = blendshapeSelection[index] };
                        toggle.RegisterValueChangedCallback(evt => blendshapeSelection[index] = evt.newValue);
                        scroll.Add(toggle);
                    }
                    selectionPanel.Add(scroll);

                    bool anySelected = blendshapeSelection.Contains(true);
                    var removeSelectedButton = new MTUIActionButton("Eliminar Blendshapes Seleccionados", () =>
                    {
                        RemoveSelectedBlendshapes();
                        RefreshContent();
                    });
                    removeSelectedButton.style.marginTop = 6;
                    removeSelectedButton.SetAvailable(anySelected);
                    selectionPanel.Add(removeSelectedButton);

                    contentContainer.Add(selectionPanel);
                }
            }

            RefreshContent();
            return root;
        }

        private static void RemoveAllBlendshapes()
        {
            string path = AssetDatabase.GetAssetPath(destinationFolderForCleanMesh);
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogError("Carpeta de destino inválida.");
                return;
            }

            Mesh cleanMesh = Object.Instantiate(meshToRemoveBlendshapes);
            cleanMesh.name = meshToRemoveBlendshapes.name + "_NoBlendshapes";

            RemoveBlendShapesFromMesh(cleanMesh);

            SaveCleanMesh(cleanMesh, path, cleanMesh.name);
        }

        private static void RemoveSelectedBlendshapes()
        {
            string path = AssetDatabase.GetAssetPath(destinationFolderForCleanMesh);
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogError("Carpeta de destino inválida.");
                return;
            }

            Mesh cleanMesh = Object.Instantiate(meshToRemoveBlendshapes);
            cleanMesh.name = meshToRemoveBlendshapes.name + "_SelectedBlendshapesRemoved";

            RemoveBlendShapesFromMesh(cleanMesh, blendshapeSelection);

            SaveCleanMesh(cleanMesh, path, cleanMesh.name);
        }

        private static void RemoveBlendShapesFromMesh(Mesh mesh, List<bool> blendshapeToRemove = null)
        {
            Mesh cleanMesh = new Mesh
            {
                vertices = mesh.vertices,
                normals = mesh.normals,
                tangents = mesh.tangents,
                uv = mesh.uv,
                uv2 = mesh.uv2,
                uv3 = mesh.uv3,
                uv4 = mesh.uv4,
                boneWeights = mesh.boneWeights,
                bindposes = mesh.bindposes,
                subMeshCount = mesh.subMeshCount
            };

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                cleanMesh.SetIndices(mesh.GetIndices(i), mesh.GetTopology(i), i);
            }

            if (blendshapeToRemove != null)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (!blendshapeToRemove[i])
                    {
                        string blendShapeName = mesh.GetBlendShapeName(i);
                        int frameCount = mesh.GetBlendShapeFrameCount(i);

                        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            float frameWeight = mesh.GetBlendShapeFrameWeight(i, frameIndex);
                            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                            mesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                            cleanMesh.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                        }
                    }
                }
            }

            mesh.Clear();
            mesh.vertices = cleanMesh.vertices;
            mesh.normals = cleanMesh.normals;
            mesh.tangents = cleanMesh.tangents;
            mesh.uv = cleanMesh.uv;
            mesh.uv2 = cleanMesh.uv2;
            mesh.uv3 = cleanMesh.uv3;
            mesh.uv4 = cleanMesh.uv4;
            mesh.boneWeights = cleanMesh.boneWeights;
            mesh.bindposes = cleanMesh.bindposes;

            for (int i = 0; i < cleanMesh.subMeshCount; i++)
            {
                mesh.SetIndices(cleanMesh.GetIndices(i), cleanMesh.GetTopology(i), i);
            }

            for (int i = 0; i < cleanMesh.blendShapeCount; i++)
            {
                string blendShapeName = cleanMesh.GetBlendShapeName(i);
                int frameCount = cleanMesh.GetBlendShapeFrameCount(i);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float frameWeight = cleanMesh.GetBlendShapeFrameWeight(i, frameIndex);
                    Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

                    cleanMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    mesh.AddBlendShapeFrame(blendShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }

        private static void SaveCleanMesh(Mesh cleanMesh, string path, string cleanMeshName)
        {
            string savePath = Path.Combine(path, cleanMeshName + ".asset");
            savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);

            AssetDatabase.CreateAsset(cleanMesh, savePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Malla guardada en {savePath}");
            AssetDatabase.Refresh();
        }
    }
}
