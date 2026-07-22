using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace VZOptizone
{
    public static class AnimationClipTextureBakerTool
    {
        private static ComputeShader infoTexGen;
        private static GameObject targetObject;
        private static DefaultAsset outputFolder;
        private const string DefaultOutputPath = "Assets/BakedAnimationTex";

        public static void DrawTool()
        {
            GUILayout.Label("Animation Clip Texture Baker", EditorStyles.boldLabel);

            // Campo para seleccionar el Compute Shader
            infoTexGen = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/VZ Optizone/Extras/ComputeShaders/MeshInfoTextureGen.compute"
            );
            if (infoTexGen == null)
            {
                EditorGUILayout.HelpBox("Compute Shader is not assigned!", MessageType.Warning);
            }

            // Campo para seleccionar el GameObject objetivo
            targetObject = EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true) as GameObject;

            // Carpeta de salida: arrastra una carpeta desde la ventana Project
            DefaultAsset newFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);
            if (newFolder != outputFolder && newFolder != null)
            {
                string folderPath = AssetDatabase.GetAssetPath(newFolder);
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    outputFolder = newFolder;
                }
                else
                {
                    Debug.LogWarning("El objeto arrastrado no es una carpeta del proyecto.");
                }
            }
            else if (newFolder == null)
            {
                outputFolder = null;
            }

            if (outputFolder == null)
            {
                EditorGUILayout.HelpBox($"Si no se asigna carpeta se usará '{DefaultOutputPath}'.", MessageType.Info);
            }

            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Arrastra un GameObject con Animator y SkinnedMeshRenderer en \"Target Object\" para poder hornear.", MessageType.Info);
            }

            GUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(infoTexGen == null || targetObject == null))
            {
                if (GUILayout.Button("Bake Textures"))
                {
                    BakeTextures();
                }
            }
        }

        private static void BakeTextures()
        {
            string outputPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : DefaultOutputPath;
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var skin = targetObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin == null)
            {
                Debug.LogError("No SkinnedMeshRenderer found on the target object.");
                return;
            }

            var vCount = skin.sharedMesh.vertexCount;
            var texWidth = Mathf.NextPowerOfTwo(vCount);
            var mesh = new Mesh();
            var animator = targetObject.GetComponent<Animator>();

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                Debug.LogError("No Animator or RuntimeAnimatorController found on the target object.");
                return;
            }

            var clips = animator.runtimeAnimatorController.animationClips;

            foreach (var clip in clips)
            {
                var frames = Mathf.NextPowerOfTwo((int)(clip.length / 0.05f));
                var dt = clip.length / frames;
                var infoList = new List<VertInfo>();

                var pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = $"{targetObject.name}.{clip.name}.posTex",
                    enableRandomWrite = true
                };
                pRt.Create();
                RenderTexture.active = pRt;
                GL.Clear(true, true, Color.clear);

                for (var i = 0; i < frames; i++)
                {
                    clip.SampleAnimation(targetObject, dt * i);
                    skin.BakeMesh(mesh);
                    infoList.AddRange(mesh.vertices.Select(v => new VertInfo { position = v }));
                }

                var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
                buffer.SetData(infoList.ToArray());

                var kernel = infoTexGen.FindKernel("CSMain");
                infoTexGen.SetInt("VertCount", vCount);
                infoTexGen.SetBuffer(kernel, "Info", buffer);
                infoTexGen.SetTexture(kernel, "OutPosition", pRt);
                infoTexGen.Dispatch(kernel, vCount / 32 + 1, frames / 32 + 1, 1);

                buffer.Release();

                var posTex = RenderTextureToTexture2D.Convert(pRt);
                var assetPath = Path.Combine(outputPath, $"{pRt.name}.asset").Replace("\\", "/");
                AssetDatabase.CreateAsset(posTex, assetPath);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Baking complete.");
        }

        private struct VertInfo
        {
            public Vector3 position;
        }

        private static class RenderTextureToTexture2D
        {
            public static Texture2D Convert(RenderTexture rt)
            {
                var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;
                return texture;
            }
        }
    }
}
