using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Optizone;
using VZ_Optizone;
using VZOptizone;
using JaimeCamachoDev.Multitool.Animation;
using JaimeCamachoDev.Multitool.Modeling;
using JaimeCamachoDev.Multitool.UI;

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
        private readonly Dictionary<string, Func<VisualElement>> toolBuilders = new();
        private readonly Dictionary<string, Action> toolActivations = new();
        private readonly Dictionary<string, Action> toolDeactivations = new();

        private Category currentCategory = Category.Modelado;
        private string searchQuery = string.Empty;
        private bool toolActive;
        private string activeTool = string.Empty;

        private VisualElement contentColumn;
        private ScrollView librarySection;
        private readonly Dictionary<Category, MTUIActionButton> categoryButtons = new();

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
        }

        private void InitializeData()
        {
            BuildCatalog();
            BuildDescriptions();
            BuildToolActions();
        }

        private void BuildCatalog()
        {
            categoryTools.Clear();

            categoryTools[Category.Modelado] = new List<string>
            {
                "Advanced mesh combiner",
                "Pivot mover & aligner",
                "Merge mesh and create atlas",
                "Remove not visible vertex",
                "Hollow shell",
                "Multi material Finder",
                "Multi material splitter",
                "Vertex ID Display",
                "Micro triangle detector",
                "Reset XForm"
            };

            categoryTools[Category.Animacion] = new List<string>
            {
                "Remove blendshapes",
                "Animation terminator",
                "Bake pose",
                "Combine animations/ors into one",
                "Transfer bone weight",
                "Alembic to VAT",
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

            toolDescriptions["Advanced mesh combiner"] = "Combina múltiples objetos estáticos o skinned en un único mesh optimizado.";
            toolDescriptions["Pivot mover & aligner"] = "Ajusta el pivote de uno o varios objetos con presets o gizmo interactivo.";
            toolDescriptions["Merge mesh and create atlas"] = "Combina mallas en una sola malla y optimiza sus materiales en un atlas listo para uso inmediato.";
            toolDescriptions["Remove not visible vertex"] = "Detecta y elimina caras completamente ocultas tras otra geometría para reducir el peso de tus modelos.";
            toolDescriptions["Hollow shell"] = "Genera una versión hueca del mesh para props o elementos ligeros.";
            toolDescriptions["Multi material Finder"] = "Detecta rápidamente los materiales utilizados por una malla.";
            toolDescriptions["Multi material splitter"] = "Separa una malla según los materiales asignados.";
            toolDescriptions["Vertex ID Display"] = "Visualiza IDs de vértice directamente en la escena para depurar.";
            toolDescriptions["Micro triangle detector"] = "Resalta los triángulos problemáticos que pueden generar artefactos.";
            toolDescriptions["Reset XForm"] = "Convierte la transformación actual en geometría para dejar el Transform en valores por defecto sin mover el objeto.";

            toolDescriptions["Remove blendshapes"] = "Elimina blendshapes innecesarios para aligerar tus modelos animados.";
            toolDescriptions["Animation terminator"] = "Recorta clips de animacin hasta un frame especfico.";
            toolDescriptions["Bake pose"] = "Aplica la pose actual de una malla skinneda a un mesh esttico.";
            toolDescriptions["Combine animations/ors into one"] = "Fusiona varias animaciones en un solo clip optimizado.";
            toolDescriptions["Transfer bone weight"] = "Transfiere pesos de hueso entre mallas con distinta topologa.";
            toolDescriptions["Alembic to VAT"] = "Convierte una secuencia Alembic en texturas VAT listas para shader y prefab.";
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
            toolBuilders.Clear();
            toolActivations.Clear();
            toolDeactivations.Clear();

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
            toolDrawers["Alembic to VAT"] = AlembicToVatTool.DrawTool;
            toolDrawers["VAT Baker from Animation Clip"] = AnimationClipTextureBakerTool.DrawTool;

            toolDrawers["Lightmap checker"] = LightmapCheckerTool.DrawTool;
            toolDrawers["Renamer"] = RenameTool.DrawTool;
            toolBuilders["Hollow shell"] = HollowShellMeshTool.CreateGUI;
            toolBuilders["Multi material Finder"] = MultiMaterialFinderTool.CreateGUI;
            toolBuilders["Multi material splitter"] = MultimaterialMeshSplitterTool.CreateGUI;
            toolDrawers["Merge mesh and create atlas"] = MeshAtlasBakerTool.DrawTool;
            toolBuilders["Generate mesh uv lightmaps"] = UV2GeneratorTool.CreateGUI;
            toolBuilders["Move UV inside grid"] = UVAdjusterTool.CreateGUI;
            toolBuilders["Vertex ID Display"] = VertexIDDisplayerTool.CreateGUI;
            toolBuilders["Micro triangle detector"] = MicroTrianglesDetectorTool.CreateGUI;
            toolDrawers["Advanced mesh combiner"] = MeshCombinerTool.DrawTool;
            toolDrawers["Pivot mover & aligner"] = PivotAdjusterTool.DrawTool;
            toolBuilders["Reset XForm"] = ResetTransformTool.CreateGUI;
            toolBuilders["Remove not visible vertex"] = VertexOptimizationTool.CreateGUI;
            toolBuilders["Recalculate Mesh Bounds"] = RecalculateMeshBoundsTool.CreateGUI;

            toolActivations["Recalculate Mesh Bounds"] = RecalculateMeshBoundsTool.EnableSceneView;
            toolDeactivations["Recalculate Mesh Bounds"] = RecalculateMeshBoundsTool.DisableSceneView;

            toolActivations["Micro triangle detector"] = MicroTrianglesDetectorTool.EnableSceneView;
            toolDeactivations["Micro triangle detector"] = MicroTrianglesDetectorTool.DisableSceneView;
            toolActivations["Pivot mover & aligner"] = PivotAdjusterTool.EnableSceneView;
            toolDeactivations["Pivot mover & aligner"] = PivotAdjusterTool.DisableSceneView;

            toolActivations["Vertex ID Display"] = VertexIDDisplayerTool.EnableSceneView;
            toolDeactivations["Vertex ID Display"] = VertexIDDisplayerTool.DisableSceneView;

            toolActivations["Remove not visible vertex"] = VertexOptimizationTool.EnableSceneView;
            toolDeactivations["Remove not visible vertex"] = VertexOptimizationTool.DisableSceneView;
            toolDeactivations["Alembic to VAT"] = AlembicToVatTool.ResetState;
        }

        // ------------------------------------------------------------------
        // UI Toolkit shell — cards/buttons match the look used elsewhere
        // (VzFolders' UI Toolkit windows). Tools are being migrated one by one
        // from IMGUI (DrawTool(), wrapped in an IMGUIContainer) to native UI
        // Toolkit (CreateGUI(), returning a VisualElement tree directly) — see
        // toolBuilders vs. the legacy toolDrawers fallback in BuildActiveToolView.
        // ------------------------------------------------------------------

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;

            rootVisualElement.Add(BuildHeader());

            var body = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(body);

            body.Add(BuildNavigation());

            contentColumn = new VisualElement { style = { flexGrow = 1, paddingLeft = 4, paddingRight = 10, paddingTop = 6 } };
            body.Add(contentColumn);

            RefreshContent();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement
            {
                style =
                {
                    backgroundColor = MTUIColors.HeaderBackground,
                    paddingLeft = 18,
                    paddingRight = 18,
                    paddingTop = 14,
                    paddingBottom = 14
                }
            };

            var title = new Label("JaimeCamachoDev Multitool");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            header.Add(title);

            var subtitle = new MTUIInfoLabel("Una evolución de VZ Optizone con un flujo de trabajo más claro y unificado.");
            subtitle.style.marginBottom = 0;
            subtitle.style.marginTop = 4;
            header.Add(subtitle);

            return header;
        }

        private VisualElement BuildNavigation()
        {
            var panel = new MTUIPanel("Categorías");
            panel.style.width = 220;
            panel.style.flexShrink = 0;
            panel.style.flexGrow = 0;

            categoryButtons.Clear();
            AddCategoryButton(panel, Category.Modelado, "Modelado");
            AddCategoryButton(panel, Category.Animacion, "Animación");
            AddCategoryButton(panel, Category.Texturas, "Texturas");
            AddCategoryButton(panel, Category.Iluminacion, "Iluminación");
            AddCategoryButton(panel, Category.Miscelanea, "Miscelánea");

            RefreshCategorySelection();

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            panel.Add(spacer);

            panel.Add(new MTUIInfoLabel("Las herramientas que evolucionaron desde VZ Optizone conviven aquí organizadas por flujo de trabajo."));

            return panel;
        }

        private void AddCategoryButton(VisualElement parent, Category category, string label)
        {
            var button = new MTUIActionButton(label, () =>
            {
                currentCategory = category;
                toolActive = false;
                activeTool = string.Empty;
                RefreshCategorySelection();
                RefreshContent();
            }, alignment: TextAnchor.MiddleLeft);

            categoryButtons[category] = button;
            parent.Add(button);
        }

        private void RefreshCategorySelection()
        {
            foreach (var kvp in categoryButtons)
            {
                bool selected = kvp.Key == currentCategory;
                kvp.Value.SetColors(
                    selected ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                    selected ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                    selected ? Color.white : MTUIColors.NeutralText
                );
            }
        }

        private void RefreshContent()
        {
            contentColumn.Clear();
            contentColumn.Add(!toolActive ? BuildToolLibrary() : BuildActiveToolView());
        }

        private VisualElement BuildToolLibrary()
        {
            var container = new VisualElement { style = { flexGrow = 1 } };

            container.Add(BuildSearchBar());

            librarySection = new ScrollView { style = { flexGrow = 1 } };
            container.Add(librarySection);

            RefreshToolCards();

            return container;
        }

        private VisualElement BuildSearchBar()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            MTUIStyle.ApplyRoundedBox(row, 8);
            MTUIStyle.ApplyPadding(row, 6, 9);
            row.style.backgroundColor = MTUIColors.PanelBackground;
            MTUIStyle.ApplyBorderColor(row, MTUIColors.NeutralBorder);

            var label = new Label("Buscar");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = 60;
            row.Add(label);

            var searchField = new TextField { value = searchQuery };
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchQuery = evt.newValue;
                RefreshToolCards();
            });
            row.Add(searchField);

            var clearButton = new MTUIActionButton("Limpiar", () =>
            {
                searchQuery = string.Empty;
                searchField.SetValueWithoutNotify(string.Empty);
                RefreshToolCards();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText);
            clearButton.style.marginLeft = 6;
            row.Add(clearButton);

            return row;
        }

        private void RefreshToolCards()
        {
            librarySection.Clear();

            var tools = categoryTools[currentCategory]
                .Where(t => string.IsNullOrEmpty(searchQuery) || t.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (tools.Count == 0)
            {
                var empty = new MTUIInfoLabel("No se encontraron herramientas que coincidan con la búsqueda actual.");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.marginTop = 24;
                librarySection.Add(empty);
                return;
            }

            foreach (string toolName in tools)
                librarySection.Add(BuildToolCard(toolName));
        }

        private VisualElement BuildToolCard(string toolName)
        {
            bool toolAvailable = toolDrawers.ContainsKey(toolName) || toolBuilders.ContainsKey(toolName);

            var card = new MTUIPanel(toolName);
            card.TitleLabel.style.fontSize = 13;

            if (toolDescriptions.TryGetValue(toolName, out string description) && !string.IsNullOrEmpty(description))
                card.Add(new MTUIInfoLabel(description));

            var openButton = new MTUIActionButton(
                toolAvailable ? "Abrir herramienta" : "Próximamente",
                () => { ActivateTool(toolName); RefreshContent(); }
            );
            if (!toolAvailable) openButton.SetAvailable(false);
            card.Add(openButton);

            if (!toolAvailable)
            {
                var warning = new MTUIInfoLabel("Esta herramienta aún no está disponible en la nueva Multitool.");
                warning.style.color = new Color(0.95f, 0.75f, 0.3f);
                card.Add(warning);
            }

            return card;
        }

        private VisualElement BuildActiveToolView()
        {
            var container = new VisualElement { style = { flexGrow = 1 } };

            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            MTUIStyle.ApplyRoundedBox(headerRow, 8);
            MTUIStyle.ApplyPadding(headerRow, 6, 9);
            headerRow.style.backgroundColor = MTUIColors.PanelBackground;
            MTUIStyle.ApplyBorderColor(headerRow, MTUIColors.NeutralBorder);

            headerRow.Add(new MTUIActionButton("← Volver a la biblioteca", () =>
            {
                DeactivateActiveTool();
                RefreshContent();
            }, MTUIColors.NeutralBackground, MTUIColors.NeutralBorder, MTUIColors.NeutralText));

            var titleLabel = new Label(activeTool);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 13;
            titleLabel.style.marginLeft = 10;
            headerRow.Add(titleLabel);

            container.Add(headerRow);

            var scroll = new ScrollView { style = { flexGrow = 1 } };

            if (toolBuilders.TryGetValue(activeTool, out Func<VisualElement> buildTool))
            {
                scroll.Add(buildTool.Invoke());
            }
            else if (toolDrawers.TryGetValue(activeTool, out Action drawAction))
            {
                scroll.Add(new IMGUIContainer(() => drawAction.Invoke()));
            }
            else
            {
                scroll.Add(new IMGUIContainer(() =>
                    EditorGUILayout.HelpBox("La herramienta seleccionada todavía no forma parte de la nueva experiencia Multitool.", MessageType.Warning)));
            }

            container.Add(scroll);

            return container;
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
        }
    }
}
