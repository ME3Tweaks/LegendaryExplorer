using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Helpers
{
    public static class LegendaryExplorerCoreUtilities
    {
#if WINDOWS
        public static bool OpenAndSelectFileInExplorer(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return false;
            }
            //Clean up file path so it can be navigated OK
            filePath = System.IO.Path.GetFullPath(filePath);
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            return true;
        }
#endif
        public static string LoadStringFromCompressedResource(string resourceName, string assetName)
        {
            try
            {
                if (InternalLoadEmbeddedFile(resourceName, out Stream resource))
                {
                    using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
                    var objectinfoEntry = archive.Entries.FirstOrDefault(x => x.FullName == assetName);
                    if (objectinfoEntry != null)
                    {
                        using var zipStream = objectinfoEntry.Open();
                        using var sr = new StreamReader(zipStream);
                        return sr.ReadToEnd();
                    }

                    Debug.WriteLine($"Could not find {assetName} in zipstream!");
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error reading from zip archive: {e.Message}");
            }
            

            return null;
        }

        public static MemoryStream LoadFileFromCompressedResource(string resourceName, string assetName)
        {
            var resource = LegendaryExplorerCoreUtilities.LoadEmbeddedFile(resourceName);
            if (resource != null)
            {
                return LoadFileFromZipStream(resource, assetName);
            }

            return null;
        }

        public static MemoryStream LoadEmbeddedFile(string embeddedFilename)
        {
            if (InternalLoadEmbeddedFile(embeddedFilename, out Stream stream))
            {
                MemoryStream ms = MemoryManager.GetMemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
            return null;
        }

        private static bool InternalLoadEmbeddedFile(string embeddedFilename, out Stream stream)
        {
            var assembly = Assembly.GetExecutingAssembly();
#if DEBUG
            // Leave this here - it makes debugging missing resource names
            // way easier
            var resources = assembly.GetManifestResourceNames();
#endif

            //debug
            var assetName = $"LegendaryExplorerCore.Embedded.{embeddedFilename}";
            stream = assembly.GetManifestResourceStream(assetName);
            if (stream == null)
            {
                Debug.WriteLine($"{assetName} not found in embedded resources");
                Debugger.Break();
                return false;
            }
            return true;
        }

        public static MemoryStream LoadFileFromZipStream(Stream zipStream, string filename)
        {
            try
            {
                if (zipStream != null)
                {
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                    var objectinfoEntry = archive.Entries.FirstOrDefault(x => x.FullName == filename);
                    if (objectinfoEntry != null)
                    {
                        MemoryStream ms = MemoryManager.GetMemoryStream();
                        objectinfoEntry.Open().CopyTo(ms);
                        ms.Position = 0;
                        return ms;
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find {filename} in zipstream!");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error reading from zip archive: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Loads a package stream for the CustomResources file for the specified game 
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static MemoryStream GetCustomAppResourceStream(MEGame game)
        {
            using var zStream = LoadEmbeddedFile("GameResources.zip");
            if (zStream != null)
            {
                return LoadFileFromZipStream(zStream, LegendaryExplorerCoreLib.CustomResourceFileName(game));
            }

            return null;
        }
    }
}
