using System;
using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Primer paso al preparar un VAT Multiple Mesh: varias mallas clonadas de un mismo
    // VAT Single Mesh (o de distintos bakes) necesitan compartir un único material. Esta
    // herramienta empaqueta sus texturas de referencia en un atlas y permite reencuadrar
    // visualmente las UV0 de cada clon dentro de su celda, para que todas terminen usando
    // el mismo material/atlas antes de pasar por VAT Painter y VAT Combiner.
    public static class VATUVVisualTool
    {
        private static bool showAtlasBuilder = true;
        private static int atlasImageCount = 1;
        private static readonly List<Texture2D> atlasSourceTextures = new List<Texture2D>();
        private static readonly int[] atlasResolutionSizes = { 256, 512, 1024, 2048 };
        private static int atlasCellResolution = 1024;
        private static Texture2D generatedAtlas;
        private static Texture2D atlasTexture;
        private static string atlasOutputName = "VAT_UV_Atlas";
        private static DefaultAsset atlasOutputFolder;
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";

        private static bool lockUniformScale = true;
        private static bool isDragging;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartPosition;
        private static int dragEntryIndex = -1;

        private static readonly Color[] previewPalette =
        {
            new Color(0.00f, 0.78f, 1.00f),
            new Color(1.00f, 0.55f, 0.35f),
            new Color(0.40f, 0.90f, 0.35f),
            new Color(0.90f, 0.30f, 0.80f),
            new Color(1.00f, 0.85f, 0.30f),
            new Color(0.55f, 0.60f, 1.00f),
            new Color(0.35f, 0.85f, 0.75f),
            new Color(0.95f, 0.45f, 0.65f)
        };

        private static readonly List<UvTargetEntry> targets = new List<UvTargetEntry>();
        private static GameObject cloneSourceObject;
        private static int activeIndex = -1;
        private static string statusMessage = string.Empty;
        private static HelpBoxMessageType statusType = HelpBoxMessageType.Info;

        private class UvTargetEntry
        {
            public string DisplayName;
            public MeshFilter Filter;
            public Mesh Mesh;
            public Vector2[] Uvs;
            public Vector2[] InitialUvs;
            public int[] Triangles;
            public Color FillColor;
            public Color OutlineColor;
            public bool Visible = true;
            public Vector2 TransformPosition = Vector2.zero;
            public Vector2 TransformScale = Vector2.one;
            public float TransformRotation;
            public bool OwnsMesh;

            public bool IsValidIndex(int index) => Uvs != null && index >= 0 && index < Uvs.Length;
        }

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT UV Visual") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Primer paso para un VAT Multiple Mesh: empaqueta varias texturas de referencia en un atlas y reencuadra visualmente las UV0 de cada malla clonada dentro de su celda, para que todas puedan compartir un único material. Después usa VAT Painter para pintarlas en la escena y VAT Combiner para combinarlas.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();
                BuildBody(contentContainer, RefreshContent);
            }

            RefreshContent();

            return root;
        }

        private static void BuildBody(VisualElement container, Action refresh)
        {
            var statusContainer = new VisualElement();
            RefreshStatusBox(statusContainer);
            container.Add(statusContainer);

            container.Add(BuildAtlasBuilderPanel(refresh));
            container.Add(BuildAtlasInUsePanel(refresh));
            container.Add(BuildTargetListPanel(refresh));

            var transformContainer = new VisualElement { style = { marginTop = 6 } };
            var legendContainer = new VisualElement();

            IMGUIContainer canvasContainer = new IMGUIContainer(() =>
            {
                if (!HasValidPreviewData())
                {
                    return;
                }

                Rect previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.ExpandWidth(true), GUILayout.MaxHeight(420f));
                DrawPreviewBackground(previewRect);

                if (atlasTexture != null && Event.current.type == EventType.Repaint)
                {
                    GUI.DrawTexture(previewRect, atlasTexture, ScaleMode.ScaleToFit);
                }

                DrawPreviewGrid(previewRect);
                DrawPreview(previewRect);
            });

            void RefreshTransform()
            {
                transformContainer.Clear();

                UvTargetEntry active = GetActiveEntry();
                bool enabled = active != null;

                var positionField = new Vector2Field("Posición") { value = active != null ? active.TransformPosition : Vector2.zero };
                positionField.SetEnabled(enabled);
                positionField.RegisterValueChangedCallback(evt =>
                {
                    if (active != null)
                    {
                        active.TransformPosition = evt.newValue;
                        canvasContainer.MarkDirtyRepaint();
                    }
                });
                transformContainer.Add(positionField);

                var scaleRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var scaleField = new Vector2Field("Escala") { value = active != null ? active.TransformScale : Vector2.one, style = { flexGrow = 1 } };

                MTUIActionButton uniformToggle = null;
                void RefreshUniformVisual()
                {
                    uniformToggle.SetColors(
                        lockUniformScale ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                        lockUniformScale ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                        lockUniformScale ? MTUIColors.BlueText : MTUIColors.NeutralText);
                }

                uniformToggle = new MTUIActionButton("Uniforme", () =>
                {
                    lockUniformScale = !lockUniformScale;
                    RefreshUniformVisual();
                    if (lockUniformScale && active != null)
                    {
                        float uniform = Mathf.Max(0.01f, (active.TransformScale.x + active.TransformScale.y) * 0.5f);
                        active.TransformScale = new Vector2(uniform, uniform);
                        scaleField.SetValueWithoutNotify(active.TransformScale);
                        canvasContainer.MarkDirtyRepaint();
                    }
                });
                RefreshUniformVisual();

                scaleField.RegisterValueChangedCallback(evt =>
                {
                    Vector2 newScale = evt.newValue;
                    if (lockUniformScale)
                    {
                        float uniform = Mathf.Max(0.01f, newScale.x);
                        newScale = new Vector2(uniform, uniform);
                    }
                    newScale.x = Mathf.Clamp(newScale.x, 0.01f, 100f);
                    newScale.y = Mathf.Clamp(newScale.y, 0.01f, 100f);
                    scaleField.SetValueWithoutNotify(newScale);
                    if (active != null)
                    {
                        active.TransformScale = newScale;
                        canvasContainer.MarkDirtyRepaint();
                    }
                });
                scaleField.SetEnabled(enabled);
                uniformToggle.SetAvailable(enabled);
                scaleRow.Add(scaleField);
                scaleRow.Add(uniformToggle);
                transformContainer.Add(scaleRow);

                var rotationSlider = new Slider("Rotación", -360f, 360f) { value = active != null ? active.TransformRotation : 0f };
                rotationSlider.SetEnabled(enabled);
                rotationSlider.RegisterValueChangedCallback(evt =>
                {
                    if (active != null)
                    {
                        active.TransformRotation = evt.newValue;
                        canvasContainer.MarkDirtyRepaint();
                    }
                });
                transformContainer.Add(rotationSlider);

                if (!enabled)
                {
                    transformContainer.Add(new HelpBox("Agrega mallas objetivo y selecciona una para editar su transformación UV.", HelpBoxMessageType.Info));
                }
            }

            void RefreshLegend()
            {
                legendContainer.Clear();
                if (targets.Count == 0)
                {
                    return;
                }

                legendContainer.Add(new Label("Mallas objetivo") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
                var scroll = new ScrollView { style = { maxHeight = 160 } };

                for (int i = 0; i < targets.Count; i++)
                {
                    int index = i;
                    UvTargetEntry entry = targets[index];
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };

                    var visibleToggle = new Toggle { value = entry.Visible, style = { marginRight = 4 } };
                    visibleToggle.RegisterValueChangedCallback(evt =>
                    {
                        entry.Visible = evt.newValue;
                        canvasContainer.MarkDirtyRepaint();
                    });
                    row.Add(visibleToggle);

                    row.Add(new VisualElement { style = { width = 16, height = 16, backgroundColor = entry.OutlineColor, marginRight = 4 } });

                    bool isActive = index == activeIndex;
                    var nameButton = new MTUIActionButton(entry.DisplayName, () =>
                    {
                        SetActiveIndex(index);
                        refresh();
                    }, isActive ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                       isActive ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                       isActive ? MTUIColors.BlueText : MTUIColors.NeutralText,
                       TextAnchor.MiddleLeft);
                    nameButton.style.flexGrow = 1;
                    row.Add(nameButton);

                    var removeButton = new MTUIActionButton("X", () =>
                    {
                        targets.RemoveAt(index);
                        if (activeIndex >= targets.Count)
                        {
                            activeIndex = targets.Count - 1;
                        }
                        refresh();
                    }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
                    removeButton.style.marginLeft = 4;
                    removeButton.style.width = 24;
                    row.Add(removeButton);

                    scroll.Add(row);
                }

                legendContainer.Add(scroll);
            }

            container.Add(new Label("Transformación UV") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            container.Add(transformContainer);
            container.Add(legendContainer);
            container.Add(canvasContainer);

            var actionsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            var applyButton = new MTUIActionButton("Aplicar UV a la malla", () =>
            {
                ApplyActiveTransform();
                refresh();
            });
            var restoreButton = new MTUIActionButton("Restaurar UV originales", () =>
            {
                RestoreActiveToOriginal();
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            restoreButton.style.marginLeft = 6;
            bool hasActive = GetActiveEntry() != null;
            applyButton.SetAvailable(hasActive);
            restoreButton.SetAvailable(hasActive);
            actionsRow.Add(applyButton);
            actionsRow.Add(restoreButton);
            actionsRow.Add(new VisualElement { style = { flexGrow = 1 } });
            actionsRow.Add(new MTUIActionButton("Restablecer gizmo", () =>
            {
                ResetActiveTransform();
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));
            container.Add(actionsRow);

            RefreshTransform();
            RefreshLegend();
        }

        private static void RefreshStatusBox(VisualElement container)
        {
            container.Clear();
            if (!string.IsNullOrEmpty(statusMessage))
            {
                container.Add(new HelpBox(statusMessage, statusType));
            }
        }

        private static VisualElement BuildAtlasBuilderPanel(Action refresh)
        {
            var panel = new MTUIPanel("Generador de atlas") { style = { marginTop = 6 } };

            var foldout = new Foldout { text = "Atlas de referencia", value = showAtlasBuilder };
            foldout.RegisterValueChangedCallback(evt => showAtlasBuilder = evt.newValue);

            foldout.Add(new HelpBox("Combina varias texturas de referencia (una por cada variación/clon) en una cuadrícula uniforme.", HelpBoxMessageType.None));

            // El slider de conteo NO debe disparar un refresh() de página completa: eso
            // destruiría y recrearía el propio slider a mitad de un arrastre continuo,
            // dejando el arrastre "enganchado"/bloqueado. Solo se reconstruyen los campos
            // de imagen (imageFieldsContainer), que sí pueden cambiar de tamaño.
            var imageFieldsContainer = new VisualElement();

            void RefreshImageFields()
            {
                imageFieldsContainer.Clear();
                EnsureAtlasSourceListSize();
                for (int i = 0; i < atlasSourceTextures.Count; i++)
                {
                    int index = i;
                    var textureField = new ObjectField($"Imagen {i + 1}") { objectType = typeof(Texture2D), allowSceneObjects = false, value = atlasSourceTextures[index], style = { marginLeft = 15 } };
                    textureField.RegisterValueChangedCallback(evt => atlasSourceTextures[index] = evt.newValue as Texture2D);
                    imageFieldsContainer.Add(textureField);
                }
            }

            var countSlider = new SliderInt("Número de imágenes", 1, 16) { value = atlasImageCount };
            countSlider.RegisterValueChangedCallback(evt =>
            {
                atlasImageCount = evt.newValue;
                RefreshImageFields();
            });
            foldout.Add(countSlider);
            foldout.Add(imageFieldsContainer);
            RefreshImageFields();

            var resolutionChoices = new List<int>(atlasResolutionSizes);
            var resolutionField = new PopupField<int>("Resolución por imagen", resolutionChoices, atlasCellResolution, v => v.ToString(), v => v.ToString());
            resolutionField.RegisterValueChangedCallback(evt => atlasCellResolution = evt.newValue);
            foldout.Add(resolutionField);

            var nameField = new TextField("Nombre del atlas") { value = atlasOutputName, style = { marginTop = 4 } };
            nameField.RegisterValueChangedCallback(evt => atlasOutputName = evt.newValue);
            foldout.Add(nameField);

            var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = atlasOutputFolder };
            folderField.RegisterValueChangedCallback(evt => atlasOutputFolder = evt.newValue as DefaultAsset);
            foldout.Add(folderField);

            var buttonsRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            buttonsRow.Add(new MTUIActionButton("Generar atlas", () =>
            {
                GenerateAtlasTexture();
                refresh();
            }));

            var saveButton = new MTUIActionButton("Guardar como textura", () =>
            {
                SaveGeneratedAtlas();
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            saveButton.style.marginLeft = 6;
            saveButton.SetAvailable(generatedAtlas != null);
            buttonsRow.Add(saveButton);
            foldout.Add(buttonsRow);

            if (generatedAtlas != null)
            {
                foldout.Add(new Image { image = generatedAtlas, scaleMode = ScaleMode.ScaleToFit, style = { height = 140, marginTop = 4 } });
                foldout.Add(new Label($"Atlas generado: {generatedAtlas.width}x{generatedAtlas.height}") { style = { fontSize = 10 } });
            }

            panel.Add(foldout);
            return panel;
        }

        private static VisualElement BuildAtlasInUsePanel(Action refresh)
        {
            var panel = new MTUIPanel("Atlas en uso") { style = { marginTop = 6 } };

            var atlasField = new ObjectField("Textura de referencia") { objectType = typeof(Texture2D), allowSceneObjects = false, value = atlasTexture };
            atlasField.RegisterValueChangedCallback(evt =>
            {
                atlasTexture = evt.newValue as Texture2D;
                refresh();
            });
            panel.Add(atlasField);

            if (atlasTexture == null)
            {
                panel.Add(new HelpBox("Genera un atlas arriba o asigna uno existente para usarlo de fondo en la vista previa.", HelpBoxMessageType.Info));
            }
            else
            {
                panel.Add(new MTUIInfoLabel($"Dimensiones: {atlasTexture.width}x{atlasTexture.height}"));
            }

            return panel;
        }

        private static VisualElement BuildTargetListPanel(Action refresh)
        {
            var panel = new MTUIPanel("Mallas clonadas") { style = { marginTop = 6 } };
            panel.Add(new MTUIInfoLabel(
                "Arrastra aquí tu VAT Single Mesh original (el GameObject con SkinnedMeshRenderer o MeshFilter) y genera automáticamente tantos clones como imágenes tenga el atlas de arriba. Las UV0 se editan sobre los clones, nunca sobre el original."));

            var sourceField = new ObjectField("Malla original") { objectType = typeof(GameObject), allowSceneObjects = true, value = cloneSourceObject };
            sourceField.RegisterValueChangedCallback(evt => cloneSourceObject = evt.newValue as GameObject);
            panel.Add(sourceField);

            EnsureAtlasSourceListSize();
            var generateButton = new MTUIActionButton($"Generar {atlasImageCount} clon(es)", () =>
            {
                GenerateClonesFromSource();
                refresh();
            });
            generateButton.style.marginTop = 4;
            generateButton.SetAvailable(cloneSourceObject != null);
            panel.Add(generateButton);

            var addSelectionButton = new MTUIActionButton("Añadir selección (mallas ya existentes)", () =>
            {
                foreach (GameObject go in Selection.gameObjects)
                {
                    MeshFilter filter = go != null ? go.GetComponent<MeshFilter>() : null;
                    if (filter != null)
                    {
                        AddTarget(filter, ownsMesh: false);
                    }
                }
                refresh();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            addSelectionButton.style.marginTop = 4;
            panel.Add(addSelectionButton);

            if (targets.Count == 0)
            {
                panel.Add(new HelpBox("No hay mallas objetivo todavía.", HelpBoxMessageType.Warning));
            }

            return panel;
        }

        private static bool TryResolveCloneSource(GameObject source, out Mesh mesh, out Material material, out string displayName)
        {
            mesh = null;
            material = null;
            displayName = null;

            if (source == null)
            {
                return false;
            }

            SkinnedMeshRenderer skin = source.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin != null && skin.sharedMesh != null)
            {
                mesh = skin.sharedMesh;
                material = skin.sharedMaterial;
                displayName = source.name;
                return true;
            }

            MeshFilter filter = source.GetComponentInChildren<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                material = renderer != null ? renderer.sharedMaterial : null;
                displayName = source.name;
                return true;
            }

            return false;
        }

        private static void GenerateClonesFromSource()
        {
            if (!TryResolveCloneSource(cloneSourceObject, out Mesh sourceMesh, out Material sourceMaterial, out string displayName))
            {
                SetStatus("Selecciona un GameObject con SkinnedMeshRenderer o MeshFilter para generar los clones.", HelpBoxMessageType.Warning);
                return;
            }

            if (sourceMesh.uv == null || sourceMesh.uv.Length == 0)
            {
                SetStatus($"'{displayName}' no tiene coordenadas UV0 que editar.", HelpBoxMessageType.Warning);
                return;
            }

            EnsureAtlasSourceListSize();
            int count = Mathf.Max(1, atlasImageCount);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Generar clones VAT UV Visual");

            var containerGO = new GameObject(displayName + "_VATClones");
            Undo.RegisterCreatedObjectUndo(containerGO, "Generar clones VAT UV Visual");

            for (int i = 0; i < count; i++)
            {
                Mesh clone = UnityEngine.Object.Instantiate(sourceMesh);
                clone.name = $"{displayName}_Clone_{i + 1}_UVEdit";

                var cloneGO = new GameObject($"{displayName}_Clone_{i + 1}");
                Undo.RegisterCreatedObjectUndo(cloneGO, "Generar clones VAT UV Visual");
                cloneGO.transform.SetParent(containerGO.transform, false);

                MeshFilter cloneFilter = cloneGO.AddComponent<MeshFilter>();
                cloneFilter.sharedMesh = clone;
                MeshRenderer cloneRenderer = cloneGO.AddComponent<MeshRenderer>();
                if (sourceMaterial != null)
                {
                    cloneRenderer.sharedMaterial = sourceMaterial;
                }

                // El mesh ya es una copia recién instanciada (no un asset compartido), así que
                // se marca ownsMesh: true para no duplicarlo de nuevo la primera vez que se
                // edite su UV.
                AddTarget(cloneFilter, ownsMesh: true);
            }

            Undo.CollapseUndoOperations(undoGroup);

            Selection.activeGameObject = containerGO;
            SetStatus($"{count} clon(es) generado(s) a partir de '{displayName}'.", HelpBoxMessageType.Info);
        }

        private static void AddTarget(MeshFilter filter, bool ownsMesh)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            if (uv == null || uv.Length == 0 || triangles == null || triangles.Length == 0)
            {
                SetStatus($"'{filter.name}' no tiene UV0 o triángulos válidos.", HelpBoxMessageType.Warning);
                return;
            }

            foreach (UvTargetEntry existing in targets)
            {
                if (existing.Filter == filter)
                {
                    return;
                }
            }

            var entry = new UvTargetEntry
            {
                DisplayName = filter.name,
                Filter = filter,
                Mesh = mesh,
                InitialUvs = (Vector2[])uv.Clone(),
                Uvs = (Vector2[])uv.Clone(),
                Triangles = (int[])triangles.Clone(),
                OwnsMesh = ownsMesh
            };

            Color baseColor = previewPalette[targets.Count % previewPalette.Length];
            entry.FillColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.22f);
            entry.OutlineColor = Color.Lerp(baseColor, Color.white, 0.2f);

            targets.Add(entry);
            if (activeIndex < 0)
            {
                activeIndex = 0;
            }
        }

        private static void EnsureAtlasSourceListSize()
        {
            if (atlasImageCount < 1)
            {
                atlasImageCount = 1;
            }

            while (atlasSourceTextures.Count < atlasImageCount)
            {
                atlasSourceTextures.Add(null);
            }

            while (atlasSourceTextures.Count > atlasImageCount)
            {
                atlasSourceTextures.RemoveAt(atlasSourceTextures.Count - 1);
            }
        }

        private static void GenerateAtlasTexture()
        {
            EnsureAtlasSourceListSize();

            if (atlasSourceTextures.Count == 0)
            {
                SetStatus("Asigna al menos una imagen para generar el atlas.", HelpBoxMessageType.Warning);
                return;
            }

            var readableCopies = new List<Texture2D>();
            var sources = new List<Texture2D>(atlasSourceTextures.Count);

            try
            {
                foreach (Texture2D source in atlasSourceTextures)
                {
                    if (source == null)
                    {
                        SetStatus("Todos los espacios de imagen deben estar asignados antes de generar el atlas.", HelpBoxMessageType.Warning);
                        return;
                    }

                    Texture2D readable = source;
                    if (!source.isReadable)
                    {
                        readable = CreateReadableCopy(source);
                        readableCopies.Add(readable);
                    }
                    sources.Add(readable);
                }

                int cellResolution = Mathf.Max(1, atlasCellResolution);
                int textureCount = sources.Count;
                int columns = Mathf.CeilToInt(Mathf.Sqrt(textureCount));
                int rows = Mathf.CeilToInt((float)textureCount / columns);
                int atlasWidth = Mathf.Max(1, columns * cellResolution);
                int atlasHeight = Mathf.Max(1, rows * cellResolution);

                var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false)
                {
                    name = atlasOutputName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var clearPixels = new Color32[atlasWidth * atlasHeight];
                for (int i = 0; i < clearPixels.Length; i++)
                {
                    clearPixels[i] = new Color32(0, 0, 0, 0);
                }
                atlas.SetPixels32(clearPixels);

                for (int index = 0; index < textureCount; index++)
                {
                    int column = index % columns;
                    int row = index / columns;
                    CopyTextureToAtlas(sources[index], atlas, column * cellResolution, row * cellResolution, cellResolution);
                }

                atlas.Apply();

                if (generatedAtlas != null)
                {
                    UnityEngine.Object.DestroyImmediate(generatedAtlas);
                }

                generatedAtlas = atlas;
                atlasTexture = generatedAtlas;
                SetStatus($"Atlas generado correctamente ({atlas.width}x{atlas.height}).", HelpBoxMessageType.Info);
            }
            finally
            {
                foreach (Texture2D copy in readableCopies)
                {
                    if (copy != null && copy != generatedAtlas)
                    {
                        UnityEngine.Object.DestroyImmediate(copy);
                    }
                }
            }
        }

        private static void CopyTextureToAtlas(Texture2D source, Texture2D atlas, int offsetX, int offsetY, int targetResolution)
        {
            int maxX = Mathf.Min(targetResolution, atlas.width - offsetX);
            int maxY = Mathf.Min(targetResolution, atlas.height - offsetY);

            for (int y = 0; y < maxY; y++)
            {
                float v = targetResolution > 1 ? (float)y / (targetResolution - 1) : 0f;
                for (int x = 0; x < maxX; x++)
                {
                    float u = targetResolution > 1 ? (float)x / (targetResolution - 1) : 0f;
                    Color sampled = source.GetPixelBilinear(u, v);
                    atlas.SetPixel(offsetX + x, offsetY + y, sampled);
                }
            }
        }

        private static void SaveGeneratedAtlas()
        {
            if (generatedAtlas == null)
            {
                SetStatus("No hay un atlas generado para guardar.", HelpBoxMessageType.Warning);
                return;
            }

            string outputPath = atlasOutputFolder != null ? AssetDatabase.GetAssetPath(atlasOutputFolder) : DefaultOutputPath;
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            byte[] pngData = generatedAtlas.EncodeToPNG();
            string safeName = string.IsNullOrWhiteSpace(atlasOutputName) ? "VAT_UV_Atlas" : atlasOutputName;
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, safeName + ".png").Replace("\\", "/"));
            File.WriteAllBytes(assetPath, pngData);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();

            atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            SetStatus($"Atlas guardado en '{assetPath}'.", HelpBoxMessageType.Info);
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            // RenderTextureReadWrite.Linear fuerza el render texture a espacio lineal, lo que
            // gamma-decodifica la textura de origen (normalmente sRGB) sin volver a codificarla
            // al escribir: el resultado se veía visiblemente más claro/lavado que el original.
            // Default respeta el espacio de color de la textura de origen y copia sin alterar el color.
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            return copy;
        }

        private static bool HasValidPreviewData()
        {
            foreach (UvTargetEntry entry in targets)
            {
                if (entry != null && entry.Visible && entry.Uvs != null && entry.Uvs.Length > 0 && entry.Triangles != null && entry.Triangles.Length >= 3)
                {
                    return true;
                }
            }

            return false;
        }

        private static UvTargetEntry GetActiveEntry()
        {
            if (activeIndex < 0 || activeIndex >= targets.Count)
            {
                return null;
            }

            UvTargetEntry entry = targets[activeIndex];
            return entry != null && entry.Mesh != null && entry.Uvs != null ? entry : null;
        }

        private static void SetActiveIndex(int index)
        {
            if (index < 0 || index >= targets.Count)
            {
                activeIndex = -1;
            }
            else
            {
                activeIndex = index;
            }

            isDragging = false;
            dragEntryIndex = -1;
        }

        private static void ResetActiveTransform()
        {
            UvTargetEntry entry = GetActiveEntry();
            if (entry == null)
            {
                return;
            }

            entry.TransformPosition = Vector2.zero;
            entry.TransformScale = Vector2.one;
            entry.TransformRotation = 0f;
            isDragging = false;
            dragEntryIndex = -1;
        }

        private static void EnsureEditableMesh(UvTargetEntry entry)
        {
            // filter.sharedMesh es el asset del proyecto (el bake VAT Single Mesh original).
            // Escribir UVs directamente sobre él corrompería cualquier otro clon/objeto que lo
            // comparta. La primera vez que se edita este entry, se duplica y el MeshFilter pasa
            // a apuntar a la copia; el resto de operaciones de este entry trabajan sobre la copia.
            if (entry.OwnsMesh || entry.Filter == null || entry.Mesh == null)
            {
                return;
            }

            Mesh duplicate = UnityEngine.Object.Instantiate(entry.Mesh);
            duplicate.name = entry.Mesh.name + "_UVEdit";
            Undo.RegisterCreatedObjectUndo(duplicate, "Duplicar mesh para edición de UV");
            Undo.RecordObject(entry.Filter, "Asignar mesh editable");
            entry.Filter.sharedMesh = duplicate;
            entry.Mesh = duplicate;
            entry.OwnsMesh = true;
        }

        private static void ApplyActiveTransform()
        {
            UvTargetEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("Selecciona una malla válida antes de aplicar cambios.", HelpBoxMessageType.Warning);
                return;
            }

            EnsureEditableMesh(entry);
            Mesh mesh = entry.Mesh;
            if (mesh == null || entry.Uvs == null || entry.Uvs.Length == 0)
            {
                SetStatus("La malla activa no contiene coordenadas UV para modificar.", HelpBoxMessageType.Warning);
                return;
            }

            Matrix4x4 transformMatrix = Matrix4x4.TRS(entry.TransformPosition, Quaternion.Euler(0f, 0f, entry.TransformRotation), new Vector3(entry.TransformScale.x, entry.TransformScale.y, 1f));
            Vector2[] transformed = new Vector2[entry.Uvs.Length];
            for (int i = 0; i < entry.Uvs.Length; i++)
            {
                Vector3 result = transformMatrix.MultiplyPoint3x4(new Vector3(entry.Uvs[i].x, entry.Uvs[i].y, 0f));
                transformed[i] = new Vector2(result.x, result.y);
            }

            Undo.RecordObject(mesh, "Aplicar transformación UV");
            mesh.uv = transformed;
            EditorUtility.SetDirty(mesh);

            entry.Uvs = (Vector2[])transformed.Clone();
            entry.TransformPosition = Vector2.zero;
            entry.TransformScale = Vector2.one;
            entry.TransformRotation = 0f;
            isDragging = false;
            dragEntryIndex = -1;

            SetStatus($"UV aplicadas correctamente a '{entry.DisplayName}'.", HelpBoxMessageType.Info);
        }

        private static void RestoreActiveToOriginal()
        {
            UvTargetEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("Selecciona una malla válida antes de restaurar.", HelpBoxMessageType.Warning);
                return;
            }

            EnsureEditableMesh(entry);
            Mesh mesh = entry.Mesh;
            if (mesh == null || entry.InitialUvs == null || entry.InitialUvs.Length == 0)
            {
                SetStatus("No se encontraron UV originales almacenadas para esta malla.", HelpBoxMessageType.Warning);
                return;
            }

            Vector2[] restored = (Vector2[])entry.InitialUvs.Clone();
            Undo.RecordObject(mesh, "Restaurar UV originales");
            mesh.uv = restored;
            EditorUtility.SetDirty(mesh);

            entry.Uvs = (Vector2[])restored.Clone();
            entry.TransformPosition = Vector2.zero;
            entry.TransformScale = Vector2.one;
            entry.TransformRotation = 0f;
            isDragging = false;
            dragEntryIndex = -1;

            SetStatus($"UV originales restauradas en '{entry.DisplayName}'.", HelpBoxMessageType.Info);
        }

        private static void SetStatus(string message, HelpBoxMessageType type)
        {
            statusMessage = message;
            statusType = type;
        }

        private static void DrawPreviewBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color baseColor = EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);
            EditorGUI.DrawRect(rect, baseColor);

            Color border = new Color(0.25f, 0.6f, 1f, 0.85f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), border);
        }

        private static void DrawPreviewGrid(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.08f);

            const int lines = 8;
            for (int i = 1; i < lines; i++)
            {
                float t = rect.x + rect.width * (i / (float)lines);
                Handles.DrawLine(new Vector3(t, rect.y), new Vector3(t, rect.yMax));

                float y = rect.y + rect.height * (i / (float)lines);
                Handles.DrawLine(new Vector3(rect.x, y), new Vector3(rect.xMax, y));
            }

            Handles.color = previous;
            Handles.EndGUI();
        }

        private static void DrawPreview(Rect rect)
        {
            if (!HasValidPreviewData())
            {
                return;
            }

            UvTargetEntry activeEntry = GetActiveEntry();
            Matrix4x4 activeMatrix = activeEntry != null
                ? Matrix4x4.TRS(activeEntry.TransformPosition, Quaternion.Euler(0f, 0f, activeEntry.TransformRotation), new Vector3(activeEntry.TransformScale.x, activeEntry.TransformScale.y, 1f))
                : Matrix4x4.identity;

            HandlePreviewInput(rect, activeEntry, activeMatrix);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Handles.BeginGUI();
            Color previous = Handles.color;

            for (int i = 0; i < targets.Count; i++)
            {
                UvTargetEntry entry = targets[i];
                if (entry == null || !entry.Visible || entry.Uvs == null || entry.Triangles == null || entry.Triangles.Length < 3)
                {
                    continue;
                }

                Matrix4x4 entryMatrix = Matrix4x4.TRS(entry.TransformPosition, Quaternion.Euler(0f, 0f, entry.TransformRotation), new Vector3(entry.TransformScale.x, entry.TransformScale.y, 1f));

                Color fill = entry.FillColor;
                Color outline = entry.OutlineColor;

                if (activeEntry != null && entry != activeEntry)
                {
                    fill.a *= 0.35f;
                    outline = Color.Lerp(outline, new Color(0.35f, 0.35f, 0.35f, outline.a), 0.6f);
                }

                Handles.color = fill;

                for (int tri = 0; tri < entry.Triangles.Length; tri += 3)
                {
                    int idxA = entry.Triangles[tri];
                    int idxB = entry.Triangles[tri + 1];
                    int idxC = entry.Triangles[tri + 2];

                    if (!entry.IsValidIndex(idxA) || !entry.IsValidIndex(idxB) || !entry.IsValidIndex(idxC))
                    {
                        continue;
                    }

                    Vector3 transformedA = entryMatrix.MultiplyPoint3x4(new Vector3(entry.Uvs[idxA].x, entry.Uvs[idxA].y, 0f));
                    Vector3 transformedB = entryMatrix.MultiplyPoint3x4(new Vector3(entry.Uvs[idxB].x, entry.Uvs[idxB].y, 0f));
                    Vector3 transformedC = entryMatrix.MultiplyPoint3x4(new Vector3(entry.Uvs[idxC].x, entry.Uvs[idxC].y, 0f));

                    Vector2 a = UvToScreen(new Vector2(transformedA.x, transformedA.y), rect);
                    Vector2 b = UvToScreen(new Vector2(transformedB.x, transformedB.y), rect);
                    Vector2 c = UvToScreen(new Vector2(transformedC.x, transformedC.y), rect);

                    Handles.DrawAAConvexPolygon(a, b, c);
                    Handles.color = outline;
                    Handles.DrawAAPolyLine(2f, a, b, c, a);
                    Handles.color = fill;
                }
            }

            if (activeEntry != null)
            {
                Vector3 pivot = activeMatrix.MultiplyPoint3x4(Vector3.zero);
                Vector3 axisX = activeMatrix.MultiplyPoint3x4(new Vector3(0.2f, 0f, 0f));
                Vector3 axisY = activeMatrix.MultiplyPoint3x4(new Vector3(0f, 0.2f, 0f));

                Handles.color = new Color(1f, 0.35f, 0.35f, 0.9f);
                Handles.DrawAAPolyLine(3f, UvToScreen(new Vector2(pivot.x, pivot.y), rect), UvToScreen(new Vector2(axisX.x, axisX.y), rect));
                Handles.color = new Color(0.35f, 1f, 0.5f, 0.9f);
                Handles.DrawAAPolyLine(3f, UvToScreen(new Vector2(pivot.x, pivot.y), rect), UvToScreen(new Vector2(axisY.x, axisY.y), rect));
            }

            Handles.color = previous;
            Handles.EndGUI();
        }

        private static void HandlePreviewInput(Rect rect, UvTargetEntry activeEntry, Matrix4x4 previewMatrix)
        {
            Event e = Event.current;
            if (e == null || activeEntry == null)
            {
                return;
            }

            Vector2 mouseUv = ScreenToUv(e.mousePosition, rect);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && rect.Contains(e.mousePosition) && IsMouseNearUv(activeEntry, mouseUv, previewMatrix))
                    {
                        isDragging = true;
                        dragStartMouse = e.mousePosition;
                        dragStartPosition = activeEntry.TransformPosition;
                        dragEntryIndex = activeIndex;
                        GUI.FocusControl(null);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (isDragging && dragEntryIndex == activeIndex)
                    {
                        Vector2 deltaPixels = e.mousePosition - dragStartMouse;
                        Vector2 deltaUv = new Vector2(deltaPixels.x / rect.width, -deltaPixels.y / rect.height);
                        activeEntry.TransformPosition = dragStartPosition + deltaUv;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (isDragging && e.button == 0)
                    {
                        isDragging = false;
                        dragEntryIndex = -1;
                        e.Use();
                    }
                    break;
                case EventType.ScrollWheel:
                    if (isDragging && rect.Contains(e.mousePosition) && dragEntryIndex == activeIndex)
                    {
                        float scroll = -e.delta.y;
                        float scaleFactor = 1f + (scroll * 0.05f);

                        Vector2 newScale = activeEntry.TransformScale;
                        if (lockUniformScale)
                        {
                            newScale *= scaleFactor;
                        }
                        else
                        {
                            newScale.x *= scaleFactor;
                            newScale.y *= scaleFactor;
                        }

                        newScale.x = Mathf.Clamp(newScale.x, 0.01f, 100f);
                        newScale.y = Mathf.Clamp(newScale.y, 0.01f, 100f);
                        activeEntry.TransformScale = newScale;
                        e.Use();
                    }
                    break;
            }
        }

        private static bool IsMouseNearUv(UvTargetEntry entry, Vector2 mouseUv, Matrix4x4 previewMatrix)
        {
            if (entry == null || entry.Uvs == null)
            {
                return false;
            }

            const float threshold = 0.05f;
            for (int i = 0; i < entry.Uvs.Length; i++)
            {
                Vector3 transformed = previewMatrix.MultiplyPoint3x4(new Vector3(entry.Uvs[i].x, entry.Uvs[i].y, 0f));
                if (Vector2.Distance(new Vector2(transformed.x, transformed.y), mouseUv) < threshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 UvToScreen(Vector2 uv, Rect rect)
        {
            return new Vector2(rect.x + uv.x * rect.width, rect.y + (1f - uv.y) * rect.height);
        }

        private static Vector2 ScreenToUv(Vector2 screen, Rect rect)
        {
            float u = Mathf.InverseLerp(rect.x, rect.xMax, screen.x);
            float v = 1f - Mathf.InverseLerp(rect.y, rect.yMax, screen.y);
            return new Vector2(u, v);
        }
    }
}
