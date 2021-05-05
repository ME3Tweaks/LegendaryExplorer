using System;
using System.IO;

namespace LegendaryExplorer.Misc
{
    public static class AppDirectories
    {
        //Should move this to Path.Combine() in future
        public static string AppDataFolder => Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\LegendaryExplorer\").FullName;
        public static string StaticExecutablesDirectory => Directory.CreateDirectory(Path.Combine(AppDataFolder, "staticexecutables")).FullName; //ensures directory will always exist.

        // TODO: IMPLEMENT INTO LEX
        /// <summary>
        /// Static files base URL points to the static directory on the ME3Explorer github and will have executable and other files that are no distributed in the initial download of ME3Explorer.
        /// </summary>
        //public const string StaticFilesBaseURL = "https://github.com/ME3Tweaks/ME3Explorer/raw/Beta/StaticFiles/";

        // TODO: Should this be merged into the executable? Does this work in .NET Single File?
        public static string ExecFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exec");
        public static string HexConverterPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HexConverter.exe");
    }
}
