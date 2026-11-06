using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JaimeCamachoDev.Multitool.Textures
{
    // Localiza ffmpeg para las herramientas de imagen. El paquete no lo incluye (pesa ~100 MB),
    // así que lo busca en este orden:
    //   1. la ruta elegida a mano (Tools → JaimeCamachoDev → Multitool → Elegir ffmpeg…),
    //   2. una copia dentro del paquete (Editor/Plugins/ffmpeg/bin), por compatibilidad,
    //   3. el PATH del sistema (y las carpetas habituales de Homebrew en macOS),
    //   4. la copia que descarga esta misma clase, compartida por todos los proyectos.
    // Si no hay ninguna, en Windows ofrece descargar una build fija y comprueba su SHA-256.
    internal static class FfmpegLocator
    {
        private const string PackageName = "com.jaimecamachodev.unitymultitool";
        private const string PrefKey = "JaimeCamachoDev.Multitool.FfmpegPath";
        private const string Version = "8.0.1";
        private const string DownloadUrl = "https://github.com/GyanD/codexffmpeg/releases/download/8.0.1/ffmpeg-8.0.1-essentials_build.zip";
        private const string DownloadSha256 = "e2aaeaa0fdbc397d4794828086424d4aaa2102cef1fb6874f6ffd29c0b88b673";
        private const int DownloadMB = 102;

        private static bool IsWindows => Application.platform == RuntimePlatform.WindowsEditor;
        private static string ExeName => IsWindows ? "ffmpeg.exe" : "ffmpeg";
        private static string CacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JaimeCamachoDev", "ffmpeg-" + Version);
        private static string CachedExe => Path.Combine(CacheDir, "bin", ExeName);

        // Ruta de ffmpeg. Si no lo encuentra, ofrece descargarlo o elegirlo; devuelve null si no hay.
        public static string ExecutablePath => Find() ?? Offer();

        public static string Find()
        {
            string custom = EditorPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(custom) && File.Exists(custom)) return custom;

            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(FfmpegLocator).Assembly);
            string basePath = packageInfo != null ? packageInfo.resolvedPath : Path.GetFullPath("Packages/" + PackageName);
            string bundled = Path.Combine(basePath, "Editor", "Plugins", "ffmpeg", "bin", ExeName);
            if (File.Exists(bundled)) return bundled;

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            string extra = IsWindows ? "" : Path.PathSeparator + "/opt/homebrew/bin" + Path.PathSeparator + "/usr/local/bin" + Path.PathSeparator + "/usr/bin";
            foreach (string dir in (pathVar + extra).Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    string candidate = Path.Combine(dir.Trim().Trim('"'), ExeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }

            return File.Exists(CachedExe) ? CachedExe : null;
        }

        [MenuItem("Tools/JaimeCamachoDev/Multitool/Elegir ffmpeg…")]
        private static void PickMenu()
        {
            string current = Find();
            if (current != null) Debug.Log($"[Multitool] ffmpeg actual: {current}");
            Pick();
        }

        private static string Offer()
        {
            if (!IsWindows)
            {
                bool choose = EditorUtility.DisplayDialog("ffmpeg no encontrado",
                    "Estas herramientas necesitan ffmpeg. Instálalo (por ejemplo «brew install ffmpeg» o con el gestor de paquetes del sistema) o elige el ejecutable a mano.",
                    "Elegir ejecutable…", "Cancelar");
                return choose ? Pick() : null;
            }

            int option = EditorUtility.DisplayDialogComplex("ffmpeg no encontrado",
                $"Estas herramientas necesitan ffmpeg.\n\n¿Descargar ffmpeg {Version} (build «essentials» de gyan.dev, ~{DownloadMB} MB)? Se guarda en\n{CacheDir}\ny lo usan todos los proyectos.",
                "Descargar", "Cancelar", "Elegir ejecutable…");
            if (option == 0) return Download();
            if (option == 2) return Pick();
            return null;
        }

        private static string Pick()
        {
            string path = EditorUtility.OpenFilePanel("Elegir ffmpeg", "", IsWindows ? "exe" : "");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            EditorPrefs.SetString(PrefKey, path);
            Debug.Log($"[Multitool] ffmpeg: {path}");
            return path;
        }

        private static string Download()
        {
            string zip = Path.Combine(Path.GetTempPath(), $"ffmpeg-{Version}-{Guid.NewGuid():N}.zip");
            long total = 0, done = 0;
            var cancel = new CancellationTokenSource();
            Task task = Task.Run(async () =>
            {
                using var http = new HttpClient();
                using HttpResponseMessage response = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
                response.EnsureSuccessStatusCode();
                Interlocked.Exchange(ref total, response.Content.Headers.ContentLength ?? 0);
                using Stream source = await response.Content.ReadAsStreamAsync();
                using FileStream target = File.Create(zip);
                var buffer = new byte[1 << 16];
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancel.Token)) > 0)
                {
                    await target.WriteAsync(buffer, 0, read, cancel.Token);
                    Interlocked.Add(ref done, read);
                }
            });

            try
            {
                while (!task.IsCompleted)
                {
                    long t = Interlocked.Read(ref total), d = Interlocked.Read(ref done);
                    if (EditorUtility.DisplayCancelableProgressBar("Descargando ffmpeg", $"{d / 1048576} / {(t > 0 ? t / 1048576 : DownloadMB)} MB", t > 0 ? (float)d / t : 0f))
                        cancel.Cancel();
                    Thread.Sleep(100);
                }
                if (task.IsCanceled || cancel.IsCancellationRequested) return null;
                if (task.IsFaulted)
                {
                    Debug.LogError($"[Multitool] No se pudo descargar ffmpeg: {task.Exception?.GetBaseException().Message}");
                    return null;
                }

                EditorUtility.DisplayProgressBar("Descargando ffmpeg", "Comprobando el fichero…", 1f);
                string hash;
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(zip))
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (hash != DownloadSha256)
                {
                    Debug.LogError($"[Multitool] El ffmpeg descargado no coincide con el esperado (SHA-256 {hash}). No se usa.");
                    return null;
                }

                string staging = CacheDir + ".tmp";
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                Directory.CreateDirectory(Path.Combine(staging, "bin"));
                using (ZipArchive archive = ZipFile.OpenRead(zip))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        int slash = entry.FullName.IndexOf('/');
                        string relative = slash >= 0 ? entry.FullName.Substring(slash + 1) : entry.FullName;
                        if (relative == "bin/ffmpeg.exe" || relative == "LICENSE" || relative == "README.txt")
                            entry.ExtractToFile(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)), true);
                    }
                }
                if (Directory.Exists(CacheDir)) Directory.Delete(CacheDir, true);
                Directory.Move(staging, CacheDir);

                if (!File.Exists(CachedExe)) return null;
                Debug.Log($"[Multitool] ffmpeg {Version} instalado en {CacheDir}");
                return CachedExe;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Multitool] No se pudo instalar ffmpeg: {e.Message}");
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                try { if (File.Exists(zip)) File.Delete(zip); } catch (IOException) { }
            }
        }
    }
}
