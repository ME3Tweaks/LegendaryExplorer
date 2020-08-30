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
    internal static class Utilites
    {
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

        public static Stream LoadFileFromZipStream(Stream zipStream, string filename)
        {
            try
            {
                if (zipStream != null)
                {
                    using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
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
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error reading from zip archive: {e.Message}");
            }

            return null;
        }
    }
}
