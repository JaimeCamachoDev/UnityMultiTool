using System.Collections.Generic;
using System.IO;
using System.Linq;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Un VAT solo puede hornear una única SkinnedMeshRenderer. Cuando un personaje está
    // formado por varias (cuerpo, ropa, pelo...), esta herramienta las combina en una
    // sola malla — conservando huesos y bind poses — dejándola lista para VAT Painter
    // y, después, para VAT Baker.
    public static class VATCombinerTool
    {
        private static GameObject rootObject;
        private static string outputName = "CombinedForVAT";
        private static DefaultAsset outputFolder;
        private static bool disableOriginalRenderers = true;
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT Combiner") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Selecciona en la escena el objeto raíz de un personaje formado por varias SkinnedMeshRenderer (cuerpo, ropa, pelo...) para combinarlas en una única malla lista para hornear un VAT.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                rootObject = Selection.activeGameObject;

                if (rootObject == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona un objeto de la escena para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                List<SkinnedMeshRenderer> renderers = rootObject.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(r => r.sharedMesh != null)
                    .ToList();

                if (renderers.Count == 0)
                {
                    contentContainer.Add(new HelpBox($"'{rootObject.name}' no tiene ninguna SkinnedMeshRenderer con una Mesh asignada en sus hijos.", HelpBoxMessageType.Warning));
                    return;
                }

                bool unreadable = renderers.Any(r => !r.sharedMesh.isReadable);
                if (unreadable)
                {
                    contentContainer.Add(new HelpBox("Alguna de las mallas no tiene Read/Write habilitado en sus Import Settings; actívalo para poder combinarlas.", HelpBoxMessageType.Error));
                    return;
                }

                var listPanel = new MTUIPanel("Mallas detectadas");
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    listPanel.Add(new MTUIInfoLabel($"• {renderer.name} — {renderer.sharedMesh.vertexCount} vértices, {renderer.sharedMaterials.Length} material(es)"));
                }
                contentContainer.Add(listPanel);

                if (renderers.Count == 1)
                {
                    contentContainer.Add(new HelpBox("Solo se detectó una malla: no es necesario combinar, ya puedes hornearla directamente en VAT Baker.", HelpBoxMessageType.Info));
                }

                var optionsPanel = new MTUIPanel("Opciones") { style = { marginTop = 10 } };

                var nameField = new TextField("Nombre del resultado") { value = outputName };
                nameField.RegisterValueChangedCallback(evt => outputName = evt.newValue);
                optionsPanel.Add(nameField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder };
                folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue as DefaultAsset);
                optionsPanel.Add(folderField);

                var disableToggle = new Toggle("Desactivar las SkinnedMeshRenderer originales") { value = disableOriginalRenderers };
                disableToggle.RegisterValueChangedCallback(evt => disableOriginalRenderers = evt.newValue);
                optionsPanel.Add(disableToggle);

                contentContainer.Add(optionsPanel);

                var combineButton = new MTUIActionButton("Combinar mallas", () =>
                {
                    Combine(rootObject, renderers, outputName, disableOriginalRenderers);
                    RefreshContent();
                });
                combineButton.style.marginTop = 10;
                combineButton.SetAvailable(renderers.Count > 1);
                contentContainer.Add(combineButton);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void Combine(GameObject root, List<SkinnedMeshRenderer> sources, string meshName, bool disableOriginals)
        {
            CombineSkinnedMeshes(sources, meshName, out Mesh combinedMesh, out Transform[] combinedBones, out Material[] combinedMaterials, out Transform rootBone);

            string outputPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputPath;
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            string safeName = string.IsNullOrWhiteSpace(meshName) ? "CombinedForVAT" : meshName;
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, safeName + ".asset").Replace("\\", "/"));
            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Combine meshes for VAT");

            var combinedObject = new GameObject(safeName);
            Undo.RegisterCreatedObjectUndo(combinedObject, "Combine meshes for VAT");
            combinedObject.transform.SetParent(root.transform, false);

            SkinnedMeshRenderer combinedRenderer = combinedObject.AddComponent<SkinnedMeshRenderer>();
            combinedRenderer.sharedMesh = savedMesh;
            combinedRenderer.bones = combinedBones;
            combinedRenderer.rootBone = rootBone;
            combinedRenderer.sharedMaterials = combinedMaterials;

            if (disableOriginals)
            {
                foreach (SkinnedMeshRenderer source in sources)
                {
                    Undo.RecordObject(source, "Disable original renderer");
                    source.enabled = false;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = combinedObject;
            Debug.Log($"VAT Combiner: {sources.Count} mallas combinadas en '{combinedObject.name}' ({savedMesh.vertexCount} vértices).");
        }

        private static void CombineSkinnedMeshes(
            List<SkinnedMeshRenderer> sources, string meshName,
            out Mesh combinedMesh, out Transform[] combinedBones, out Material[] combinedMaterials, out Transform rootBone)
        {
            var boneList = new List<Transform>();
            var boneIndexMap = new Dictionary<Transform, int>();
            var bindposesByBoneIndex = new Dictionary<int, Matrix4x4>();

            int GetOrAddBoneIndex(Transform bone)
            {
                if (!boneIndexMap.TryGetValue(bone, out int index))
                {
                    index = boneList.Count;
                    boneList.Add(bone);
                    boneIndexMap[bone] = index;
                }
                return index;
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var boneWeights = new List<BoneWeight>();
            var submeshTriangleLists = new List<List<int>>();
            var materials = new List<Material>();

            int vertexOffset = 0;

            foreach (SkinnedMeshRenderer source in sources)
            {
                Mesh mesh = source.sharedMesh;
                Transform[] sourceBones = source.bones;
                Matrix4x4[] sourceBindposes = mesh.bindposes;

                var localToGlobalBoneIndex = new int[sourceBones.Length];
                for (int i = 0; i < sourceBones.Length; i++)
                {
                    int globalIndex = GetOrAddBoneIndex(sourceBones[i]);
                    localToGlobalBoneIndex[i] = globalIndex;
                    if (!bindposesByBoneIndex.ContainsKey(globalIndex) && i < sourceBindposes.Length)
                    {
                        bindposesByBoneIndex[globalIndex] = sourceBindposes[i];
                    }
                }

                int vertCount = mesh.vertexCount;
                vertices.AddRange(mesh.vertices);
                normals.AddRange(mesh.normals != null && mesh.normals.Length == vertCount ? mesh.normals : new Vector3[vertCount]);
                tangents.AddRange(mesh.tangents != null && mesh.tangents.Length == vertCount ? mesh.tangents : new Vector4[vertCount]);
                uvs.AddRange(mesh.uv != null && mesh.uv.Length == vertCount ? mesh.uv : new Vector2[vertCount]);

                BoneWeight[] sourceWeights = mesh.boneWeights;
                for (int i = 0; i < vertCount; i++)
                {
                    BoneWeight w = i < sourceWeights.Length ? sourceWeights[i] : default;
                    w.boneIndex0 = localToGlobalBoneIndex[w.boneIndex0];
                    w.boneIndex1 = localToGlobalBoneIndex[w.boneIndex1];
                    w.boneIndex2 = localToGlobalBoneIndex[w.boneIndex2];
                    w.boneIndex3 = localToGlobalBoneIndex[w.boneIndex3];
                    boneWeights.Add(w);
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    int[] tris = mesh.GetTriangles(s);
                    var offsetTris = new List<int>(tris.Length);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        offsetTris.Add(tris[i] + vertexOffset);
                    }
                    submeshTriangleLists.Add(offsetTris);

                    Material mat = s < source.sharedMaterials.Length ? source.sharedMaterials[s] : null;
                    materials.Add(mat);
                }

                vertexOffset += vertCount;
            }

            combinedMesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            combinedMesh.SetVertices(vertices);
            combinedMesh.SetNormals(normals);
            combinedMesh.SetTangents(tangents);
            combinedMesh.SetUVs(0, uvs);
            combinedMesh.boneWeights = boneWeights.ToArray();

            var bindposesArray = new Matrix4x4[boneList.Count];
            for (int i = 0; i < boneList.Count; i++)
            {
                bindposesArray[i] = bindposesByBoneIndex.TryGetValue(i, out Matrix4x4 bp) ? bp : Matrix4x4.identity;
            }
            combinedMesh.bindposes = bindposesArray;

            combinedMesh.subMeshCount = submeshTriangleLists.Count;
            for (int s = 0; s < submeshTriangleLists.Count; s++)
            {
                combinedMesh.SetTriangles(submeshTriangleLists[s], s);
            }

            combinedMesh.RecalculateBounds();

            combinedBones = boneList.ToArray();
            combinedMaterials = materials.ToArray();
            rootBone = sources[0].rootBone != null ? sources[0].rootBone : sources[0].transform;
        }
    }
}
