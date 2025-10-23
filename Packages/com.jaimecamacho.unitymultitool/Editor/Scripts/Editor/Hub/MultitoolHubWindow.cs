using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JaimeCamachoDev.Multitool.Modeling;
using OptiZone;
using Optizone;
using VZ_Optizone;
using VZOptizone;

namespace JaimeCamachoDev.Multitool
{
    public class MultitoolHubWindow : EditorWindow
    {
        private enum Category
        {
            Modelado,
            Animacion,
            Texturas,
            Iluminacion,
            Miscelanea
        }

        private class ToolDefinition
        {
            public Category Category;
            public string Name;
            public string Description;
            public Action Drawer;
            public Action Activate;
            public Action Deactivate;
            public bool IsNew;

            public bool IsImplemented => Drawer != null;
        }

        private static string pendingToolActivation;
        private readonly List<ToolDefinition> tools = new();
        private Category currentCategory = Category.Modelado;
        private Vector2 libraryScrollPosition;
        private Vector2 toolScrollPosition;
        private string searchQuery = string.Empty;
        private ToolDefinition activeTool;

        private GUIStyle headerTitleStyle;
        private GUIStyle headerSubtitleStyle;
        private GUIStyle navigationContainerStyle;
        private GUIStyle navigationTitleStyle;
        private GUIStyle categoryButtonStyle;
        private GUIStyle selectedCategoryButtonStyle;
        private GUIStyle searchFieldStyle;
        private GUIStyle toolCardStyle;
        private GUIStyle toolCardTitleStyle;
        private GUIStyle toolCardDescriptionStyle;
        private GUIStyle openToolButtonStyle;
        private GUIStyle newBadgeStyle;
        private GUIStyle emptyStateLabelStyle;

        private Texture2D selectedCategoryBackground;

        [MenuItem("Tools/JaimeCamachoDev/Multitool/Open Hub")]
        public static void ShowWindow()
        {
            MultitoolHubWindow window = GetWindow<MultitoolHubWindow>("Multitool");
            window.minSize = new Vector2(780f, 500f);
            window.InitializeData();
        }

        public static void ShowTool(string toolName)
        {
            pendingToolActivation = toolName;
            ShowWindow();
        }

        [MenuItem("Tools/JaimeCamachoDev/Multitool/Modelado/Advanced Mesh Combiner", priority = 10)]
        public static void OpenAdvancedMeshCombiner()
        {
            ShowTool("Advanced Mesh Combiner");
        }

        [MenuItem("Tools/JaimeCamachoDev/Multitool/Modelado/Pivot mover & aligner", priority = 11)]
        public static void OpenPivotMover()
        {
            ShowTool("Pivot mover & aligner");
        }

        private void OnEnable()
        {
            InitializeData();
        }

        private void OnDisable()
        {
            DeactivateActiveTool();
            DestroyTextures();
        }

