using System;
using System.Collections.Generic;
using System.IO;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.Misc
{
    public static class AppDirectories
    {
        //Should move this to Path.Combine() in future
        public static string AppDataFolder => Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\LegendaryExplorer\").FullName;
        public static string StaticExecutablesDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder, "staticexecutables")).FullName; //ensures directory will always exist.

        /// <summary>
        /// Static files base URL points to the static directory on the LegendaryExplorer github and will have executable and other files that are no distributed in the initial download of LegendaryExplorer.
        /// </summary>
        public const string StaticFilesBaseURL = "https://github.com/ME3Tweaks/LegendaryExplorer/raw/Beta/StaticFiles/";

        public static string ExecFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exec");
        public static string HexConverterPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HexConverter.exe");

        public static string ObjectDatabasesFolder => Directory.CreateDirectory(Path.Combine(AppDataFolder, "ObjectDatabases")).FullName;
        public static string GetObjectDatabasePath(MEGame game) => Path.Combine(ObjectDatabasesFolder, $"{game}.bin");
        public static string GetMaterialGuidMapPath(MEGame game) => Path.Combine(ObjectDatabasesFolder, $"{game}MaterialMap.json"); //todo: update to .bin format

        public static List<FileDialogCustomPlace> GameCustomPlaces
        {
            get
            {
                List<FileDialogCustomPlace> list = new List<FileDialogCustomPlace>();
                if (ME1Directory.DefaultGamePath != null && Directory.Exists(ME1Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME1Directory.DefaultGamePath));
                if (ME2Directory.DefaultGamePath != null && Directory.Exists(ME2Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME2Directory.DefaultGamePath));
                if (ME3Directory.DefaultGamePath != null && Directory.Exists(ME3Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(ME3Directory.DefaultGamePath));
                if (LE1Directory.DefaultGamePath != null && Directory.Exists(LE1Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE1Directory.DefaultGamePath));
                if (LE2Directory.DefaultGamePath != null && Directory.Exists(LE2Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE2Directory.DefaultGamePath));
                if (LE3Directory.DefaultGamePath != null && Directory.Exists(LE3Directory.DefaultGamePath)) list.Add(new FileDialogCustomPlace(LE3Directory.DefaultGamePath));

                // Useful place: ME3Tweaks Mod Manager library
                var m3settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ME3TweaksModManager", "settings.ini");
                if (File.Exists(m3settingsFile))
                    try
                    {
                        DuplicatingIni ini = DuplicatingIni.LoadIni(m3settingsFile);
                        var libraryPath = ini.GetValue("ModLibrary", "LibraryPath");
                        if (libraryPath.HasValue && Directory.Exists(libraryPath.Value))
                        {
                            list.Add(new FileDialogCustomPlace(libraryPath.Value));
                        }
                    }
                    catch (Exception)
                    {
                        // Don't do anything
                    }

                return list;
            }
        }


        /// <summary>
        /// Shared method for getting a standard open file dialog.
        /// </summary>
        /// <returns></returns>
        public static OpenFileDialog GetOpenPackageDialog()
        {
            return new OpenFileDialog
            {
                Filter = GameFileFilters.OpenFileFilter,
                Title = "Open package file",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
        }
    }
}
