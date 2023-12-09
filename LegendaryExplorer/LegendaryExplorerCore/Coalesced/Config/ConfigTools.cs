using System;
using System.IO;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Coalesced.Config
{
    /// <summary>
    /// Utilities for working with configuration files in ME games
    /// </summary>
    public static class ConfigTools
    {

        /// <summary>
        /// Builds a combined config bundle representing the final set of the files on disk for the specified target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public static ConfigAssetBundle GetMergedBundle(MEGame game, string gamePathOverride = null)
        {
            ConfigAssetBundle mergedBundle = ConfigAssetBundle.FromSingleFile(game, GetCoalescedPath(game, gamePathOverride));
            var dlcPath = MEDirectories.GetDLCPath(game, gamePathOverride);
            var installedDLC = MELoadedDLC.GetDLCNamesInMountOrder(game, gamePathOverride);

            foreach (var dlcName in installedDLC)
            {
                var dlcBundle = ConfigAssetBundle.FromDLCFolder(game, Path.Combine(dlcPath, dlcName, game.CookedDirName()), dlcName);
                dlcBundle.MergeInto(mergedBundle);
            }
            return mergedBundle;
        }

        /// <summary>
        /// Fetches the INT coalesced file path from the target. ME1 is not supported as it does not have a single file path
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GetCoalescedPath(MEGame game, string gamePathOverride = null)
        {
            if (game == MEGame.ME2)
                return Path.Combine(MEDirectories.GetBioGamePath(game, gamePathOverride), @"Config", @"PC", @"Cooked", @"Coalesced.ini");
            if (game is MEGame.LE1 or MEGame.LE2)
                return Path.Combine(MEDirectories.GetBioGamePath(game, gamePathOverride), @"Coalesced_INT.bin");
            if (game.IsGame3())
                return Path.Combine(MEDirectories.GetBioGamePath(game, gamePathOverride), @"Coalesced.bin");

            throw new Exception($@"Cannot fetch combined Coalesced path for unsupported game: {game}");
        }
    }
}
