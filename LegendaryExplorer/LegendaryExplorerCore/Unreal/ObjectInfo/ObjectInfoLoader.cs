using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LegendaryExplorerCore.Unreal.ObjectInfo
{
    public static class ObjectInfoLoader
    {
        public static string LoadEmbeddedJSONText(MEGame game)
        {
            try
            {
                var infosStream = LegendaryExplorerCoreUtilities.LoadEmbeddedFile("Infos.zip");
                if (infosStream != null)
                {
                    var decompressedStream = LegendaryExplorerCoreUtilities.LoadFileFromZipStream(infosStream, $"{game}ObjectInfo.json");
                    using StreamReader reader = new StreamReader(decompressedStream);
                    return reader.ReadToEnd();
                }


            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load embedded json file for {game}: {e.Message}");
            }

            return null;
        }
    }
}
