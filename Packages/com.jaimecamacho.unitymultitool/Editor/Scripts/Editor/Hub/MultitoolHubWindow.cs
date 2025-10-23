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

        private readonly Dictionary<Category, List<string>> categoryTools = new();
        private readonly Dictionary<string, string> toolDescriptions = new();
        private readonly Dictionary<string, Action> toolDrawers = new();
        private readonly Dictionary<string, Action> toolActivations = new();
        private readonly Dictionary<string, Action> toolDeactivations = new();

        private Category currentCategory = Category.Modelado;
        private Vector2 libraryScrollPosition;
        private Vector2 toolScrollPosition;
        private string searchQuery = string.Empty;
        private bool toolActive;
        private string activeTool = string.Empty;

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
        private GUIStyle emptyStateLabelStyle;

        private Texture2D selectedCategoryBackground;

        [MenuItem("Tools/JaimeCamachoDev/Multitool/Open Hub")]
        public static void ShowWindow()
        {
            MultitoolHubWindow window = GetWindow<MultitoolHubWindow>("Multitool");
            window.minSize = new Vector2(780f, 500f);
            window.InitializeData();
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
            BuildDescriptions();
            BuildToolActions();
            InitializeStyles();
        }

        private void BuildCatalog()
        {
            categoryTools.Clear();

            categoryTools[Category.Modelado] = new List<string>
            {
                "Advanced Mesh Combiner",
                "Pivot mover & aligner",
                "Remove not visible vertex",
                "Hollow shell",
                "Multi material Finder",
                "Multi material splitter",
                "Vertex ID Display",
                "Micro triangle detector"
            };

            categoryTools[Category.Animacion] = new List<string>
            {
                "Remove blendshapes",
                "Animation terminator",
                "Bake pose",
                "Combine animations/ors into one",
                "Transfer bone weight",
                "VAT Baker from Animation Clip",
                "VAT All in One"
            };

            categoryTools[Category.Texturas] = new List<string>
            {
                "Convert Asset to Image",
                "Split texture into channels",
                "Merge textures into one",
                "Extract frames from video",
                "Convert sprites to animation clip"
            };

            categoryTools[Category.Iluminacion] = new List<string>
            {
                "Generate mesh uv lightmaps",
                "Move UV inside grid",
                "Lightmap checker",
                "Recalculate Mesh Bounds"
            };

            categoryTools[Category.Miscelanea] = new List<string>
            {
                "Renamer"
            };
        }

        private void BuildDescriptions()
        {
            toolDescriptions.Clear();

            toolDescriptions["Advanced Mesh Combiner"] = "Fusiona múltiples MeshRenderers y SkinnedMeshRenderers en un mesh optimizado agrupando materiales, creando colliders opcionales y guardando assets listos para VR.";
            toolDescriptions["Pivot mover & aligner"] = "Reposiciona el pivote con asas en escena, presets de anclaje y preservación de hijos/colliders para iterar props rápido.";
            toolDescriptions["Remove not visible vertex"] = "Limpia vrtices ocultos para reducir el peso de tus modelos (en desarrollo).";
            toolDescriptions["Hollow shell"] = "Genera una versin hueca del mesh para props o elementos ligeros.";
            toolDescriptions["Multi material Finder"] = "Detecta rpidamente los materiales utilizados por una malla.";
            toolDescriptions["Multi material splitter"] = "Separa una malla segn los materiales asignados.";
            toolDescriptions["Vertex ID Display"] = "Visualiza IDs de vrtice directamente en la escena para depurar.";
            toolDescriptions["Micro triangle detector"] = "Resalta los triángulos problemáticos que pueden generar artefactos.";

            toolDescriptions["Remove blendshapes"] = "Elimina blendshapes innecesarios para aligerar tus modelos animados.";
            toolDescriptions["Animation terminator"] = "Recorta clips de animacin hasta un frame especfico.";
            toolDescriptions["Bake pose"] = "Aplica la pose actual de una malla skinneda a un mesh esttico.";
            toolDescriptions["Combine animations/ors into one"] = "Fusiona varias animaciones en un solo clip optimizado.";
            toolDescriptions["Transfer bone weight"] = "Transfiere pesos de hueso entre mallas con distinta topologa.";
            toolDescriptions["VAT Baker from Animation Clip"] = "Genera texturas VAT a partir de un clip de animacin.";
            toolDescriptions["VAT All in One"] = "Paquete completo de herramientas VAT (en desarrollo).";

            toolDescriptions["Convert Asset to Image"] = "Convierte assets de texturas en imágenes y viceversa.";
            toolDescriptions["Split texture into channels"] = "Extrae canales RGBA independientes utilizando ffmpeg.";
            toolDescriptions["Merge textures into one"] = "Combina cuatro texturas en un solo mapa RGBA.";
            toolDescriptions["Extract frames from video"] = "Exporta fotogramas individuales a partir de un vdeo.";
            toolDescriptions["Convert sprites to animation clip"] = "Genera clips de animacin a partir de sprites 2D.";

            toolDescriptions["Generate mesh uv lightmaps"] = "Crea coordenadas UV2 automticas listas para bake de luz.";
            toolDescriptions["Move UV inside grid"] = "Ajusta UVs para mantenerlos dentro del tile principal.";
            toolDescriptions["Lightmap checker"] = "Inspecciona y visualiza lightmaps en la escena actual.";
            toolDescriptions["Recalculate Mesh Bounds"] = "Ajusta los bounds de tus meshes para mejorar el culling.";

            toolDescriptions["Renamer"] = "Renombra objetos y assets en bloque con reglas flexibles.";
        }

        private void BuildToolActions()
        {
            toolDrawers.Clear();
            toolActivations.Clear();
            toolDeactivations.Clear();

            toolDrawers["Advanced Mesh Combiner"] = MeshCombinerTool.DrawTool;
            toolDrawers["Pivot mover & aligner"] = PivotAdjusterTool.DrawTool;
            toolDrawers["Convert Asset to Image"] = AssetToImageConverterTool.DrawTool;
            toolDrawers["Split texture into channels"] = ImageChannelSplitterTool.DrawTool;
            toolDrawers["Merge textures into one"] = ImageChannelMergerTool.DrawTool;
            toolDrawers["Extract frames from video"] = VideoToFramesExtractorTool.DrawTool;
            toolDrawers["Convert sprites to animation clip"] = UIAnimationClipGeneratorTool.DrawTool;

            toolDrawers["Remove blendshapes"] = BlendshapeRemovalTool.DrawTool;
            toolDrawers["Animation terminator"] = AnimationTerminatorTool.DrawTool;
            toolDrawers["Bake pose"] = BakeMeshTool.DrawTool;
            toolDrawers["Combine animations/ors into one"] = CombineAnimationsWithPathsTool.DrawTool;
            toolDrawers["Transfer bone weight"] = BoneWeightTransferTool.DrawTool;
            toolDrawers["VAT Baker from Animation Clip"] = AnimationClipTextureBakerTool.DrawTool;

            toolDrawers["Lightmap checker"] = LightmapCheckerTool.DrawTool;
            toolDrawers["Renamer"] = RenameTool.DrawTool;
            toolDrawers["Hollow shell"] = HollowShellMeshTool.DrawTool;
            toolDrawers["Multi material Finder"] = MultiMaterialFinderTool.DrawTool;
            toolDrawers["Multi material splitter"] = MultimaterialMeshSplitterTool.DrawTool;
            toolDrawers["Generate mesh uv lightmaps"] = UV2GeneratorTool.DrawTool;
            toolDrawers["Move UV inside grid"] = UVAdjusterToolOpti.DrawTool;
            toolDrawers["Vertex ID Display"] = VertexIDDisplayerTool.DrawTool;
            toolDrawers["Micro triangle detector"] = MicroTrianglesDetectorTool.DrawTool;
            toolDrawers["Recalculate Mesh Bounds"] = () =>
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
            };

            toolActivations["Pivot mover & aligner"] = PivotAdjusterTool.EnableSceneView;
            toolDeactivations["Pivot mover & aligner"] = PivotAdjusterTool.DisableSceneView;

            toolActivations["Recalculate Mesh Bounds"] = RecalculateMeshBoundsTool.EnableSceneView;
            toolDeactivations["Recalculate Mesh Bounds"] = RecalculateMeshBoundsTool.DisableSceneView;

            toolActivations["Micro triangle detector"] = MicroTrianglesDetectorTool.EnableSceneView;
            toolDeactivations["Micro triangle detector"] = MicroTrianglesDetectorTool.DisableSceneView;
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
                    if (!toolActive)
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
                toolActive = false;
                activeTool = string.Empty;
            }
        }

        private void DrawToolLibrary()
        {
            GUILayout.BeginVertical();
            DrawSearchBar();

            GUILayout.Space(4f);

            List<string> tools = categoryTools[currentCategory]
                .Where(t => string.IsNullOrEmpty(searchQuery) || t.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            libraryScrollPosition = EditorGUILayout.BeginScrollView(libraryScrollPosition);

            if (tools.Count == 0)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("No se encontraron herramientas que coincidan con la búsqueda actual.", emptyStateLabelStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                foreach (string toolName in tools)
                {
                    DrawToolCard(toolName);
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

        private void DrawToolCard(string toolName)
        {
            bool toolAvailable = toolDrawers.ContainsKey(toolName);

            GUILayout.BeginVertical(toolCardStyle);
            GUILayout.Label(toolName, toolCardTitleStyle);

            if (toolDescriptions.TryGetValue(toolName, out string description) && !string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description, toolCardDescriptionStyle);
            }

            GUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(!toolAvailable))
            {
                string buttonLabel = toolAvailable ? "Abrir herramienta" : "Próximamente";
                if (GUILayout.Button(buttonLabel, openToolButtonStyle))
                {
                    ActivateTool(toolName);
                }
            }

            if (!toolAvailable)
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox("Esta herramienta aún no está disponible en la nueva Multitool.", MessageType.Warning);
            }

            GUILayout.EndVertical();
        }

        private void ActivateTool(string toolName)
        {
            if (toolActive && activeTool == toolName)
            {
                return;
            }

            DeactivateActiveTool();

            activeTool = toolName;
            toolActive = true;

            if (toolActivations.TryGetValue(toolName, out Action activate))
            {
                activate.Invoke();
            }
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
                GUILayout.Label(activeTool, toolCardTitleStyle);
                GUILayout.FlexibleSpace();
            }

            toolScrollPosition = EditorGUILayout.BeginScrollView(toolScrollPosition);

            if (toolDrawers.TryGetValue(activeTool, out Action drawAction))
            {
                drawAction.Invoke();
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
            if (!toolActive)
            {
                return;
            }

            if (toolDeactivations.TryGetValue(activeTool, out Action deactivate))
            {
                deactivate.Invoke();
            }

            toolActive = false;
            activeTool = string.Empty;
            toolScrollPosition = Vector2.zero;
        }
    }
}
