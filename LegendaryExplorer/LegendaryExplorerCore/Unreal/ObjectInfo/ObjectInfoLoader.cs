using System;
using System.Diagnostics;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

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