        private void InitializeData()
        {
            BuildCatalog();

            if (!string.IsNullOrEmpty(pendingToolActivation))
            {
                ToolDefinition pending = tools.FirstOrDefault(t => string.Equals(t.Name, pendingToolActivation, StringComparison.OrdinalIgnoreCase));
                if (pending != null)
                {
                    currentCategory = pending.Category;
                    ActivateTool(pending);
                }

                pendingToolActivation = null;
            }

            InitializeStyles();
        }

        
        private void BuildCatalog()
        {
            tools.Clear();

            RegisterTool(Category.Modelado, "Advanced Mesh Combiner",
                "Fusiona múltiples MeshRenderers y SkinnedMeshRenderers en un mesh optimizado agrupando materiales, creando colliders opcionales y guardando assets listos para VR.",
                MeshCombinerTool.DrawTool,
                isNew: true);

            RegisterTool(Category.Modelado, "Pivot mover & aligner",
                "Reposiciona el pivote con asas en escena, presets de anclaje y preservación de hijos/colliders para iterar props rápido.",
                PivotAdjusterTool.DrawTool,
                PivotAdjusterTool.EnableSceneView,
                PivotAdjusterTool.DisableSceneView,
                isNew: true);

            RegisterTool(Category.Modelado, "Remove not visible vertex",
                "Limpia vértices ocultos para reducir el peso de tus modelos (en desarrollo).");

            RegisterTool(Category.Modelado, "Hollow shell",
                "Genera una versión hueca del mesh para props o elementos ligeros.",
                HollowShellMeshTool.DrawTool);

            RegisterTool(Category.Modelado, "Multi material Finder",
                "Detecta rápidamente los materiales utilizados por una malla.",
                MultiMaterialFinderTool.DrawTool);

            RegisterTool(Category.Modelado, "Multi material splitter",
                "Separa una malla según los materiales asignados.",
                MultimaterialMeshSplitterTool.DrawTool);

            RegisterTool(Category.Modelado, "Vertex ID Display",
                "Visualiza IDs de vértice directamente en la escena para depurar.",
                VertexIDDisplayerTool.DrawTool);

            RegisterTool(Category.Modelado, "Micro triangle detector",
                "Resalta los triángulos problemáticos que pueden generar artefactos.",
                MicroTrianglesDetectorTool.DrawTool,
                MicroTrianglesDetectorTool.EnableSceneView,
                MicroTrianglesDetectorTool.DisableSceneView);

            RegisterTool(Category.Animacion, "Remove blendshapes",
                "Elimina blendshapes innecesarios para aligerar tus modelos animados.",
                BlendshapeRemovalTool.DrawTool);

            RegisterTool(Category.Animacion, "Animation terminator",
                "Recorta clips de animación hasta un frame específico.",
                AnimationTerminatorTool.DrawTool);

            RegisterTool(Category.Animacion, "Bake pose",
                "Aplica la pose actual de una malla skinneda a un mesh estático.",
                BakeMeshTool.DrawTool);

            RegisterTool(Category.Animacion, "Combine animations/ors into one",
                "Fusiona varias animaciones en un solo clip optimizado.",
                CombineAnimationsWithPathsTool.DrawTool);

            RegisterTool(Category.Animacion, "Transfer bone weight",
                "Transfiere pesos de hueso entre mallas con distinta topología.",
                BoneWeightTransferTool.DrawTool);

            RegisterTool(Category.Animacion, "VAT Baker from Animation Clip",
                "Genera texturas VAT a partir de un clip de animación.",
                AnimationClipTextureBakerTool.DrawTool);

            RegisterTool(Category.Animacion, "VAT All in One",
                "Paquete completo de herramientas VAT (en desarrollo).");

            RegisterTool(Category.Texturas, "Convert Asset to Image",
                "Convierte assets de texturas en imágenes y viceversa.",
                AssetToImageConverterTool.DrawTool);

            RegisterTool(Category.Texturas, "Split texture into channels",
                "Extrae canales RGBA independientes utilizando ffmpeg.",
                ImageChannelSplitterTool.DrawTool);

            RegisterTool(Category.Texturas, "Merge textures into one",
                "Combina cuatro texturas en un solo mapa RGBA.",
                ImageChannelMergerTool.DrawTool);

            RegisterTool(Category.Texturas, "Extract frames from video",
                "Exporta fotogramas individuales a partir de un vídeo.",
                VideoToFramesExtractorTool.DrawTool);

            RegisterTool(Category.Texturas, "Convert sprites to animation clip",
                "Genera clips de animación a partir de sprites 2D.",
                UIAnimationClipGeneratorTool.DrawTool);

            RegisterTool(Category.Iluminacion, "Generate mesh uv lightmaps",
                "Crea coordenadas UV2 automáticas listas para bake de luz.",
                UV2GeneratorTool.DrawTool);

            RegisterTool(Category.Iluminacion, "Move UV inside grid",
                "Ajusta UVs para mantenerlos dentro del tile principal.",
                UVAdjusterToolOpti.DrawTool);

            RegisterTool(Category.Iluminacion, "Lightmap checker",
                "Inspecciona y visualiza lightmaps en la escena actual.",
                LightmapCheckerTool.DrawTool);

            RegisterTool(Category.Iluminacion, "Recalculate Mesh Bounds",
                "Ajusta los bounds de tus meshes para mejorar el culling.",
                () =>
                {
                    if (Selection.activeGameObject != null)
                    {
                        MeshFilter meshFilter = Selection.activeGameObject.GetComponent<MeshFilter>();
                        if (meshFilter != null)
                        {
                            RecalculateMeshBoundsTool.SetTarget(meshFilter);
                        }
                    }

                    RecalculateMeshBoundsTool.DrawTool();
                },
                RecalculateMeshBoundsTool.EnableSceneView,
                RecalculateMeshBoundsTool.DisableSceneView);

            RegisterTool(Category.Miscelanea, "Renamer",
                "Renombra objetos y assets en bloque con reglas flexibles.",
                RenameTool.DrawTool);
        }

