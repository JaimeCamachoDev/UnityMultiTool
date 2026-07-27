using System.Collections.Generic;
using System.IO;
using System.Linq;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using MultitoolHub = JaimeCamachoDev.Multitool.MultitoolHubWindow;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Hornea Vertex Animation Textures (VAT) a partir de un clip y, para Single Mesh,
    // sustituye automáticamente el objeto animado de la escena por su versión estática
    // con el shader y las texturas ya asignadas. Para Multiple Mesh, este mismo horneado
    // produce el VAT Single Mesh de partida de la multitud; después hay que prepararla con
    // VAT UV Visual, VAT Painter y VAT Combiner (ver BuildMultiMeshPreparationPanel).
    public static class AnimationClipTextureBakerTool
    {
        private const string PackageName = "com.jaimecamachodev.unitymultitool";
        private const string ComputeShaderRelativePath = "Editor/Extras/ComputeShaders/MeshInfoTextureGen.compute";
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";
        private const float SampleInterval = 0.05f;

        private enum VatMeshMode { Single, Multiple }
        private enum VatLighting { Lit, Unlit }

        private static VatMeshMode meshMode = VatMeshMode.Single;
        private static VatLighting lighting = VatLighting.Lit;

        private static Shader singleMeshLitShader;
        private static Shader singleMeshUnlitShader;
        private static Shader multiMeshLitShader;
        private static Shader multiMeshUnlitShader;

        private static GameObject targetObject;
        private static DefaultAsset outputFolder;
        private static bool replaceInScene = true;

        // El shader VAT desplaza los vértices fuera de la malla estática original (bind
        // pose), así que los bounds recalculados automáticamente no cubren la animación
        // completa y Unity puede recortar (cull) el VAT cuando su centro sale de cámara.
        private static bool useCustomBounds;
        private static Vector3 customBoundsCenter = Vector3.zero;
        private static Vector3 customBoundsSize = Vector3.one * 5f;

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT Baker") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Hornea texturas de animación (VAT) a partir de los clips de un Animator y, en modo Single Mesh, sustituye automáticamente el objeto animado por su versión estática con el shader VAT ya asignado.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                EnsureDefaultShaders();

                contentContainer.Clear();
                contentContainer.Add(BuildModePanel(RefreshContent));

                if (meshMode == VatMeshMode.Multiple)
                {
                    contentContainer.Add(BuildMultiMeshPreparationPanel());
                }

                contentContainer.Add(BuildBakePanel(RefreshContent));
            }

            // Autocompleta el objetivo si hay algo animable seleccionado en la escena,
            // sin bloquear la posibilidad de arrastrar manualmente otro objeto o un prefab.
            void SyncFromSelection()
            {
                GameObject active = Selection.activeGameObject;
                if (active != null && active != targetObject && active.GetComponent<Animator>() != null
                    && active.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                {
                    targetObject = active;
                    RefreshContent();
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Selection.selectionChanged += SyncFromSelection;
                SceneView.duringSceneGui += DrawBoundsPreview;
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                Selection.selectionChanged -= SyncFromSelection;
                SceneView.duringSceneGui -= DrawBoundsPreview;
            });

            RefreshContent();

            return root;
        }

        // Previsualiza en la escena, mientras el panel está abierto, la caja de bounds
        // personalizada antes de hornear, para que el usuario pueda ajustarla visualmente en
        // vez de adivinar valores numéricos a ciegas.
        private static void DrawBoundsPreview(SceneView sceneView)
        {
            if (!useCustomBounds)
            {
                return;
            }

            SkinnedMeshRenderer skin = targetObject != null ? targetObject.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            Transform reference = skin != null ? skin.transform : targetObject != null ? targetObject.transform : null;
            if (reference == null)
            {
                return;
            }

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;

            Handles.matrix = reference.localToWorldMatrix;
            Handles.color = new Color(0.1f, 1f, 0.55f, 0.9f);
            Handles.DrawWireCube(customBoundsCenter, customBoundsSize);

            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        // Los 4 shaders VAT vienen empaquetados dentro del propio paquete: se autoasignan la
        // primera vez que se necesitan para que el usuario no tenga que arrastrarlos a mano
        // cada vez. Si el usuario los sustituye por una variante propia, esa elección se
        // respeta (solo se autoasigna cuando el campo está vacío).
        private static void EnsureDefaultShaders()
        {
            if (singleMeshLitShader == null) singleMeshLitShader = VATShaderLibrary.SingleMeshLit;
            if (singleMeshUnlitShader == null) singleMeshUnlitShader = VATShaderLibrary.SingleMeshUnlit;
            if (multiMeshLitShader == null) multiMeshLitShader = VATShaderLibrary.MultipleMeshLit;
            if (multiMeshUnlitShader == null) multiMeshUnlitShader = VATShaderLibrary.MultipleMeshUnlit;
        }

        private static VisualElement BuildModePanel(System.Action onChanged)
        {
            var panel = new MTUIPanel("Tipo de VAT");
            panel.Add(new MTUIInfoLabel("Elige primero el tipo de VAT: esto determina el shader a usar y desbloquea el resto de la herramienta."));

            var meshModeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            var lightingRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };

            var meshModeButtons = new List<(VatMeshMode mode, MTUIActionButton button)>();
            var lightingButtons = new List<(VatLighting mode, MTUIActionButton button)>();

            void RefreshChipColors()
            {
                foreach (var (mode, button) in meshModeButtons)
                {
                    bool selected = mode == meshMode;
                    button.SetColors(
                        selected ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                        selected ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                        selected ? MTUIColors.BlueText : MTUIColors.NeutralText);
                }

                foreach (var (mode, button) in lightingButtons)
                {
                    bool selected = mode == lighting;
                    button.SetColors(
                        selected ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                        selected ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                        selected ? MTUIColors.BlueText : MTUIColors.NeutralText);
                }
            }

            void AddMeshModeButton(string label, VatMeshMode mode)
            {
                var button = new MTUIActionButton(label, () =>
                {
                    if (meshMode != mode)
                    {
                        meshMode = mode;
                        onChanged();
                    }
                });
                button.style.flexGrow = 1;
                meshModeButtons.Add((mode, button));
                meshModeRow.Add(button);
            }

            void AddLightingButton(string label, VatLighting mode)
            {
                var button = new MTUIActionButton(label, () =>
                {
                    if (lighting != mode)
                    {
                        lighting = mode;
                        onChanged();
                    }
                });
                button.style.flexGrow = 1;
                lightingButtons.Add((mode, button));
                lightingRow.Add(button);
            }

            AddMeshModeButton("Single Mesh", VatMeshMode.Single);
            AddMeshModeButton("Multiple Mesh", VatMeshMode.Multiple);
            AddLightingButton("Lit", VatLighting.Lit);
            AddLightingButton("Unlit", VatLighting.Unlit);
            RefreshChipColors();

            panel.Add(meshModeRow);
            panel.Add(lightingRow);

            panel.Add(new MTUIInfoLabel($"Shader activo: {GetActiveShaderLabel()}") { style = { marginTop = 8 } });

            if (GetActiveShader() == null)
            {
                panel.Add(new HelpBox("No hay un shader asignado para esta combinación. Asígnalo abajo en \"Shaders VAT\".", HelpBoxMessageType.Warning));
            }

            var shadersFoldout = new Foldout { text = "Shaders VAT", value = false, style = { marginTop = 6 } };

            var singleLitField = new ObjectField("Single Mesh Lit") { objectType = typeof(Shader), allowSceneObjects = false, value = singleMeshLitShader };
            singleLitField.RegisterValueChangedCallback(evt => { singleMeshLitShader = evt.newValue as Shader; onChanged(); });
            shadersFoldout.Add(singleLitField);

            var singleUnlitField = new ObjectField("Single Mesh Unlit") { objectType = typeof(Shader), allowSceneObjects = false, value = singleMeshUnlitShader };
            singleUnlitField.RegisterValueChangedCallback(evt => { singleMeshUnlitShader = evt.newValue as Shader; onChanged(); });
            shadersFoldout.Add(singleUnlitField);

            shadersFoldout.Add(new HelpBox(
                "Los shaders Multiple Mesh no se usan en este horneado (que siempre produce un VAT Single Mesh): se asignan más adelante, sobre la malla ya combinada, en VAT Combiner.",
                HelpBoxMessageType.None) { style = { marginTop = 4 } });

            var multiLitField = new ObjectField("Multiple Mesh Lit (referencia)") { objectType = typeof(Shader), allowSceneObjects = false, value = multiMeshLitShader };
            multiLitField.RegisterValueChangedCallback(evt => { multiMeshLitShader = evt.newValue as Shader; onChanged(); });
            shadersFoldout.Add(multiLitField);

            var multiUnlitField = new ObjectField("Multiple Mesh Unlit (referencia)") { objectType = typeof(Shader), allowSceneObjects = false, value = multiMeshUnlitShader };
            multiUnlitField.RegisterValueChangedCallback(evt => { multiMeshUnlitShader = evt.newValue as Shader; onChanged(); });
            shadersFoldout.Add(multiUnlitField);

            panel.Add(shadersFoldout);

            return panel;
        }

        private static VisualElement BuildMultiMeshPreparationPanel()
        {
            var panel = new MTUIPanel("Preparación (Multiple Mesh)") { style = { marginTop = 10 } };
            panel.Add(new MTUIInfoLabel(
                "Para una multitud de personajes: primero hornea un VAT Single Mesh normal para un único personaje. Después usa VAT UV Visual para encuadrar las UV de sus clones dentro de un atlas compartido, VAT Painter para pintarlas en la escena y, por último, VAT Combiner para combinarlas en un único draw call. El horneado final de este panel es el mismo para ambos modos, así que solo lo necesitas para el VAT Single Mesh de partida."));

            var buttonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6, flexWrap = Wrap.Wrap } };

            var uvVisualButton = new MTUIActionButton("1. Abrir VAT UV Visual", () =>
            {
                MultitoolHub.OpenTool("VAT UV Visual");
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            buttonsRow.Add(uvVisualButton);

            var painterButton = new MTUIActionButton("2. Abrir VAT Painter", () =>
            {
                MultitoolHub.OpenTool("VAT Painter");
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            painterButton.style.marginLeft = 6;
            buttonsRow.Add(painterButton);

            var combinerButton = new MTUIActionButton("3. Abrir VAT Combiner", () =>
            {
                MultitoolHub.OpenTool("VAT Combiner");
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            combinerButton.style.marginLeft = 6;
            buttonsRow.Add(combinerButton);

            panel.Add(buttonsRow);

            return panel;
        }

        private static VisualElement BuildBakePanel(System.Action onChanged)
        {
            var panel = new MTUIPanel("Hornear VAT") { style = { marginTop = 10 } };

            string targetLabel = meshMode == VatMeshMode.Single ? "Objeto animado" : "Objeto animado (personaje base de la multitud)";
            var targetField = new ObjectField(targetLabel) { objectType = typeof(GameObject), allowSceneObjects = true, value = targetObject };
            targetField.RegisterValueChangedCallback(evt =>
            {
                targetObject = evt.newValue as GameObject;
                onChanged();
            });
            panel.Add(targetField);

            Animator animator = targetObject != null ? targetObject.GetComponent<Animator>() : null;
            SkinnedMeshRenderer skin = targetObject != null ? targetObject.GetComponentInChildren<SkinnedMeshRenderer>() : null;

            if (targetObject == null)
            {
                panel.Add(new HelpBox("Arrastra un GameObject con Animator y SkinnedMeshRenderer para poder hornear.", HelpBoxMessageType.Info));
            }
            else if (animator == null || animator.runtimeAnimatorController == null)
            {
                panel.Add(new HelpBox($"'{targetObject.name}' no tiene un Animator con un Animator Controller asignado.", HelpBoxMessageType.Warning));
            }
            else if (skin == null || skin.sharedMesh == null)
            {
                panel.Add(new HelpBox($"'{targetObject.name}' no tiene un SkinnedMeshRenderer con una Mesh asignada.", HelpBoxMessageType.Warning));
            }
            else
            {
                int clipCount = animator.runtimeAnimatorController.animationClips.Distinct().Count();
                panel.Add(new MTUIInfoLabel($"{skin.sharedMesh.vertexCount} vértices · {clipCount} clip(s) de animación detectados"));
            }

            var folderField = new ObjectField("Carpeta de salida") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder, style = { marginTop = 6 } };
            folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue as DefaultAsset);
            panel.Add(folderField);

            if (outputFolder == null)
            {
                panel.Add(new HelpBox($"Si no se asigna carpeta se usará '{DefaultOutputPath}'.", HelpBoxMessageType.Info));
            }

            panel.Add(BuildBoundsPanel(skin));

            bool isSceneInstance = targetObject != null && !EditorUtility.IsPersistent(targetObject);
            var replaceToggle = new Toggle("Reemplazar el objeto animado en la escena automáticamente") { value = replaceInScene, style = { marginTop = 6 } };
            replaceToggle.SetEnabled(isSceneInstance);
            replaceToggle.RegisterValueChangedCallback(evt => replaceInScene = evt.newValue);
            panel.Add(replaceToggle);

            if (targetObject != null && !isSceneInstance)
            {
                panel.Add(new HelpBox("El objeto es un asset del proyecto (no una instancia de escena): se generarán las texturas, la malla y el material, pero no se reemplazará nada en la escena.", HelpBoxMessageType.Info));
            }

            ComputeShader computeShader = ResolveComputeShader();
            if (computeShader == null)
            {
                panel.Add(new HelpBox("No se encontró el Compute Shader 'MeshInfoTextureGen'. Reinstala o repara el paquete.", HelpBoxMessageType.Error));
            }

            Shader activeShader = GetActiveShader();
            bool canBake = targetObject != null && animator != null && animator.runtimeAnimatorController != null
                && skin != null && skin.sharedMesh != null && activeShader != null && computeShader != null;

            var bakeButton = new MTUIActionButton("Bake VAT", () =>
            {
                BakeVat(targetObject, animator, skin, activeShader, computeShader);
                onChanged();
            });
            bakeButton.style.marginTop = 10;
            bakeButton.SetAvailable(canBake);
            panel.Add(bakeButton);

            return panel;
        }

        private static VisualElement BuildBoundsPanel(SkinnedMeshRenderer skin)
        {
            var panel = new MTUIPanel("Bounds") { style = { marginTop = 6 } };
            panel.Add(new MTUIInfoLabel(
                "El shader VAT desplaza los vértices fuera de la malla estática original, así que los bounds automáticos pueden no cubrir toda la animación y Unity puede recortar el objeto al salir de cámara. Activa unos bounds personalizados que envuelvan el recorrido completo."));

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

            var autoFitButton = new MTUIActionButton("Ajustar desde la malla del personaje (x2)", () =>
            {
                if (skin != null && skin.sharedMesh != null)
                {
                    Bounds source = skin.sharedMesh.bounds;
                    customBoundsCenter = source.center;
                    customBoundsSize = Vector3.Max(source.size * 2f, Vector3.one * 0.1f);
                    boundsField.SetValueWithoutNotify(new Bounds(customBoundsCenter, customBoundsSize));
                    SceneView.RepaintAll();
                }
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            autoFitButton.style.marginTop = 4;
            autoFitButton.SetAvailable(skin != null && skin.sharedMesh != null);
            panel.Add(autoFitButton);

            customToggle.RegisterValueChangedCallback(evt =>
            {
                useCustomBounds = evt.newValue;
                boundsField.SetEnabled(useCustomBounds);
                SceneView.RepaintAll();
            });

            return panel;
        }

        // El horneado siempre produce un VAT Single Mesh: incluso para preparar una
        // multitud (Multiple Mesh), el paso de horneado en sí solo puede muestrear una
        // SkinnedMeshRenderer a la vez. El shader VAT Multiple Mesh solo se asigna después,
        // sobre la malla ya combinada, desde VAT Combiner. El modo Multiple aquí únicamente
        // desbloquea el panel de preparación (VAT UV Visual / VAT Painter / VAT Combiner).
        private static Shader GetActiveShader()
        {
            return lighting == VatLighting.Lit ? singleMeshLitShader : singleMeshUnlitShader;
        }

        private static string GetActiveShaderLabel()
        {
            string lightLabel = lighting == VatLighting.Lit ? "Lit" : "Unlit";
            return $"Single Mesh {lightLabel}";
        }

        private static ComputeShader ResolveComputeShader()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(AnimationClipTextureBakerTool).Assembly);
            string basePath = packageInfo != null ? packageInfo.assetPath : "Packages/" + PackageName;
            string path = basePath + "/" + ComputeShaderRelativePath;
            return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }

        private static void BakeVat(GameObject target, Animator animator, SkinnedMeshRenderer skin, Shader shader, ComputeShader computeShader)
        {
            string outputPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputPath;
            string targetFolder = Path.Combine(outputPath, target.name).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips.Distinct().Where(c => c != null).ToArray();
            if (clips.Length == 0)
            {
                EditorUtility.DisplayDialog("VAT Baker", "El Animator Controller no tiene clips de animación.", "Entendido");
                return;
            }

            int vertCount = skin.sharedMesh.vertexCount;
            int texWidth = Mathf.NextPowerOfTwo(vertCount);
            Mesh sampleMesh = new Mesh();

            Texture2D firstPositionTexture = null;
            Texture2D firstNormalTexture = null;
            int firstFrameCount = 0;
            float firstClipLength = 0f;

            try
            {
                AnimationMode.StartAnimationMode();

                for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
                {
                    AnimationClip clip = clips[clipIndex];

                    if (EditorUtility.DisplayCancelableProgressBar("VAT Baker", $"Horneando '{clip.name}' ({clipIndex + 1}/{clips.Length})...", (float)clipIndex / clips.Length))
                    {
                        Debug.LogWarning("VAT Baker: horneado cancelado por el usuario.");
                        break;
                    }

                    int frames = Mathf.NextPowerOfTwo(Mathf.Max(1, (int)(clip.length / SampleInterval)));
                    float dt = clip.length / frames;
                    var infoList = new List<VertInfo>(frames * vertCount);

                    for (int i = 0; i < frames; i++)
                    {
                        AnimationMode.SampleAnimationClip(target, clip, dt * i);
                        skin.BakeMesh(sampleMesh);

                        Vector3[] positions = sampleMesh.vertices;
                        Vector3[] normals = sampleMesh.normals;
                        bool hasNormals = normals != null && normals.Length == positions.Length;

                        for (int v = 0; v < positions.Length; v++)
                        {
                            infoList.Add(new VertInfo
                            {
                                position = positions[v],
                                normal = hasNormals ? normals[v] : Vector3.up,
                                tangent = Vector3.zero
                            });
                        }
                    }

                    (Texture2D positionTexture, Texture2D normalTexture) = BakeInfoTextures(
                        computeShader, infoList, vertCount, frames, texWidth,
                        $"{target.name}.{clip.name}.PosTex", $"{target.name}.{clip.name}.NormalTex");

                    string posPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, positionTexture.name + ".asset").Replace("\\", "/"));
                    string normalPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, normalTexture.name + ".asset").Replace("\\", "/"));
                    AssetDatabase.CreateAsset(positionTexture, posPath);
                    AssetDatabase.CreateAsset(normalTexture, normalPath);

                    if (clipIndex == 0)
                    {
                        firstPositionTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(posPath);
                        firstNormalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                        firstFrameCount = frames;
                        firstClipLength = clip.length;
                    }
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(sampleMesh);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (firstPositionTexture == null)
            {
                Debug.LogWarning("VAT Baker: no se generó ninguna textura (¿se canceló el horneado?).");
                return;
            }

            Mesh bakedMesh = BuildBakedMesh(skin, vertCount, texWidth, target.name + "_VAT");
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, bakedMesh.name + ".asset").Replace("\\", "/"));
            AssetDatabase.CreateAsset(bakedMesh, meshPath);

            Material originalMaterial = skin.sharedMaterial;
            Material vatMaterial = new Material(shader) { name = target.name + "_VAT_Mat" };

            // Nombres de propiedad reales de los shaders VAT Lit/Unlit (Single/Multiple Mesh).
            if (vatMaterial.HasProperty("_Position_texture")) vatMaterial.SetTexture("_Position_texture", firstPositionTexture);
            if (vatMaterial.HasProperty("_Animation_lenght")) vatMaterial.SetFloat("_Animation_lenght", firstClipLength);

            // Nombres alternativos por si algún shader VAT usa la convención antigua.
            if (vatMaterial.HasProperty("_VAT_positions")) vatMaterial.SetTexture("_VAT_positions", firstPositionTexture);
            if (vatMaterial.HasProperty("_VAT_normals")) vatMaterial.SetTexture("_VAT_normals", firstNormalTexture);
            if (vatMaterial.HasProperty("_Framecount")) vatMaterial.SetFloat("_Framecount", firstFrameCount);
            if (vatMaterial.HasProperty("_Timeposition")) vatMaterial.SetFloat("_Timeposition", 0f);
            CopyMatchingTextureProperties(originalMaterial, vatMaterial);

            string materialPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(targetFolder, vatMaterial.name + ".mat").Replace("\\", "/"));
            AssetDatabase.CreateAsset(vatMaterial, materialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Mesh finalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Material finalMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake VAT");

            // BakeMesh() bakes vertices relative to the SkinnedMeshRenderer's own transform,
            // not the dragged target's transform. FBX rigs commonly carry a rotation offset
            // (e.g. a -90 X correction) on the mesh node that differs from the target/root
            // node, so the new object must match skin.transform's world transform, not
            // target.transform's, or it ends up rotated/mispositioned relative to the animation.
            Transform skinTransform = skin.transform;
            Vector3 skinWorldPosition = skinTransform.position;
            Quaternion skinWorldRotation = skinTransform.rotation;
            Vector3 skinWorldScale = skinTransform.lossyScale;

            GameObject vatObject = new GameObject(target.name + "_VAT");
            Undo.RegisterCreatedObjectUndo(vatObject, "Bake VAT");
            vatObject.transform.SetParent(target.transform.parent, false);
            vatObject.transform.SetPositionAndRotation(skinWorldPosition, skinWorldRotation);

            Transform vatParent = vatObject.transform.parent;
            Vector3 parentLossyScale = vatParent != null ? vatParent.lossyScale : Vector3.one;
            vatObject.transform.localScale = new Vector3(
                parentLossyScale.x != 0f ? skinWorldScale.x / parentLossyScale.x : skinWorldScale.x,
                parentLossyScale.y != 0f ? skinWorldScale.y / parentLossyScale.y : skinWorldScale.y,
                parentLossyScale.z != 0f ? skinWorldScale.z / parentLossyScale.z : skinWorldScale.z);

            MeshFilter meshFilter = vatObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = finalMesh;
            MeshRenderer meshRenderer = vatObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = finalMaterial;

            bool isSceneInstance = !EditorUtility.IsPersistent(target);
            if (replaceInScene && isSceneInstance)
            {
                Undo.RecordObject(target, "Disable original animated object");
                target.SetActive(false);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = vatObject;
            Debug.Log($"VAT Baker: horneado completo. {clips.Length} clip(s) exportado(s) a '{targetFolder}'. Objeto VAT creado: '{vatObject.name}'.");
        }

        private static (Texture2D position, Texture2D normal) BakeInfoTextures(
            ComputeShader computeShader, List<VertInfo> infoList, int vertCount, int frames, int texWidth,
            string positionName, string normalName)
        {
            // Width = vertex axis, height = frame axis: matches how the VAT shaders
            // sample this texture (vertex from the built-in VertexID node on X, a
            // continuous 0..1 time value on Y).
            var positionRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true };
            var normalRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true };
            positionRt.Create();
            normalRt.Create();

            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            int kernel = computeShader.FindKernel("CSMain");
            computeShader.SetInt("VertCount", vertCount);
            computeShader.SetInt("FrameCount", frames);
            computeShader.SetBuffer(kernel, "Info", buffer);
            computeShader.SetTexture(kernel, "OutPosition", positionRt);
            computeShader.SetTexture(kernel, "OutNormal", normalRt);

            computeShader.Dispatch(kernel, Mathf.CeilToInt(texWidth / 8f), Mathf.CeilToInt(frames / 8f), 1);

            buffer.Release();

            Texture2D positionTexture = RenderTextureToTexture2D(positionRt);
            positionTexture.name = positionName;
            Texture2D normalTexture = RenderTextureToTexture2D(normalRt);
            normalTexture.name = normalName;

            RenderTexture.active = null;
            positionRt.Release();
            normalRt.Release();
            Object.DestroyImmediate(positionRt);
            Object.DestroyImmediate(normalRt);

            return (positionTexture, normalTexture);
        }

        private static Mesh BuildBakedMesh(SkinnedMeshRenderer skin, int vertCount, int texWidth, string meshName)
        {
            Mesh source = skin.sharedMesh;
            Mesh baked = new Mesh
            {
                name = meshName,
                indexFormat = source.indexFormat
            };

            baked.SetVertices(new List<Vector3>(source.vertices));
            baked.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                baked.SetTriangles(source.GetTriangles(s), s);
            }

            if (source.normals != null && source.normals.Length == vertCount)
            {
                baked.SetNormals(new List<Vector3>(source.normals));
            }

            if (source.uv != null && source.uv.Length == vertCount)
            {
                baked.SetUVs(0, new List<Vector2>(source.uv));
            }

            // UV1: columna del vértice dentro de la textura VAT (columna=vértice, fila=frame).
            // El shader de referencia obtiene el índice de vértice del nodo VertexID en vez
            // de leer este UV, pero se deja escrito por si algún shader VAT lo usa en su lugar.
            var vatUv = new List<Vector2>(vertCount);
            for (int v = 0; v < vertCount; v++)
            {
                vatUv.Add(new Vector2((v + 0.5f) / texWidth, 0f));
            }
            baked.SetUVs(1, vatUv);

            baked.RecalculateBounds();
            if (useCustomBounds)
            {
                baked.bounds = new Bounds(customBoundsCenter, customBoundsSize);
            }

            return baked;
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
                if (source.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
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

        private static Texture2D RenderTextureToTexture2D(RenderTexture rt)
        {
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct VertInfo
        {
            public Vector3 position;
            public Vector3 normal;
            public Vector3 tangent;
        }
    }
}
