using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace VZOptizone
{
    public static class BlendshapeRemovalTool
    {
        private static Mesh meshToRemoveBlendshapes; // Malla a la que se le eliminarán los blendshapes
        private static Object destinationFolderForCleanMesh; // Carpeta de destino para la malla sin blendshapes
        private static List<bool> blendshapeSelection; // Lista para almacenar la selección de blendshapes

        public static void DrawTool()
        {
            GUILayout.Label("Eliminar Todos los Blendshapes", EditorStyles.boldLabel);

            // Selección de la malla y la carpeta de destino
            meshToRemoveBlendshapes = (Mesh)EditorGUILayout.ObjectField("Malla para limpiar", meshToRemoveBlendshapes, typeof(Mesh), true);
            destinationFolderForCleanMesh = EditorGUILayout.ObjectField("Carpeta de Destino para la Malla Limpiada", destinationFolderForCleanMesh, typeof(Object), false);

            if (meshToRemoveBlendshapes != null && destinationFolderForCleanMesh != null)
            {
                // Botón para eliminar todos los blendshapes
                if (GUILayout.Button("Eliminar Todos los Blendshapes"))
                {
                    RemoveAllBlendshapes();
                }

                GUILayout.Space(20);

                // Sección para eliminar blendshapes específicos
                GUILayout.Label("Eliminar Blendshapes Específicos", EditorStyles.boldLabel);

                // Mostrar blendshapes de la malla seleccionada
                ShowBlendshapeSelection();

                // Botón para eliminar blendshapes seleccionados
                if (GUILayout.Button("Eliminar Blendshapes Seleccionados"))
                {
                    RemoveSelectedBlendshapes();
                }
            }
            else
            {
                GUILayout.Label("Por favor, selecciona una malla y una carpeta de destino.", EditorStyles.helpBox);
            }
        }


        private static void ShowBlendshapeSelection()
        {
            if (blendshapeSelection == null || blendshapeSelection.Count != meshToRemoveBlendshapes.blendShapeCount)
            {
                blendshapeSelection = new List<bool>(new bool[meshToRemoveBlendshapes.blendShapeCount]);
            }

            GUILayout.Label("Selecciona los blendshapes que deseas eliminar:", EditorStyles.boldLabel);

            for (int i = 0; i < meshToRemoveBlendshapes.blendShapeCount; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Blendshape {meshToRemoveBlendshapes.GetBlendShapeName(i)}", GUILayout.MaxWidth(300)); // Alinear el nombre a la izquierda

                GUILayout.FlexibleSpace(); // Añadir espacio flexible para empujar la casilla hacia la derecha

                blendshapeSelection[i] = EditorGUILayout.Toggle(blendshapeSelection[i], GUILayout.Width(20)); // Alinear la casilla de verificación a la derecha
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void RemoveAllBlendshapes()
        {
            string path = AssetDatabase.GetAssetPath(destinationFolderForCleanMesh);
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogError("Carpeta de destino inválida.");
                return;
            }

            // Crear una copia de la malla original sin blendshapes
            Mesh cleanMesh = Object.Instantiate(meshToRemoveBlendshapes);
            cleanMesh.name = meshToRemoveBlendshapes.name + "_NoBlendshapes";

            // Remover todos los blendshapes
            RemoveBlendShapesFromMesh(cleanMesh);

            // Guardar la malla en la carpeta de destino
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

            // Crear una copia de la malla original y eliminar solo los blendshapes seleccionados
            Mesh cleanMesh = Object.Instantiate(meshToRemoveBlendshapes);
            cleanMesh.name = meshToRemoveBlendshapes.name + "_SelectedBlendshapesRemoved";

            // Remover solo los blendshapes seleccionados
            RemoveBlendShapesFromMesh(cleanMesh, blendshapeSelection);

            // Guardar la malla en la carpeta de destino
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

            // Copiar los submeshes
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                cleanMesh.SetIndices(mesh.GetIndices(i), mesh.GetTopology(i), i);
            }

            // Copiar solo los blendshapes que queremos mantener
            if (blendshapeToRemove != null)
            {
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (!blendshapeToRemove[i])
                    {
                        // Si el blendshape no está marcado para eliminar, lo mantenemos
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

            // Asignar los datos de la nueva malla sin los blendshapes eliminados
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

            // Asignar los blendshapes restantes
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