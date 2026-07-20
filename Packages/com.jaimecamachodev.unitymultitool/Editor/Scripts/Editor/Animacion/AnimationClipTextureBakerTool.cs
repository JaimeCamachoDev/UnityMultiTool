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
        private static string outputPath = "Assets/BakedAnimationTex";

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

            // Campo para ingresar la ruta de salida
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Output Path:");
            outputPath = EditorGUILayout.TextField(outputPath);
            EditorGUILayout.EndHorizontal();

            // Botón para seleccionar carpeta de salida
            if (GUILayout.Button("Select Folder"))
            {
                string selectedFolder = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    outputPath = selectedFolder.Replace(Application.dataPath, "Assets");
                }
            }

            // Botón para hornear texturas
            if (GUILayout.Button("Bake Textures"))
            {
                if (infoTexGen == null)
                {
                    Debug.LogError("Compute Shader is not assigned!");
                    return;
                }

                if (targetObject == null)
                {
                    Debug.LogError("Target Object is not assigned!");
                    return;
                }

                BakeTextures();
            }
        }

        private static void BakeTextures()
        {
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
