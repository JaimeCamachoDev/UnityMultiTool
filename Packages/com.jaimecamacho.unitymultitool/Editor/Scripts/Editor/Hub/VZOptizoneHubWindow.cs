using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using OptiZone;
using Optizone;
using VZ_Optizone;

namespace VZOptizone
{
    public class VZOptizoneWindow : EditorWindow
    {
        private int currentSection = 0;
        private string[] sections = { "INFO", "MODELING", "ANIMATION", "IMAGE", "SCENE" };
        private Dictionary<string, List<string>> toolCategories = new Dictionary<string, List<string>>();
        private Vector2 scrollPosition;
        private bool toolActive = false;
        private string activeTool = "";

        // Inicializa la ventana
        [MenuItem("VZ Optizone/Open Hub")]
        public static void ShowWindow()
        {
            GetWindow<VZOptizoneWindow>("VZ Optizone");
        }

        private void OnEnable()
        {
            // Inicializar herramientas para cada sección
            toolCategories.Add("IMAGE", new List<string> { "Convert Asset to Image", 
                "Split texture into channels", 
                "Merge textures into one",
                "Extract frames from video",
                "Convert sprites to animation clip"});
            toolCategories.Add("MODELING", new List<string> { "Merge mesh and create atlas",
                "Remove not visible vertex",
                "Hollow shell",
                "Multi material Finder",
                "Multi material splitter",
                "Generate mesh uv lightmaps",
                "Move UV inside grid",
                "Vertex ID Display",
                "Recalculate Mesh Bounds",
                "Micro triangle detector"});
            toolCategories.Add("ANIMATION", new List<string> { "Remove blendshapes",
                "Animation terminator",
                "Bake pose",
                "Combine animations/ors into one",
                "Transfer bone weight",
                "VAT Baker from Animation Clip",
                "VAT All in One"});
            toolCategories.Add("SCENE", new List<string> { "Lightmap checker",
                "Renamer"});
        }

