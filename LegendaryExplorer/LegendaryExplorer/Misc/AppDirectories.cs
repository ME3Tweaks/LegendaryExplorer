using System;
using System.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Misc
{
    public static class AppDirectories
    {
        //Should move this to Path.Combine() in future
        public static string AppDataFolder => Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\LegendaryExplorer\").FullName;
        public static string StaticExecutablesDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder, "staticexecutables")).FullName; //ensures directory will always exist.

        // TODO: IMPLEMENT INTO LEX PROPERLY
        /// <summary>
        /// Static files base URL points to the static directory on the ME3Explorer github and will have executable and other files that are no distributed in the initial download of LegendaryExplorer.
        /// </summary>
        public const string StaticFilesBaseURL = "https://github.com/ME3Tweaks/LegendaryExplorer/raw/Beta/StaticFiles/";

        // TODO: Should this be merged into the executable? Does this work in .NET Single File?
        public static string ExecFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exec");
        public static string HexConverterPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HexConverter.exe");

        public static string ObjectDatabasesFolder => Directory.CreateDirectory(Path.Combine(AppDataFolder, "ObjectDatabases")).FullName;
        public static string GetObjectDatabasePath(MEGame game) => Path.Combine(ObjectDatabasesFolder, $"{game}.bin");
        public static string GetMaterialGuidMapPath(MEGame game) => Path.Combine(ObjectDatabasesFolder, $"{game}MaterialMap.json"); //todo: update to .bin format

    }
}
