using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.TlkManagerNS;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.ME1;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.TLK;
using ME3ExplorerCore.TLK.ME1;
using Newtonsoft.Json;

namespace LegendaryExplorer.UnrealExtensions
{
    public static class TLKLoader
    {
        public static readonly string LoadedTLKsPathME1 = AppDirectories.AppDataFolder + "ME1LoadedTLKs.JSON";
        public static readonly string LoadedTLKsPathME2 = AppDirectories.AppDataFolder + "ME2LoadedTLKs.JSON";
        public static readonly string LoadedTLKsPathME3 = AppDirectories.AppDataFolder + "ME3LoadedTLKs.JSON";

        public static bool TlkFirstLoadDone { get; private set; } //Set when the TLK loading at startup is finished.

        private static void loadME1Tlk()
        {
            if (File.Exists(LoadedTLKsPathME1))
            {
                List<(int, string)> files = JsonConvert.DeserializeObject<List<(int, string)>>(File.ReadAllText(LoadedTLKsPathME1));
                foreach ((int exportnum, string filename) in files)
                {
                    ME1TalkFiles.LoadTlkData(filename, exportnum);
                }
            }
            else
            {
                string path = ME1Directory.CookedPCPath + @"Packages\Dialog\GlobalTlk.upk";
                if (File.Exists(path))
                {
                    try
                    {
                        IMEPackage pcc = MEPackageHandler.OpenME1Package(path);
                        ME1TalkFiles.tlkList.Add(new ME1TalkFile(pcc, 1));
                    }
                    catch (Exception)
                    {

                    }
                }
            }
        }

        private static void loadME2Tlk()
        {
            if (File.Exists(LoadedTLKsPathME2))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(LoadedTLKsPathME2));
                foreach (string filePath in files)
                {
                    try
                    {
                        ME2TalkFiles.LoadTlkData(filePath);
                    }
                    catch
                    {

                    }
                }
            }
            else
            {
                string tlkPath = ME2Directory.CookedPCPath + "BIOGame_INT.tlk";
                ME2TalkFiles.LoadTlkData(tlkPath);
            }
        }

        private static void loadME3Tlk()
        {
            if (File.Exists(LoadedTLKsPathME3))
            {
                List<string> files = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(LoadedTLKsPathME3));
                foreach (string filePath in files)
                {
                    try
                    {
                        ME3TalkFiles.LoadTlkData(filePath);
                    }
                    catch
                    {

                    }
                }
            }
            else
            {
                string tlkPath = ME3Directory.CookedPCPath + "BIOGame_INT.tlk";
                ME3TalkFiles.LoadTlkData(tlkPath);
            }
        }

        //TODO: Call this from somewhere in LEX
        public static void LoadSavedTlkList()
        {
            Action[] loaders =
            {
                loadME1Tlk,
                loadME2Tlk,
                loadME3Tlk
            };
            Parallel.ForEach(loaders, action => action());
            TLKManagerWPF.ME1LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKManagerWPF.ME2LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TLKManagerWPF.ME3LastReloaded = $"{DateTime.Now:HH:mm:ss tt}";
            TlkFirstLoadDone = true;
        }

        public static void SaveTLKList(MEGame game = MEGame.Unknown)
        {
            if (game == MEGame.Unknown || game == MEGame.ME1) File.WriteAllText(LoadedTLKsPathME1, JsonConvert.SerializeObject(ME1TalkFiles.tlkList.Select(x => (x.uindex, x.pcc.FilePath))));
            if (game == MEGame.Unknown || game == MEGame.ME2) File.WriteAllText(LoadedTLKsPathME2, JsonConvert.SerializeObject(ME2TalkFiles.tlkList.Select(x => x.path)));
            if (game == MEGame.Unknown || game == MEGame.ME3) File.WriteAllText(LoadedTLKsPathME3, JsonConvert.SerializeObject(ME3TalkFiles.tlkList.Select(x => x.path)));
        }
    }
}
