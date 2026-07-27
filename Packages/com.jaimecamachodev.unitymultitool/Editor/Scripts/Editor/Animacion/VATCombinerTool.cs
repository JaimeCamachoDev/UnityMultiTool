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
    // Último paso al preparar un VAT Multiple Mesh: combina en una única malla y un único
    // draw call todas las instancias estáticas pintadas con VAT Painter (todas comparten el
    // mismo VAT Single Mesh de origen). Cada instancia conserva su posición y rotación de
    // mundo horneadas en los canales UV2/UV3 — exactamente como los lee el shader VAT
    // Multiple Mesh real (verificado contra LIT_VAT_MultipleMesh.shadergraph) — para que el
    // vertex shader pueda recolocar la posición muestreada de _Position_texture en su sitio.
    public static class VATCombinerTool
    {
        private enum VatLighting { Lit, Unlit }

        private static GameObject rootObject;
        private static VatLighting lighting = VatLighting.Lit;
        private static Shader manualShaderOverride;
        private static string outputName = "VATCrowd";
        private static DefaultAsset outputFolder;
        private static bool removeOriginalsAfterCombine = true;
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";

        private static bool useCustomBounds;
        private static Vector3 customBoundsCenter = Vector3.zero;
        private static Vector3 customBoundsSize = Vector3.one * 20f;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT Combiner") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Selecciona en la escena el objeto raíz de las instancias VAT pintadas con VAT Painter (por ejemplo 'VATPaintRoot') para combinarlas en un único draw call. Las instancias que compartan el mismo material se combinan juntas; su posición y rotación de mundo quedan horneadas en la malla para el shader VAT Multiple Mesh.",
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

                List<MeshInstance> instances = CollectInstances(rootObject);

                if (instances.Count == 0)
                {
                    contentContainer.Add(new HelpBox($"'{rootObject.name}' no tiene ninguna MeshFilter+MeshRenderer con malla y material asignados en sus hijos.", HelpBoxMessageType.Warning));
                    return;
                }

                Dictionary<Material, List<MeshInstance>> groups = GroupByMaterial(instances);

                var listPanel = new MTUIPanel("Instancias detectadas");
                listPanel.Add(new MTUIInfoLabel($"{instances.Count} instancia(s) en {groups.Count} grupo(s) de material."));
                foreach (var group in groups)
                {
                    string materialName = group.Key != null ? group.Key.name : "(sin material)";
                    int vertexCount = group.Value[0].Filter.sharedMesh.vertexCount;
                    bool uniform = group.Value.All(i => i.Filter.sharedMesh.vertexCount == vertexCount);
                    string suffix = uniform ? $"{vertexCount} vértices/instancia" : "vértices por instancia NO uniformes — no se puede combinar";
                    listPanel.Add(new MTUIInfoLabel($"• {materialName}: {group.Value.Count} instancia(s), {suffix}"));
                }
                contentContainer.Add(listPanel);

                bool anyGroupInvalid = groups.Values.Any(g => !g.All(i => i.Filter.sharedMesh.vertexCount == g[0].Filter.sharedMesh.vertexCount));
                if (anyGroupInvalid)
                {
                    contentContainer.Add(new HelpBox(
                        "Todas las instancias combinadas juntas deben provenir del mismo VAT Single Mesh (mismo número de vértices), porque el shader VAT Multiple Mesh reconstruye cada instancia a partir de un único _Position_texture compartido.",
                        HelpBoxMessageType.Error));
                }

                var optionsPanel = new MTUIPanel("Opciones") { style = { marginTop = 10 } };

                optionsPanel.Add(new MTUIInfoLabel("Shader del material combinado (VAT Multiple Mesh, incluido en el paquete)"));
                var lightingRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var lightingButtons = new List<(VatLighting mode, MTUIActionButton button)>();

                void RefreshLightingColors()
                {
                    foreach (var (mode, button) in lightingButtons)
                    {
                        bool selected = mode == lighting;
                        button.SetColors(
                            selected ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                            selected ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                            selected ? MTUIColors.BlueText : MTUIColors.NeutralText);
                    }
                }

                void AddLightingButton(string label, VatLighting mode)
                {
                    var button = new MTUIActionButton(label, () =>
                    {
                        lighting = mode;
                        RefreshLightingColors();
                    });
                    button.style.flexGrow = 1;
                    lightingButtons.Add((mode, button));
                    lightingRow.Add(button);
                }

                AddLightingButton("Lit", VatLighting.Lit);
                AddLightingButton("Unlit", VatLighting.Unlit);
                RefreshLightingColors();
                optionsPanel.Add(lightingRow);

                var shaderOverrideFoldout = new Foldout { text = "Shader personalizado (opcional)", value = false, style = { marginTop = 6 } };
                var shaderField = new ObjectField("Sustituir shader") { objectType = typeof(Shader), allowSceneObjects = false, value = manualShaderOverride };
                shaderField.RegisterValueChangedCallback(evt => manualShaderOverride = evt.newValue as Shader);
                shaderOverrideFoldout.Add(shaderField);
                optionsPanel.Add(shaderOverrideFoldout);

                Shader activeShader = GetActiveShader();
                if (activeShader == null)
                {
                    optionsPanel.Add(new HelpBox("No se encontró el shader VAT Multiple Mesh en el paquete. Reinstala o repara el paquete, o asigna uno manualmente arriba.", HelpBoxMessageType.Error));
                }

                var nameField = new TextField("Nombre del resultado") { value = outputName };
                nameField.RegisterValueChangedCallback(evt => outputName = evt.newValue);
                optionsPanel.Add(nameField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder };
                folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue as DefaultAsset);
                optionsPanel.Add(folderField);

                var removeToggle = new Toggle("Eliminar las instancias originales tras combinar") { value = removeOriginalsAfterCombine };
                removeToggle.RegisterValueChangedCallback(evt => removeOriginalsAfterCombine = evt.newValue);
                optionsPanel.Add(removeToggle);

                contentContainer.Add(optionsPanel);

                contentContainer.Add(BuildBoundsPanel());

                bool canCombine = !anyGroupInvalid && activeShader != null;

                var combineButton = new MTUIActionButton("Combinar instancias VAT", () =>
                {
                    CombineGroups(groups, activeShader, outputName, removeOriginalsAfterCombine);
                    RefreshContent();
                });
                combineButton.style.marginTop = 10;
                combineButton.SetAvailable(canCombine);
                contentContainer.Add(combineButton);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Selection.selectionChanged += RefreshContent;
                SceneView.duringSceneGui += DrawBoundsPreview;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Selection.selectionChanged -= RefreshContent;
                SceneView.duringSceneGui -= DrawBoundsPreview;
            });

            RefreshContent();

            return root;
        }

        // El combinado horneado por CombineGroups ya está en espacio de mundo (CombineMeshes
        // con useMatrices=true), así que los bounds personalizados se previsualizan
        // directamente en espacio de mundo, sin transform de referencia.
        private static void DrawBoundsPreview(SceneView sceneView)
        {
            if (!useCustomBounds)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(0.1f, 1f, 0.55f, 0.9f);
            Handles.DrawWireCube(customBoundsCenter, customBoundsSize);
            Handles.color = previousColor;
        }

        private static Shader GetActiveShader()
        {
            if (manualShaderOverride != null)
            {
                return manualShaderOverride;
            }

            return lighting == VatLighting.Lit ? VATShaderLibrary.MultipleMeshLit : VATShaderLibrary.MultipleMeshUnlit;
        }

        private static VisualElement BuildBoundsPanel()
        {
            var panel = new MTUIPanel("Bounds") { style = { marginTop = 10 } };
            panel.Add(new MTUIInfoLabel(
                "Una malla combinada de una multitud puede moverse fuera de sus bounds automáticos (calculados en bind pose) cuando el shader VAT desplaza los vértices. Activa unos bounds personalizados que envuelvan toda la zona por la que se mueve la multitud."));

            var customToggle = new Toggle("Usar bounds personalizados") { value = useCustomBounds, style = { marginTop = 4 } };
            panel.Add(customToggle);

            var boundsField = new BoundsField("Bounds") { value = new Bounds(customBoundsCenter, customBoundsSize), style = { marginTop = 4 } };
            boundsField.SetEnabled(useCustomBounds);
            boundsField.RegisterValueChangedCallback(evt =>
            {
                customBoundsCenter = evt.newValue.center;
                customBoundsSize = evt.newValue.size;
                SceneView.RepaintAll();
            });
            panel.Add(boundsField);

            if (useCustomBounds)
            {
                panel.Add(new MTUIInfoLabel("La caja verde en la vista de escena muestra estos bounds en tiempo real."));
            }

            customToggle.RegisterValueChangedCallback(evt =>
            {
                useCustomBounds = evt.newValue;
                boundsField.SetEnabled(useCustomBounds);
                SceneView.RepaintAll();
            });

            return panel;
        }

        private class MeshInstance
        {
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Material Material;
        }

        private static List<MeshInstance> CollectInstances(GameObject root)
        {
            var result = new List<MeshInstance>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                {
                    continue;
                }

                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (renderer == null || material == null)
                {
                    continue;
                }

                result.Add(new MeshInstance { Filter = filter, Renderer = renderer, Material = material });
            }

            return result;
        }

        private static Dictionary<Material, List<MeshInstance>> GroupByMaterial(List<MeshInstance> instances)
        {
            var groups = new Dictionary<Material, List<MeshInstance>>();
            foreach (MeshInstance instance in instances)
            {
                if (!groups.TryGetValue(instance.Material, out List<MeshInstance> list))
                {
                    list = new List<MeshInstance>();
                    groups[instance.Material] = list;
                }

                list.Add(instance);
            }

            return groups;
        }

        private static void CombineGroups(Dictionary<Material, List<MeshInstance>> groups, Shader shader, string baseName, bool removeOriginals)
        {
            string outputPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputPath;
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Combine VAT crowd");

            int groupIndex = 0;
            var createdObjects = new List<GameObject>();

            foreach (var kvp in groups)
            {
                Material sourceMaterial = kvp.Key;
                List<MeshInstance> instances = kvp.Value;

                int vertexCount = instances[0].Filter.sharedMesh.vertexCount;
                if (!instances.All(i => i.Filter.sharedMesh.vertexCount == vertexCount))
                {
                    Debug.LogWarning($"VAT Combiner: el grupo de material '{(sourceMaterial != null ? sourceMaterial.name : "(sin material)")}' tiene mallas con número de vértices distinto, se ha omitido.");
                    continue;
                }

                groupIndex++;
                string groupName = groups.Count > 1 ? $"{baseName}_{groupIndex}" : baseName;

                CombineInstance[] combineInstances = new CombineInstance[instances.Count];
                var offsets = new List<Vector4>(instances.Count * vertexCount);
                var rotations = new List<Vector4>(instances.Count * vertexCount);

                for (int i = 0; i < instances.Count; i++)
                {
                    Transform instanceTransform = instances[i].Filter.transform;
                    combineInstances[i] = new CombineInstance
                    {
                        mesh = instances[i].Filter.sharedMesh,
                        transform = instanceTransform.localToWorldMatrix
                    };

                    Vector3 position = instanceTransform.position;
                    Quaternion rotation = instanceTransform.rotation;
                    Vector4 offset = new Vector4(position.x, position.y, position.z, 0f);
                    Vector4 rotationVector = new Vector4(rotation.x, rotation.y, rotation.z, rotation.w);

                    for (int v = 0; v < vertexCount; v++)
                    {
                        offsets.Add(offset);
                        rotations.Add(rotationVector);
                    }
                }

                var combinedMesh = new Mesh
                {
                    name = groupName + "_Mesh",
                    indexFormat = offsets.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                combinedMesh.CombineMeshes(combineInstances, true, true);
                combinedMesh.SetUVs(2, offsets);
                combinedMesh.SetUVs(3, rotations);
                combinedMesh.RecalculateBounds();
                if (useCustomBounds)
                {
                    combinedMesh.bounds = new Bounds(customBoundsCenter, customBoundsSize);
                }

                var vatMaterial = new Material(shader) { name = groupName + "_Mat" };
                CopyMatchingTextureProperties(sourceMaterial, vatMaterial);
                CopyMatchingFloatProperties(sourceMaterial, vatMaterial);
                if (vatMaterial.HasProperty("_NumberOfMeshes")) vatMaterial.SetFloat("_NumberOfMeshes", instances.Count);
                if (vatMaterial.HasProperty("_TotalVertex")) vatMaterial.SetFloat("_TotalVertex", vertexCount);

                string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, combinedMesh.name + ".asset").Replace("\\", "/"));
                AssetDatabase.CreateAsset(combinedMesh, meshPath);

                string materialPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, vatMaterial.name + ".mat").Replace("\\", "/"));
                AssetDatabase.CreateAsset(vatMaterial, materialPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                var combinedObject = new GameObject(groupName);
                Undo.RegisterCreatedObjectUndo(combinedObject, "Combine VAT crowd");
                combinedObject.AddComponent<MeshFilter>().sharedMesh = savedMesh;
                combinedObject.AddComponent<MeshRenderer>().sharedMaterial = savedMaterial;
                createdObjects.Add(combinedObject);

                if (removeOriginals)
                {
                    foreach (MeshInstance instance in instances)
                    {
                        if (instance.Filter != null)
                        {
                            Undo.DestroyObjectImmediate(instance.Filter.gameObject);
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (createdObjects.Count > 0)
            {
                Selection.objects = createdObjects.ToArray();
            }

            Debug.Log($"VAT Combiner: {createdObjects.Count} grupo(s) combinado(s) en '{outputPath}'.");
        }

        private static void CopyMatchingTextureProperties(Material source, Material destination)
        {
            if (source == null || destination == null || source.shader == null)
            {
                return;
            }

            int propertyCount = source.shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                if (source.shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                {
                    continue;
                }

                string propertyName = source.shader.GetPropertyName(i);
                if (!destination.HasProperty(propertyName))
                {
                    continue;
                }

                Texture texture = source.GetTexture(propertyName);
                if (texture != null)
                {
                    destination.SetTexture(propertyName, texture);
                }
            }
        }

        private static void CopyMatchingFloatProperties(Material source, Material destination)
        {
            if (source == null || destination == null || source.shader == null)
            {
                return;
            }

            int propertyCount = source.shader.GetPropertyCount();
            for (int i = 0; i < propertyCount; i++)
            {
                ShaderPropertyType type = source.shader.GetPropertyType(i);
                if (type != ShaderPropertyType.Float && type != ShaderPropertyType.Range)
                {
                    continue;
                }

                string propertyName = source.shader.GetPropertyName(i);
                if (!destination.HasProperty(propertyName))
                {
                    continue;
                }

                destination.SetFloat(propertyName, source.GetFloat(propertyName));
            }
        }
    }
}
