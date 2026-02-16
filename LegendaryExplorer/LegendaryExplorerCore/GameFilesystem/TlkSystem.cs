using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using LegendaryExplorerCore.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace LegendaryExplorerCore.GameFilesystem
{
    public static class TLKSystem
    {
        public static List<ITalkFile> LoadTLKs(MEGame game, MELocalization localization, bool male, string gamePath = null)
        {
            if (gamePath == null)
            {
                gamePath = MEDirectories.GetDefaultGamePath(game);
            }

            switch (game)
            {
                case MEGame.ME1:
                case MEGame.LE1:
                    return LoadTlksForGame1(gamePath, game, localization, male);
                case MEGame.ME2:
                case MEGame.LE2:
                    return LoadTlksForGame2(gamePath, game, localization);
                case MEGame.ME3:
                case MEGame.LE3:
                    return LoadTlksForGame3(gamePath, game, localization);
            }

            return new List<ITalkFile>();
        }

        private static List<ITalkFile> LoadTlksForGame1(string gamePath, MEGame game, MELocalization localization, bool male)
        {
            List<ITalkFile> tlkFiles = new List<ITalkFile>();
            if (localization == MELocalization.None)
                localization = MELocalization.INT;

            var packageExtension = game == MEGame.ME1 ? ".upk" : ".pcc";

            string baseTlkPackagePath = null;
            if (game == MEGame.LE1)
            {
                baseTlkPackagePath = Path.Combine(gamePath, @"BIOGame", game.CookedDirName(), $@"Startup_{localization}{packageExtension}");
                if (File.Exists(baseTlkPackagePath))
                {
                    using var package = MEPackageHandler.UnsafePartialLoad(baseTlkPackagePath, x => x.ClassName == @"BioTlkFile");
                    var tlkIFP = @"GlobalTlk.GlobalTlk_tlk";
                    if (male)
                    {
                        tlkIFP += @"_M";
                    }

                    var baseTlk = package.FindExport(tlkIFP, @"BioTlkFile");
                    var tlk = new ME1TalkFile(baseTlk);
                    tlkFiles.Add(tlk);
                }
            }
            else
            {
                baseTlkPackagePath = Path.Combine(gamePath, @"BIOGame", game.CookedDirName(), $@"Packages", @"Dialog", $@"GlobalTlk{packageExtension}");
                if (File.Exists(baseTlkPackagePath))
                {
                    using var package = MEPackageHandler.UnsafePartialLoad(baseTlkPackagePath, x => x.ClassName == @"BioTlkFile");
                    var tlkIFP = @"GlobalTlk_tlk";
                    if (male)
                    {
                        tlkIFP += @"_M";
                    }

                    var baseTlk = package.FindExport(tlkIFP, @"BioTlkFile");
                    var tlk = new ME1TalkFile(baseTlk);
                    tlkFiles.Add(tlk);
                }
            }

            // DLC TLKs
            var dlcPath = game == MEGame.LE1
                ? Path.Combine(gamePath, "BIOGame", "DLC")
                : Path.Combine(gamePath, "DLC");

            if (Directory.Exists(dlcPath))
            {
                var dlcFolders = Directory.GetDirectories(dlcPath, "DLC_*");
                var dlcMounts = new List<(string dlcName, int mountPriority, List<string> globalTalkTables)>();

                foreach (var dlcFolder in dlcFolders)
                {
                    var dlcName = Path.GetFileName(dlcFolder);
                    var autoloadPath = Path.Combine(dlcFolder, "Autoload.ini");

                    if (File.Exists(autoloadPath))
                    {
                        int? modMount = null;
                        Dictionary<string, string> iniEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        var iniLines = File.ReadAllLines(autoloadPath);
                        bool inPackagesSection = false;
                        bool inME1DLCMountSection = false;

                        foreach (var line in iniLines)
                        {
                            var trimmedLine = line.Trim();

                            if (trimmedLine.Equals("[Packages]", StringComparison.OrdinalIgnoreCase))
                            {
                                inPackagesSection = true;
                                inME1DLCMountSection = false;
                                continue;
                            }
                            else if (trimmedLine.Equals("[ME1DLCMOUNT]", StringComparison.OrdinalIgnoreCase))
                            {
                                inPackagesSection = false;
                                inME1DLCMountSection = true;
                                continue;
                            }
                            else if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                            {
                                inPackagesSection = false;
                                inME1DLCMountSection = false;
                                continue;
                            }

                            if (inPackagesSection && trimmedLine.Contains("="))
                            {
                                var parts = trimmedLine.Split('=');
                                if (parts.Length == 2)
                                {
                                    var key = parts[0].Trim();
                                    var value = parts[1].Trim();
                                    iniEntries[key] = value;
                                }
                            }
                            else if (inME1DLCMountSection && trimmedLine.Contains("="))
                            {
                                var parts = trimmedLine.Split('=');
                                if (parts.Length == 2 && parts[0].Trim().Equals("ModMount", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (int.TryParse(parts[1].Trim(), out int mount))
                                    {
                                        modMount = mount;
                                    }
                                }
                            }
                        }

                        if (modMount == 0 && game == MEGame.ME1)
                        {
                            if (dlcFolder == "DLC_UNC")
                            {
                                modMount = 1;
                            }
                            else if (dlcFolder == "DLC_Vegas")
                            {
                                modMount = 2;
                            }
                        }

                        // Enumerate GlobalTalkTable entries sequentially (1, 2, 3...) until one doesn't exist
                        List<string> globalTalkTables = new List<string>();
                        int tableIndex = 1;
                        while (iniEntries.TryGetValue($"GlobalTalkTable{tableIndex}", out string tableName))
                        {
                            globalTalkTables.Add(tableName);
                            tableIndex++;
                        }

                        if (modMount.HasValue && globalTalkTables.Count > 0)
                        {
                            dlcMounts.Add((dlcName, modMount.Value, globalTalkTables));
                        }
                    }

                    else if (game == MEGame.ME1)
                    {
                        if (dlcName == "DLC_UNC")
                        {
                            dlcMounts.Add((dlcName, 1, ["DLC_UNC_GlobalTlk.GlobalTlk_tlk"]));
                        }
                        else if (dlcName == "DLC_Vegas")
                        {
                            dlcMounts.Add((dlcName, 2, ["DLC_Vegas_GlobalTlk.GlobalTlk_tlk"]));
                        }
                    }
                }

                // Sort by mount priority (lowest to highest)
                dlcMounts.Sort((a, b) => a.mountPriority.CompareTo(b.mountPriority));

                // Load TLK files from each DLC
                foreach (var (dlcName, mountPriority, globalTalkTables) in dlcMounts)
                {
                    var cookedPath = Path.Combine(dlcPath, dlcName, game.CookedDirName());

                    foreach (var globalTalkTable in globalTalkTables)
                    {
                        // Extract filename from the GlobalTalkTable value
                        // Value format: "DLC_MOD_EDEN_GlobalTlk.GlobalTlk_tlk"
                        // We want the part before the period
                        var dotIndex = globalTalkTable.IndexOf('.');
                        if (dotIndex > 0)
                        {
                            var filename = globalTalkTable.Substring(0, dotIndex);
                            var searchPattern = $"{filename}{packageExtension}";

                            // Search for the file in the cooked folder and its subfolders
                            string tlkPackagePath = null;
                            if (Directory.Exists(cookedPath))
                            {
                                var matchingFiles = Directory.GetFiles(cookedPath, searchPattern, SearchOption.AllDirectories);
                                if (matchingFiles.Length > 0)
                                {
                                    tlkPackagePath = matchingFiles[0];
                                }
                            }

                            if (tlkPackagePath != null && File.Exists(tlkPackagePath))
                            {
                                using var package = MEPackageHandler.UnsafePartialLoad(tlkPackagePath, x => x.ClassName == @"BioTlkFile");

                                // The IFP is the part after the filename and dot
                                var tlkIFP = globalTalkTable.Substring(dotIndex + 1);
                                if (male)
                                {
                                    tlkIFP += @"_M";
                                }

                                var dlcTlk = package.FindExport(tlkIFP, @"BioTlkFile");
                                if (dlcTlk != null)
                                {
                                    var tlk = new ME1TalkFile(dlcTlk);
                                    tlkFiles.Add(tlk);
                                } else
                                {
                                    Debug.WriteLine($@"Can't find TLK export {tlkIFP} in {tlkPackagePath}");
                                }
                            }
                        }
                    }
                }
            }

            return tlkFiles;
        }

        private static List<ITalkFile> LoadTlksForGame2(string gamePath, MEGame game, MELocalization localization)
        {
            List<ITalkFile> tlkFiles = new List<ITalkFile>();

            if (localization == MELocalization.None)
                localization = MELocalization.INT;

            var baseTlk = Path.Combine(gamePath, @"BIOGame", game.CookedDirName(), $@"BIOGame_{localization}.tlk");
            if (File.Exists(baseTlk))
            {
                var tlk = new ME2ME3TalkFile(baseTlk);
                tlkFiles.Add(tlk);
            }

            // Get DLC in mount-priority order
            var dlcPath = Path.Combine(gamePath, "BIOGame", "DLC");
            if (Directory.Exists(dlcPath))
            {
                var dlcFolders = Directory.GetDirectories(dlcPath, "DLC_*");
                var dlcMounts = new List<(string dlcName, MountFile mount)>();

                foreach (var dlcFolder in dlcFolders)
                {
                    var dlcName = Path.GetFileName(dlcFolder);
                    var cookedPath = Path.Combine(dlcFolder, game.CookedDirName());
                    var mountPath = Path.Combine(cookedPath, "Mount.dlc");

                    MountFile mount = null;

                    if (File.Exists(mountPath))
                    {
                        mount = new MountFile(mountPath);
                        dlcMounts.Add((dlcName, mount));
                    }
                }

                // Sort by mount priority (lowest to highest)
                dlcMounts.Sort((a, b) => a.mount.MountPriority.CompareTo(b.mount.MountPriority));

                // Load TLK files from each DLC
                foreach (var (dlcName, mount) in dlcMounts)
                {
                    var cookedPath = Path.Combine(dlcPath, dlcName, game.CookedDirName());
                    var bioEngine = Path.Combine(cookedPath, "BIOEngine.ini");

                    int? moduleNum = null;
                    if (File.Exists(bioEngine))
                    {
                        var iniLines = File.ReadAllLines(bioEngine);
                        bool inDLCModulesSection = false;

                        foreach (var line in iniLines)
                        {
                            var trimmedLine = line.Trim();

                            if (trimmedLine.Equals("[Engine.DLCModules]", StringComparison.OrdinalIgnoreCase))
                            {
                                inDLCModulesSection = true;
                                continue;
                            }

                            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                            {
                                inDLCModulesSection = false;
                                continue;
                            }

                            if (inDLCModulesSection && trimmedLine.Contains("="))
                            {
                                // Find module number for this DLC folder name
                                var parts = trimmedLine.Split('=');
                                if (parts.Length == 2 && parts[0].Trim().Equals(dlcName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (int.TryParse(parts[1].Trim(), out int num))
                                    {
                                        moduleNum = num;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (moduleNum.HasValue)
                    {
                        // Find module tlk
                        var dlcTlkPath = Path.Combine(cookedPath, $"DLC_{moduleNum.Value}_{localization}.tlk");
                        if (File.Exists(dlcTlkPath))
                        {
                            var tlk = new ME2ME3TalkFile(dlcTlkPath);
                            tlkFiles.Add(tlk);
                        }
                    }
                }
            }

            return tlkFiles;
        }

        private static List<ITalkFile> LoadTlksForGame3(string gamePath, MEGame game, MELocalization localization)
        {
            List<ITalkFile> tlkFiles = new List<ITalkFile>();

            if (localization == MELocalization.None)
                localization = MELocalization.INT;

            var baseTlk = Path.Combine(gamePath, @"BIOGame", @"CookedPCConsole", $@"BIOGame_{localization}.tlk");
            if (File.Exists(baseTlk))
            {
                var tlk = new ME2ME3TalkFile(baseTlk);
                tlkFiles.Add(tlk);
            }

            // Get DLC in mount-priority order
            var dlcPath = Path.Combine(gamePath, "BIOGame", "DLC");
            if (Directory.Exists(dlcPath))
            {
                var dlcFolders = Directory.GetDirectories(dlcPath, "DLC_*");
                var dlcMounts = new List<(string dlcName, MountFile mount)>();

                foreach (var dlcFolder in dlcFolders)
                {
                    var dlcName = Path.GetFileName(dlcFolder);
                    var cookedPath = Path.Combine(dlcFolder, "CookedPCConsole");
                    var mountPath = Path.Combine(cookedPath, "Mount.dlc");

                    MountFile mount = null;

                    if (File.Exists(mountPath))
                    {
                        mount = new MountFile(mountPath);
                    }
                    else if (game == MEGame.ME3)
                    {
                        // Try to get mount.dlc from Default.sfar
                        var sfarPath = Path.Combine(cookedPath, "Default.sfar");
                        if (File.Exists(sfarPath))
                        {
                            try
                            {
                                var sfar = new DLCPackage(sfarPath);
                                var mountIndex = sfar.FindFileEntry("Mount.dlc");
                                if (mountIndex >= 0)
                                {
                                    var mountStream = sfar.DecompressEntry(mountIndex);
                                    mount = new MountFile(MEGame.ME3);
                                    mount.LoadMountFileME3(mountStream);
                                }
                            }
                            catch
                            {
                                // Failed to read mount from SFAR, skip this DLC
                            }
                        }
                    }

                    if (mount != null)
                    {
                        dlcMounts.Add((dlcName, mount));
                    }
                }

                // Sort by mount priority (lowest to highest)
                dlcMounts.Sort((a, b) => a.mount.MountPriority.CompareTo(b.mount.MountPriority));

                // Load TLK files from each DLC
                foreach (var (dlcName, mount) in dlcMounts)
                {
                    var dlcTlkPath = Path.Combine(dlcPath, dlcName, "CookedPCConsole", $"{dlcName}_{localization}.tlk");
                    if (File.Exists(dlcTlkPath))
                    {
                        var tlk = new ME2ME3TalkFile(dlcTlkPath);
                        tlkFiles.Add(tlk);
                    }
                    else if (game == MEGame.ME3)
                    {
                        // Try to get TLK from Default.sfar
                        var cookedPath = Path.Combine(dlcPath, dlcName, "CookedPCConsole");
                        var sfarPath = Path.Combine(cookedPath, "Default.sfar");
                        if (File.Exists(sfarPath))
                        {
                            try
                            {
                                var sfar = new DLCPackage(sfarPath);
                                var tlkFileName = $"{dlcName}_{localization}.tlk";
                                var tlkIndex = sfar.FindFileEntry(tlkFileName);
                                if (tlkIndex >= 0)
                                {
                                    var tlkStream = sfar.DecompressEntry(tlkIndex);
                                    var tlk = new ME2ME3TalkFile(tlkStream, tlkFileName + " (SFAR)");
                                    tlkFiles.Add(tlk);
                                }
                            }
                            catch
                            {
                                // Failed to read TLK from SFAR, skip this DLC
                            }
                        }
                    }
                }
            }

            return tlkFiles;
        }
    }
}