        private void OnGUI()
        {
            DrawHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftNavigationPanel();

                // Contenido del panel derecho
                GUILayout.BeginVertical("box");
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

                // Si no hay ninguna herramienta activa, mostramos la lista de herramientas
                if (!toolActive)
                {
                    switch (sections[currentSection])
                    {
                        case "INFO":
                            DrawInfoSection();
                            break;
                        case "IMAGE":
                            DrawImageToolsMenu();
                            break;
                        case "ANIMATION":
                            DrawAnimationToolsMenu();
                            break;
                        case "MODELING":
                            DrawMODELINGToolsMenu();
                            break;
                        case "SCENE":
                            DrawSceneToolsMenu();
                            break;
                        default:
                            GUILayout.Label("Selecciona una herramienta en el menú de la izquierda.");
                            break;
                    }
                }
                else
                {
                    // Mostramos la herramienta seleccionada
                    DrawActiveTool();
                }

                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }
        }
        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false)); // Evita que el contenedor horizontal se expanda

            // "Optizone" en blanco
            GUIStyle whiteStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            GUILayout.Label("VZ Optizone", whiteStyle);

            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        private void DrawLeftNavigationPanel()
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(150));

            for (int i = 0; i < sections.Length; i++)
            {
                if (GUILayout.Button(sections[i]))
                {
                    currentSection = i;
                    toolActive = false; // Resetear la herramienta activa al cambiar de sección
                }
            }

            EditorGUILayout.EndVertical();
        }
        private void DrawInfoSection()
        {
            GUILayout.Label("Welcome to VZ Optizone", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            // Ajustar texto según el tamaño de la ventana
            GUILayout.Label("VZ Optizone is a multi-functional tool hub designed to streamline and optimize your workflow within Unity. " +
                            "It includes a range of utilities for tasks such as image processing, 3D model optimization, animation handling, " +
                            "and scene management, allowing you to quickly perform common operations with ease.",
                            new GUIStyle(GUI.skin.label) { wordWrap = true });

            GUILayout.Space(10);

            GUILayout.Label("How to Use VZ Optizone:", EditorStyles.boldLabel);

            GUILayout.Label("- Navigate the tool using the menu on the left.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("- Select the category related to the task you want to perform.", new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.Label("- Click on the tool you need and follow the instructions to complete your task.", new GUIStyle(GUI.skin.label) { wordWrap = true });

            GUILayout.Space(10);

            GUILayout.Label("Note: This tool hub is designed to evolve, and new tools may be added in the future to expand functionality.",
                            new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        private void DrawSceneToolsMenu()
        {
            GUILayout.Label("Tools for SCENE", EditorStyles.boldLabel);

            if (toolCategories.ContainsKey("SCENE"))
            {
                foreach (var tool in toolCategories["SCENE"])
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(tool))
                    {
                        OpenImageTool(tool);
                    }
                }
            }
        }
        private void DrawMODELINGToolsMenu()
        {
            GUILayout.Label("Tools for MODELING", EditorStyles.boldLabel);

            if (toolCategories.ContainsKey("MODELING"))
            {
                foreach (var tool in toolCategories["MODELING"])
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(tool))
                    {
                        OpenImageTool(tool);
                    }
                }
            }
        }

        private void DrawAnimationToolsMenu()
        {
            GUILayout.Label("Tools for ANIMATION", EditorStyles.boldLabel);

            if (toolCategories.ContainsKey("ANIMATION"))
            {
                foreach (var tool in toolCategories["ANIMATION"])
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(tool))
                    {
                        OpenImageTool(tool);
                    }
                }
            }
        }
        private void DrawImageToolsMenu()
        {
            GUILayout.Label("Tools for IMAGE", EditorStyles.boldLabel);

            if (toolCategories.ContainsKey("IMAGE"))
            {
                foreach (var tool in toolCategories["IMAGE"])
                {
                    GUILayout.Space(5);
                    if (GUILayout.Button(tool))
                    {
                        OpenImageTool(tool);
                    }
                }
            }
        }

        private void OpenImageTool(string toolName)
        {
            activeTool = toolName;
            toolActive = true;

            if (toolName == "Recalculate Mesh Bounds")
            {
                RecalculateMeshBoundsTool.EnableSceneView();
            }
        }

        private void DrawActiveTool()
        {
            if (GUILayout.Button("Volver"))
            {
                toolActive = false;
                activeTool = "";

                // Deshabilita los Handles de la herramienta cuando se cierra
                RecalculateMeshBoundsTool.DisableSceneView();
                MicroTrianglesDetectorTool.DisableSceneView();
            }

            switch (activeTool)
            {
                case "Convert Asset to Image":
                    AssetToImageConverterTool.DrawTool();
                    break;
                case "Split texture into channels":
                    ImageChannelSplitterTool.DrawTool();
                    break;
                case "Merge textures into one":
                    ImageChannelMergerTool.DrawTool();
                    break;
                case "Extract frames from video":
                    VideoToFramesExtractorTool.DrawTool();
                    break;
                case "Convert sprites to animation clip":
                    UIAnimationClipGeneratorTool.DrawTool();
                    break;
                case "Remove blendshapes":
                    BlendshapeRemovalTool.DrawTool();
                    break;
                case "Animation terminator":
                    AnimationTerminatorTool.DrawTool();
                    break;
                case "Merge mesh and create atlas":
                    //MeshCombinerAndAtlasTool.DrawTool();
                    break;
                case "Lightmap checker":
                    LightmapCheckerTool.DrawTool();
                    break;
                case "Remove not visible vertex":
                    //VertexOptimizationTool.DrawTool();
                    break;
                case "Hollow shell":
                    HollowShellMeshTool.DrawTool();
                    break;
                case "Multi material Finder":
                    MultiMaterialFinderTool.DrawTool();
                    break;
                case "Renamer":
                    RenameTool.DrawTool();
                    break;
                case "Multi material splitter":
                    MultimaterialMeshSplitterTool.DrawTool();
                    break;
                case "Generate mesh uv lightmaps":
                    UV2GeneratorTool.DrawTool();
                    break;
                case "Move UV inside grid":
                    UVAdjusterToolOpti.DrawTool();
                    break;
                case "Bake pose":
                    BakeMeshTool.DrawTool();
                    break;
                case "Vertex ID Display":
                    VertexIDDisplayerTool.DrawTool();
                    break;
                case "Combine animations/ors into one":
                    CombineAnimationsWithPathsTool.DrawTool();
                    break;
                case "Transfer bone weight":
                    BoneWeightTransferTool.DrawTool();
                    break;
                case "Recalculate Mesh Bounds":
                    if (Selection.activeGameObject != null)
                    {
                        MeshFilter meshFilter = Selection.activeGameObject.GetComponent<MeshFilter>();
                        if (meshFilter != null)
                        {
                            RecalculateMeshBoundsTool.SetTarget(meshFilter);
                        }
                    }
                    RecalculateMeshBoundsTool.DrawTool();
                    break;
                case "VAT Baker from Animation Clip":
                    AnimationClipTextureBakerTool.DrawTool();
                    break;
                //case "VAT All in One":
                //    VATAllInOneTool.DrawTool();
                //    break;
                case "Micro triangle detector":
                    MicroTrianglesDetectorTool.EnableSceneView();
                    MicroTrianglesDetectorTool.DrawTool();
                    break;


                    // Añadir casos para otras herramientas
            }
        }
    }
}