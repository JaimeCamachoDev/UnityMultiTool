using System.IO;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JaimeCamachoDev.Multitool.Textures
{
    // Resuelve la ruta del binario ffmpeg.exe empaquetado dentro del propio paquete
    // (Editor/Plugins/ffmpeg/bin), sin depender de que el proyecto tenga una carpeta
    // "VZ Optizone" en Assets.
    internal static class FfmpegLocator
    {
        private const string PackageName = "com.jaimecamachodev.unitymultitool";
        private const string RelativeExecutablePath = "Editor/Plugins/ffmpeg/bin/ffmpeg.exe";

        public static string ExecutablePath
        {
            get
            {
                PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(FfmpegLocator).Assembly);
                string basePath = packageInfo != null ? packageInfo.resolvedPath : Path.GetFullPath("Packages/" + PackageName);
                return Path.Combine(basePath, RelativeExecutablePath);
            }
        }
    }
}
