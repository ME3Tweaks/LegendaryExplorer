using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorerCore.Gammtek.Extensions
{
    public static class UnrealExtensions
    {
        /// <summary>
        /// Gets the defaults for this export - the export must be a class. Returns null if the defaults is an import.
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public static ExportEntry GetDefaults(this ExportEntry export)
        {
            return export.FileRef.GetUExport(ObjectBinary.From<UClass>(export).Defaults);
        }

        /// <summary>
        /// Gets the Level object from this file
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static ExportEntry GetLevel(this IMEPackage package)
        {
            return package.FindExport("TheWorld.PersistentLevel");
        }

        /// <summary>
        /// Gets thte binary of the level from this package
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static Level GetLevelBinary(this IMEPackage package)
        {
            var level = GetLevel(package);
            if (level != null)
                return ObjectBinary.From<Level>(level);
            return null;
        }

        /// <summary>
        /// Gets the list of actors in the level. Skips null entries.
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static List<ExportEntry> GetLevelActors(this IMEPackage package)
        {
            var levelBin = GetLevelBinary(package);
            if (levelBin == null)
                return null; // Not level
            return levelBin.Actors.Where(x => x > 0).Select(x => package.GetUExport(x)).ToList();
        }
    }
}
