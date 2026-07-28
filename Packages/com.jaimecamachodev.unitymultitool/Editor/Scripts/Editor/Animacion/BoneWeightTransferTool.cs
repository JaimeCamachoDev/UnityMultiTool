using System.Collections.Generic;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    public static class BoneWeightTransferTool
    {
        private static SkinnedMeshRenderer originalMeshRenderer;
        private static SkinnedMeshRenderer modifiedMeshRenderer;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("Transfer Bone Weight") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Transfiere los bone weights de una malla original a una versión recortada de la misma malla, emparejando vértices por posición.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                var sourcePanel = new MTUIPanel("Paso 1: Selecciona la malla original y la recortada");

                var originalField = new ObjectField("Malla Original") { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true, value = originalMeshRenderer };
                originalField.RegisterValueChangedCallback(evt => { originalMeshRenderer = evt.newValue as SkinnedMeshRenderer; RefreshContent(); });
                sourcePanel.Add(originalField);

                var modifiedField = new ObjectField("Malla Recortada") { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true, value = modifiedMeshRenderer };
                modifiedField.RegisterValueChangedCallback(evt => { modifiedMeshRenderer = evt.newValue as SkinnedMeshRenderer; RefreshContent(); });
                sourcePanel.Add(modifiedField);

                contentContainer.Add(sourcePanel);

                bool canTransfer = originalMeshRenderer != null && modifiedMeshRenderer != null;
                if (!canTransfer)
                {
                    contentContainer.Add(new HelpBox("Asegúrate de seleccionar ambas mallas.", HelpBoxMessageType.Warning) { style = { marginTop = 10 } });
                }

                var transferButton = new MTUIActionButton("Transferir Bone Weights", TransferBoneWeights);
                transferButton.style.marginTop = 10;
                transferButton.SetAvailable(canTransfer);
                contentContainer.Add(transferButton);
            }

            RefreshContent();
            return root;
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
            var newWeights = new BoneWeight[modifiedMesh.vertexCount];

            Vector3[] originalVertices = originalMesh.vertices;
            Vector3[] modifiedVertices = modifiedMesh.vertices;

            var vertexMap = new Dictionary<int, int>();

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
                if (vertexMap.TryGetValue(i, out int originalIndex))
                {
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

            EditorUtility.SetDirty(modifiedMesh);
            AssetDatabase.SaveAssets();

            Debug.Log("Transferencia de Bone Weights completada.");
        }
    }
}
