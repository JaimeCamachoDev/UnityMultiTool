using System.Collections.Generic;
using System.IO;
using JaimeCamachoDev.Multitool.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Un VAT solo puede usar un único material/textura. Cuando la malla combinada (por
    // ejemplo, salida de VAT Combiner) todavía tiene varios submeshes/materiales, esta
    // herramienta empaqueta sus texturas base en un único atlas y remapea las UVs, para
    // que quede lista para hornear con un solo shader en VAT Baker.
    public static class VATPainterTool
    {
        private static readonly int[] atlasSizes = { 512, 1024, 2048, 4096 };
        private static readonly string[] atlasSizeLabels = { "512", "1024", "2048", "4096" };
        private static int atlasSizeIndex = 2;
        private static int atlasPadding = 8;
        private static string outputName = "PaintedForVAT";
        private static DefaultAsset outputFolder;
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";

        public static VisualElement CreateGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("VAT Painter") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 } });
            root.Add(new HelpBox(
                "Selecciona en la escena la malla combinada (por ejemplo, la salida de VAT Combiner) para unificar sus texturas base en un único atlas y dejarla lista para hornear un VAT.",
                HelpBoxMessageType.Info));

            var contentContainer = new VisualElement { style = { marginTop = 6 } };
            root.Add(contentContainer);

            void RefreshContent()
            {
                contentContainer.Clear();

                GameObject active = Selection.activeGameObject;
                SkinnedMeshRenderer skin = active != null ? active.GetComponent<SkinnedMeshRenderer>() : null;

                if (skin == null || skin.sharedMesh == null)
                {
                    contentContainer.Add(new HelpBox("Selecciona un objeto con SkinnedMeshRenderer para continuar.", HelpBoxMessageType.Warning));
                    return;
                }

                Mesh mesh = skin.sharedMesh;
                if (!mesh.isReadable)
                {
                    contentContainer.Add(new HelpBox($"'{active.name}' usa un mesh sin Read/Write habilitado en sus Import Settings.", HelpBoxMessageType.Error));
                    return;
                }

                Material[] materials = skin.sharedMaterials;

                var infoPanel = new MTUIPanel("Malla seleccionada");
                infoPanel.Add(new MTUIInfoLabel($"{active.name} — {mesh.subMeshCount} submesh(es), {materials.Length} material(es), {mesh.vertexCount} vértices"));
                contentContainer.Add(infoPanel);

                if (mesh.subMeshCount <= 1)
                {
                    contentContainer.Add(new HelpBox("Esta malla ya tiene un único submesh/material: no es necesario pintar un atlas, ya puedes hornearla directamente en VAT Baker.", HelpBoxMessageType.Info));
                    return;
                }

                var atlasPanel = new MTUIPanel("Configuración del atlas") { style = { marginTop = 10 } };
                atlasPanel.Add(new MTUIInfoLabel("Tamaño del atlas") { style = { marginBottom = 2 } });

                var sizeRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var sizeButtons = new List<MTUIActionButton>();

                void RefreshSizeButtons()
                {
                    for (int i = 0; i < sizeButtons.Count; i++)
                    {
                        bool selected = i == atlasSizeIndex;
                        sizeButtons[i].SetColors(
                            selected ? MTUIColors.BlueBackground : MTUIColors.NeutralBackground,
                            selected ? MTUIColors.BlueBorder : MTUIColors.NeutralBorder,
                            selected ? MTUIColors.BlueText : MTUIColors.NeutralText);
                    }
                }

                for (int i = 0; i < atlasSizeLabels.Length; i++)
                {
                    int index = i;
                    var button = new MTUIActionButton(atlasSizeLabels[i], () =>
                    {
                        atlasSizeIndex = Mathf.Clamp(index, 0, atlasSizes.Length - 1);
                        RefreshSizeButtons();
                    });
                    button.style.flexGrow = 1;
                    sizeButtons.Add(button);
                    sizeRow.Add(button);
                }
                RefreshSizeButtons();
                atlasPanel.Add(sizeRow);

                var paddingSlider = new SliderInt("Padding", 0, 64) { value = atlasPadding, style = { marginTop = 6 } };
                paddingSlider.RegisterValueChangedCallback(evt => atlasPadding = evt.newValue);
                atlasPanel.Add(paddingSlider);
                contentContainer.Add(atlasPanel);

                var savePanel = new MTUIPanel("Guardado") { style = { marginTop = 10 } };

                var nameField = new TextField("Nombre del resultado") { value = outputName };
                nameField.RegisterValueChangedCallback(evt => outputName = evt.newValue);
                savePanel.Add(nameField);

                var folderField = new ObjectField("Carpeta de destino") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = outputFolder };
                folderField.RegisterValueChangedCallback(evt => outputFolder = evt.newValue as DefaultAsset);
                savePanel.Add(folderField);
                contentContainer.Add(savePanel);

                var paintButton = new MTUIActionButton("Generar atlas y aplicar", () =>
                {
                    Paint(skin, outputName);
                    RefreshContent();
                });
                paintButton.style.marginTop = 10;
                contentContainer.Add(paintButton);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => Selection.selectionChanged += RefreshContent);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Selection.selectionChanged -= RefreshContent);

            RefreshContent();

            return root;
        }

        private static void Paint(SkinnedMeshRenderer skin, string resultName)
        {
            Mesh source = skin.sharedMesh;
            Material[] materials = skin.sharedMaterials;
            int subMeshCount = source.subMeshCount;

            var sourceTextures = new List<Texture2D>(subMeshCount);
            var tempTextures = new List<Texture2D>();

            for (int i = 0; i < subMeshCount; i++)
            {
                Material material = i < materials.Length ? materials[i] : null;
                Texture2D texture = ExtractBaseTexture(material);
                if (texture == null)
                {
                    texture = CreateSolidTexture(Color.white);
                    tempTextures.Add(texture);
                }
                else if (!texture.isReadable)
                {
                    Texture2D readable = CreateReadableCopy(texture);
                    tempTextures.Add(readable);
                    texture = readable;
                }

                sourceTextures.Add(texture);
            }

            var atlas = new Texture2D(atlasSizes[atlasSizeIndex], atlasSizes[atlasSizeIndex], TextureFormat.RGBA32, false) { name = resultName + "_Atlas" };
            Rect[] rects = atlas.PackTextures(sourceTextures.ToArray(), atlasPadding, atlasSizes[atlasSizeIndex], false);

            foreach (Texture2D temp in tempTextures)
            {
                Object.DestroyImmediate(temp);
            }

            if (rects == null || rects.Length != subMeshCount)
            {
                Object.DestroyImmediate(atlas);
                EditorUtility.DisplayDialog("VAT Painter", $"Las {subMeshCount} texturas no caben en un atlas de {atlasSizes[atlasSizeIndex]}px con el padding actual ({atlasPadding}px). Prueba con un tamaño mayor o reduce el padding.", "Entendido");
                return;
            }

            Mesh paintedMesh = BuildPaintedMesh(source, rects, resultName);

            Material firstMaterial = materials.Length > 0 ? materials[0] : null;
            Material paintedMaterial = firstMaterial != null ? new Material(firstMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            paintedMaterial.name = resultName + "_Mat";
            ApplyAtlasTexture(paintedMaterial, atlas);

            string outputPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputPath;
            if (!AssetDatabase.IsValidFolder(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            string atlasPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, atlas.name + ".asset").Replace("\\", "/"));
            AssetDatabase.CreateAsset(atlas, atlasPath);

            string meshPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, paintedMesh.name + ".asset").Replace("\\", "/"));
            AssetDatabase.CreateAsset(paintedMesh, meshPath);

            string materialPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(outputPath, paintedMaterial.name + ".mat").Replace("\\", "/"));
            AssetDatabase.CreateAsset(paintedMaterial, materialPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Material savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Undo.RecordObject(skin, "Paint VAT atlas");
            skin.sharedMesh = savedMesh;
            skin.sharedMaterials = new[] { savedMaterial };

            Debug.Log($"VAT Painter: atlas generado ({atlas.width}x{atlas.height}) y aplicado a '{skin.gameObject.name}'.");
        }

        private static Mesh BuildPaintedMesh(Mesh source, Rect[] rects, string meshName)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            Color[] colors = source.colors;
            Vector2[] uv = source.uv;
            BoneWeight[] boneWeights = source.boneWeights;

            bool hasNormals = normals != null && normals.Length == vertices.Length;
            bool hasTangents = tangents != null && tangents.Length == vertices.Length;
            bool hasColors = colors != null && colors.Length == vertices.Length;
            bool hasUv = uv != null && uv.Length == vertices.Length;
            bool hasBoneWeights = boneWeights != null && boneWeights.Length == vertices.Length;

            var newVertices = new List<Vector3>();
            var newNormals = hasNormals ? new List<Vector3>() : null;
            var newTangents = hasTangents ? new List<Vector4>() : null;
            var newColors = hasColors ? new List<Color>() : null;
            var newUv = new List<Vector2>();
            var newBoneWeights = hasBoneWeights ? new List<BoneWeight>() : null;
            var triangles = new List<int>();

            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                Rect rect = rects[submesh];
                int[] subTriangles = source.GetTriangles(submesh);

                for (int i = 0; i < subTriangles.Length; i++)
                {
                    int originalIndex = subTriangles[i];

                    newVertices.Add(vertices[originalIndex]);
                    newNormals?.Add(normals[originalIndex]);
                    newTangents?.Add(tangents[originalIndex]);
                    newColors?.Add(colors[originalIndex]);
                    newBoneWeights?.Add(boneWeights[originalIndex]);

                    Vector2 uvCoord = hasUv ? uv[originalIndex] : Vector2.zero;
                    uvCoord.x = rect.x + uvCoord.x * rect.width;
                    uvCoord.y = rect.y + uvCoord.y * rect.height;
                    newUv.Add(uvCoord);

                    triangles.Add(newVertices.Count - 1);
                }
            }

            var painted = new Mesh
            {
                name = meshName,
                indexFormat = newVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            painted.SetVertices(newVertices);
            painted.SetTriangles(triangles, 0);
            painted.SetUVs(0, newUv);
            if (newNormals != null) painted.SetNormals(newNormals);
            else painted.RecalculateNormals();
            if (newTangents != null) painted.SetTangents(newTangents);
            if (newColors != null) painted.SetColors(newColors);
            if (newBoneWeights != null) painted.boneWeights = newBoneWeights.ToArray();
            painted.bindposes = source.bindposes;

            painted.RecalculateBounds();
            return painted;
        }

        private static void ApplyAtlasTexture(Material material, Texture2D atlas)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
        }

        private static Texture2D ExtractBaseTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            Texture texture = null;
            if (material.HasProperty("_BaseMap")) texture = material.GetTexture("_BaseMap");
            if (texture == null && material.HasProperty("_MainTex")) texture = material.GetTexture("_MainTex");

            return texture as Texture2D;
        }

        private static Texture2D CreateReadableCopy(Texture2D source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            return copy;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
