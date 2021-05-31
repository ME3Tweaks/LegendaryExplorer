using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using ME3ExplorerCore.Memory;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Helpers
{
    public static class ME3ExplorerCoreUtilities
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
            System.Diagnostics.Process.Start("explorer.exe", string.Format("/select,\"{0}\"", filePath));
            return true;
        }
#endif
        public static string LoadStringFromCompressedResource(string resourceName, string assetName)
        {
            var resource = ME3ExplorerCoreUtilities.LoadEmbeddedFile(resourceName);
            if (resource != null)
            {
                var strdata = LoadFileFromZipStream(resource, assetName);
                if (strdata != null)
                {
                    using StreamReader sr = new StreamReader(strdata);
                    return sr.ReadToEnd();
                }
            }

            return null;
        }

        public static MemoryStream LoadFileFromCompressedResource(string resourceName, string assetName)
        {
            var resource = ME3ExplorerCoreUtilities.LoadEmbeddedFile(resourceName);
            if (resource != null)
            {
                return LoadFileFromZipStream(resource, assetName);
            }

            return null;
        }

        public static MemoryStream LoadEmbeddedFile(string embeddedFilename)
        {

            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames();
            //debug
            var assetName = $"ME3ExplorerCore.Embedded.{embeddedFilename}";
            var stream = assembly.GetManifestResourceStream(assetName);
            if (stream == null)
            {
                Debug.WriteLine($"{assetName} not found in embedded resources");
                return null;
            }
            MemoryStream ms = MemoryManager.GetMemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        public static MemoryStream LoadFileFromZipStream(Stream zipStream, string filename)
        {
            try
            {
                if (zipStream != null)
                {
                    using ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
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
                return LoadFileFromZipStream(zStream, ME3ExplorerCoreLib.CustomResourceFileName(game));
            }

            return null;
        }
    }
}
