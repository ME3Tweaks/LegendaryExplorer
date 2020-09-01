using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ME3ExplorerCore.Helpers
{
    internal static class Utilities
    {
        public static string LoadStringFromCompressedResource(string resourceName, string assetName)
        {
            var resource = Utilities.LoadEmbeddedFile(resourceName);
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
            var resource = Utilities.LoadEmbeddedFile(resourceName);
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
            MemoryStream ms = new MemoryStream();
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
                        MemoryStream ms = new MemoryStream();
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
    }
}
