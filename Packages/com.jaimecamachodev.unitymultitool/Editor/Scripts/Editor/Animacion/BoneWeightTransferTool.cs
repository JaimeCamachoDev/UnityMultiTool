using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace VZOptizone
{
    public static class BoneWeightTransferTool
    {
        private static SkinnedMeshRenderer originalMeshRenderer;
        private static SkinnedMeshRenderer modifiedMeshRenderer;

        public static void DrawTool()
        {
            EditorGUILayout.LabelField("Paso 1: Selecciona la malla original y la recortada", EditorStyles.boldLabel);

            originalMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Malla Original", originalMeshRenderer, typeof(SkinnedMeshRenderer), true);
            modifiedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Malla Recortada", modifiedMeshRenderer, typeof(SkinnedMeshRenderer), true);

            GUILayout.Space(20);

            if (GUILayout.Button("Transferir Bone Weights"))
            {
                if (originalMeshRenderer != null && modifiedMeshRenderer != null)
                {
                    TransferBoneWeights();
                }
                else
                {
                    Debug.LogError("Asegúrate de seleccionar ambas mallas.");
                }
            }
        }

        private static void TransferBoneWeights()
        {
            Mesh originalMesh = originalMeshRenderer.sharedMesh;
            Mesh modifiedMesh = modifiedMeshRenderer.sharedMesh;

            if (originalMesh == null || modifiedMesh == null)
            {
                Debug.LogError("Una o ambas mallas no tienen una malla asignada.");
                return;
            }

            BoneWeight[] originalWeights = originalMesh.boneWeights;
            BoneWeight[] newWeights = new BoneWeight[modifiedMesh.vertexCount];

            Vector3[] originalVertices = originalMesh.vertices;
            Vector3[] modifiedVertices = modifiedMesh.vertices;

            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            // Mapeo de vértices por posición
            for (int i = 0; i < modifiedVertices.Length; i++)
            {
                Vector3 modPos = modifiedMeshRenderer.transform.TransformPoint(modifiedVertices[i]);
                for (int j = 0; j < originalVertices.Length; j++)
                {
                    Vector3 origPos = originalMeshRenderer.transform.TransformPoint(originalVertices[j]);
                    if (Vector3.Distance(modPos, origPos) < 0.001f)
                    {
                        vertexMap[i] = j;
                        break;
                    }
                }
            }

            for (int i = 0; i < modifiedMesh.vertexCount; i++)
            {
                if (vertexMap.ContainsKey(i))
                {
                    int originalIndex = vertexMap[i];
                    newWeights[i] = originalWeights[originalIndex];
                }
                else
                {
                    Debug.LogWarning($"Vértice {i} no encontró un peso correspondiente. Asignando valores predeterminados.");
                    newWeights[i] = new BoneWeight();
                }
            }

            modifiedMesh.boneWeights = newWeights;
            modifiedMeshRenderer.sharedMesh = modifiedMesh;

            Debug.Log("Transferencia de Bone Weights completada.");
        }
    }
}
