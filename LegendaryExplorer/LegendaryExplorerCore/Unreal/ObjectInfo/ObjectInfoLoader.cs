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
                return LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("Infos.zip", $"{game}ObjectInfo.json");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load embedded json file for {game}: {e.Message}");
            }

            return null;
        }
    }
}
