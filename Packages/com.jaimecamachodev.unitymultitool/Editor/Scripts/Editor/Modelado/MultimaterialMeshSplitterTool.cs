using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace VZ_Optizone
{
    public static class MultimaterialMeshSplitterTool
    {
        private static Mesh meshMultiMat; // meshMultiMat para dividir en submeshes
        private static Object destinationFolder; // Carpeta donde se guardarán los submeshes

        public static void DrawTool()
        {
            GUILayout.Label("Separar Submeshes por Material", EditorStyles.boldLabel);

            // Seleccionar el meshMultiMat
            meshMultiMat = (Mesh)EditorGUILayout.ObjectField("Skinned Mesh Renderer", meshMultiMat, typeof(Mesh), true);

            // Seleccionar la carpeta de destino
            destinationFolder = EditorGUILayout.ObjectField("Carpeta de Destino", destinationFolder, typeof(Object), false);

            GUILayout.Space(20);

            // Botón para dividir y guardar los submeshes
            if (GUILayout.Button("Dividir y Guardar Submeshes"))
            {
                if (meshMultiMat != null && destinationFolder != null)
                {
                    SplitAndSaveSubmeshes();
                }
                else
                {
                    Debug.LogWarning("Por favor, selecciona un Skinned Mesh Renderer y una carpeta de destino.");
                }
            }
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

            for (int i = 0; i < originalMesh.subMeshCount; i++)
            {
                Mesh submesh = new Mesh
                {
                    name = originalMesh.name + "_Submesh_" + (i + 1),
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
