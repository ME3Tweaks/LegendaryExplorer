using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Misc
{
    /// <summary>
    /// Utility methods for handling conversion between OT and LE.
    /// </summary>
    public class CrossGenHelpers
    {
        /// <summary>
        /// Fetches and opens a package from the other generation.
        /// </summary>
        /// <param name="currentGenPackage">The current generation package</param>
        /// <param name="otherGenPackage">The other generation packaged opened, if found.</param>
        /// <returns>The error message (e.g. could not find other version), null otherwise.</returns>
        public static string FetchOppositeGenPackage(IMEPackage currentGenPackage, out IMEPackage otherGenPackage)
        {
            otherGenPackage = null;
            if (!currentGenPackage.Game.IsMEGame())
            {
                return "The passed in package is not a Mass Effect package.";
            }

            var otherGameVersion = currentGenPackage.Game.IsLEGame() ? currentGenPackage.Game.ToOTVersion() : currentGenPackage.Game.ToLEVersion();
            var openingLEVersionFromOT = currentGenPackage.Game.IsOTGame();

            var files = MELoadedFiles.GetFilesLoadedInGame(otherGameVersion);

            var otherVerNameBase = Path.GetFileNameWithoutExtension(currentGenPackage.FilePath);
            if (currentGenPackage.Game == MEGame.ME1 && openingLEVersionFromOT && otherVerNameBase == "BIOC_Base")
                otherVerNameBase = "SFXGame";
            if (currentGenPackage.Game == MEGame.LE1 && !openingLEVersionFromOT && otherVerNameBase == "SFXGame")
                otherVerNameBase = "BIOC_Base";

            var otherVerName = $"{otherVerNameBase}.{(currentGenPackage.Game == MEGame.LE1 ? "SFM" : "pcc")}";
            if (files.TryGetValue(otherVerName, out var matchingVersion))
            {
                otherGenPackage = MEPackageHandler.OpenMEPackage(matchingVersion);
                return null;
            }

            if (currentGenPackage.Game == MEGame.LE3)
            {
                // Check SFARs of ME3
                var me3DlcPath = ME3Directory.GetDLCPath();
                if (Directory.Exists(me3DlcPath))
                {
                    // Todo: Enumerate in official order
                    // Enumerate official DLC
                    foreach (var dlc in Directory.GetDirectories(me3DlcPath))
                    {
                        var dlcName = Path.GetFileName(dlc);

                        if (!ME3Directory.OfficialDLC.Contains(dlcName, StringComparer.InvariantCultureIgnoreCase))
                            continue; // Do not look in DLC mod folders

                        var sfarPath = Path.Combine(dlc, @"CookedPCConsole", @"Default.sfar");
                        if (File.Exists(sfarPath))
                        {
                            DLCPackage p = new DLCPackage(sfarPath);
                            var dlcEntry = p.FindFileEntry(otherVerName);
                            if (dlcEntry >= 0)
                            {
                                // Found!
                                var packageStream = p.DecompressEntry(dlcEntry);
                                otherGenPackage = MEPackageHandler.OpenMEPackageFromStream(packageStream, p.Files[dlcEntry].FileName);
                                otherGenPackage.IsMemoryPackage = true;
                                return null;
                            }
                        }
                    }
                }
            }

            if (currentGenPackage.Game == MEGame.LE1)
            {
                // try other extensions
                otherVerName = $"{otherVerNameBase}.u";
                if (files.TryGetValue(otherVerName, out var matchingVerMe1))
                {
                    otherGenPackage = MEPackageHandler.OpenMEPackage(matchingVerMe1);
                    return null;
                }
                otherVerName = $"{otherVerNameBase}.upk";
                if (files.TryGetValue(otherVerName, out matchingVerMe1))
                {
                    otherGenPackage = MEPackageHandler.OpenMEPackage(matchingVerMe1);
                    return null;
                }
            }

            return $"Could not find {Path.GetFileName(currentGenPackage.FilePath)} in the other version of this game.";
        }
    }
}