        private void RegisterTool(Category category, string name, string description, Action drawer = null, Action activate = null, Action deactivate = null, bool isNew = false)
        {
            tools.Add(new ToolDefinition
            {
                Category = category,
                Name = name,
                Description = description,
                Drawer = drawer,
                Activate = activate,
                Deactivate = deactivate,
                IsNew = isNew
            });
        }


        

        

        private void InitializeStyles()
        {
            if (headerTitleStyle != null)
            {
                return;
            }

            bool isProSkin = EditorGUIUtility.isProSkin;

            headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 22,
                normal = { textColor = isProSkin ? Color.white : new Color(0.13f, 0.15f, 0.18f) }
            };

            headerSubtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = isProSkin ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.32f, 0.36f, 0.44f) }
            };

            navigationContainerStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(14, 14, 16, 16),
                margin = new RectOffset(4, 8, 8, 8)
            };

            navigationTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = isProSkin ? Color.white : new Color(0.13f, 0.15f, 0.18f) }
            };

            categoryButtonStyle = new GUIStyle("Button")
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fixedHeight = 32,
                padding = new RectOffset(12, 12, 6, 6)
            };

            selectedCategoryBackground = CreateColorTexture(new Color(0.27f, 0.43f, 0.89f, isProSkin ? 0.9f : 0.8f));

            selectedCategoryButtonStyle = new GUIStyle(categoryButtonStyle)
            {
                normal =
                {
                    background = selectedCategoryBackground,
                    textColor = Color.white
                },
                hover =
                {
                    background = selectedCategoryBackground,
                    textColor = Color.white
                },
                active =
                {
                    background = selectedCategoryBackground,
                    textColor = Color.white
                }
            };

            searchFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12
            };

            toolCardStyle = new GUIStyle("HelpBox")
            {
                padding = new RectOffset(16, 16, 14, 14),
                margin = new RectOffset(4, 4, 6, 10)
            };

            toolCardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                wordWrap = true
            };

            toolCardDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 11,
                normal = { textColor = isProSkin ? new Color(0.78f, 0.82f, 0.89f) : new Color(0.32f, 0.36f, 0.44f) }
            };

            openToolButtonStyle = new GUIStyle("Button")
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fixedHeight = 24
            };

            newBadgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = isProSkin ? new Color(0.33f, 0.74f, 1f) : new Color(0.1f, 0.37f, 0.74f) }
            };

            emptyStateLabelStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isProSkin ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.32f, 0.36f, 0.44f) }
            };
        }

        private void DestroyTextures()
        {
            if (selectedCategoryBackground != null)
            {
                DestroyImmediate(selectedCategoryBackground);
                selectedCategoryBackground = null;
            }

            headerTitleStyle = null;
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawHeader();

            GUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNavigation();

                GUILayout.Space(10f);

                GUILayout.BeginVertical();
                {
                    if (activeTool == null)
                    {
                        DrawToolLibrary();
                    }
                    else
                    {
                        DrawActiveTool();
                    }
                }
                GUILayout.EndVertical();
            }
        }

        private void DrawHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(position.width, 78f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerRect, new Color(0.14f, 0.16f, 0.22f));
            }

            Rect titleRect = new Rect(headerRect.x + 18f, headerRect.y + 16f, headerRect.width - 36f, 26f);
            GUI.Label(titleRect, "JaimeCamachoDev Multitool", headerTitleStyle);

            Rect subtitleRect = new Rect(headerRect.x + 18f, headerRect.y + 42f, headerRect.width - 36f, 20f);
            GUI.Label(subtitleRect, "Una evolucin de VZ Optizone con un flujo de trabajo ms claro y unificado.", headerSubtitleStyle);
        }

        private void DrawNavigation()
        {
            GUILayout.BeginVertical(navigationContainerStyle, GUILayout.Width(220f), GUILayout.ExpandHeight(true));
            GUILayout.Label("Categorías", navigationTitleStyle);
            GUILayout.Space(6f);

            DrawCategoryButton(Category.Modelado, "Modelado");
            DrawCategoryButton(Category.Animacion, "Animación");
            DrawCategoryButton(Category.Texturas, "Texturas");
            DrawCategoryButton(Category.Iluminacion, "Iluminación");
            DrawCategoryButton(Category.Miscelanea, "Miscelánea");

            GUILayout.FlexibleSpace();

            EditorGUILayout.HelpBox("Las herramientas que evolucionaron desde VZ Optizone conviven aquí organizadas por flujo de trabajo.", MessageType.Info);
            GUILayout.EndVertical();
        }

        private void DrawCategoryButton(Category category, string label)
        {
            bool isSelected = currentCategory == category;
            GUIStyle style = isSelected ? selectedCategoryButtonStyle : categoryButtonStyle;

            if (GUILayout.Button(label, style))
            {
                currentCategory = category;
                DeactivateActiveTool();
            }
        }

        private void DrawToolLibrary()
        {
            GUILayout.BeginVertical();
            DrawSearchBar();

            GUILayout.Space(4f);

            List<ToolDefinition> filteredTools = tools
                .Where(t => t.Category == currentCategory &&
                            (string.IsNullOrEmpty(searchQuery) || t.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            libraryScrollPosition = EditorGUILayout.BeginScrollView(libraryScrollPosition);

            if (filteredTools.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No se encontraron herramientas que coincidan con la búsqueda actual.", emptyStateLabelStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                foreach (ToolDefinition tool in filteredTools)
                {
                    DrawToolCard(tool);
                }
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Buscar", navigationTitleStyle, GUILayout.Width(60f));
                GUI.SetNextControlName("MultitoolSearchField");
                searchQuery = EditorGUILayout.TextField(searchQuery, searchFieldStyle);

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    if (GUILayout.Button("Limpiar", GUILayout.Width(70f)))
                    {
                        searchQuery = string.Empty;
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private void DrawToolCard(ToolDefinition tool)
        {
            bool toolAvailable = tool.IsImplemented;

            GUILayout.BeginVertical(toolCardStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(tool.Name, toolCardTitleStyle);
                GUILayout.FlexibleSpace();
                if (tool.IsNew)
                {
                    GUILayout.Label("Nuevo", newBadgeStyle ?? EditorStyles.miniBoldLabel, GUILayout.Width(60f));
                }
            }

            if (!string.IsNullOrEmpty(tool.Description))
            {
                GUILayout.Label(tool.Description, toolCardDescriptionStyle);
            }

            GUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(!toolAvailable))
            {
                string buttonLabel = toolAvailable ? "Abrir herramienta" : "Próximamente";
                if (GUILayout.Button(buttonLabel, openToolButtonStyle))
                {
                    ActivateTool(tool);
                }
            }

            if (!toolAvailable)
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox("Esta herramienta aún no está disponible en la nueva Multitool.", MessageType.Warning);
            }

            GUILayout.EndVertical();
        }

        private void ActivateTool(ToolDefinition tool)
        {
            if (activeTool == tool)
            {
                return;
            }

            DeactivateActiveTool();

            activeTool = tool;

            activeTool?.Activate?.Invoke();
            toolScrollPosition = Vector2.zero;
        }

        private void DrawActiveTool()
        {
            GUILayout.BeginVertical();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("← Volver a la biblioteca", GUILayout.Width(210f), GUILayout.Height(24f)))
                {
                    DeactivateActiveTool();
                    return;
                }

                GUILayout.Space(6f);
                GUILayout.Label(activeTool?.Name ?? string.Empty, toolCardTitleStyle);
                GUILayout.FlexibleSpace();
            }

            toolScrollPosition = EditorGUILayout.BeginScrollView(toolScrollPosition);

            if (!string.IsNullOrEmpty(activeTool?.Description))
            {
                EditorGUILayout.HelpBox(activeTool.Description, MessageType.Info);
                GUILayout.Space(6f);
            }

            if (activeTool?.Drawer != null)
            {
                activeTool.Drawer.Invoke();
            }
            else
            {
                EditorGUILayout.HelpBox("La herramienta seleccionada todavía no forma parte de la nueva experiencia Multitool.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DeactivateActiveTool()
        {
            if (activeTool == null)
            {
                return;
            }

            activeTool.Deactivate?.Invoke();
            activeTool = null;
            toolScrollPosition = Vector2.zero;
        }
    }
}
