using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JaimeCamachoDev.Multitool.Animation
{
    // Resuelve los 4 shaders VAT reales que vienen empaquetados dentro del propio paquete
    // (Editor/Extras/Shaders/VAT), para que VAT Baker y VAT Combiner nunca tengan que pedirle
    // al usuario que los arrastre manualmente cada vez.
    internal static class VATShaderLibrary
    {
        private const string PackageName = "com.jaimecamachodev.unitymultitool";
        private const string ShadersRelativeFolder = "Editor/Extras/Shaders/VAT";

        public static Shader SingleMeshLit => Resolve("LIT_VAT_SingleMesh");
        public static Shader SingleMeshUnlit => Resolve("UNLIT_VAT_SingleMesh");
        public static Shader MultipleMeshLit => Resolve("LIT_VAT_MultipleMesh");
        public static Shader MultipleMeshUnlit => Resolve("UNLIT_VAT_MultipleMesh");

        private static Shader Resolve(string fileNameWithoutExtension)
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(VATShaderLibrary).Assembly);
            string basePath = packageInfo != null ? packageInfo.assetPath : "Packages/" + PackageName;
            string path = $"{basePath}/{ShadersRelativeFolder}/{fileNameWithoutExtension}.shadergraph";
            return AssetDatabase.LoadAssetAtPath<Shader>(path);
        }
    }
}
