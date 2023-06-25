using System;
using System.Collections.ObjectModel;
using System.IO;

#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Contains information about the main LE directory as well as Launcher location. LE[X]Directory classes will use the LEDirectory default path to build their game paths.
    /// </summary>
    public static class LEDirectory
    {
        /// <summary>
        /// Uses the registry to find the default game path for the Legendary Edition installation,
        /// saving any found path to the <see cref="LegendaryExplorerCoreLibSettings"/> LEDirectory setting.
        /// </summary>
        /// <remarks>On non-Windows platforms, this method does nothing and simply returns false.</remarks>
        /// <returns>True if a path was found in the registry, false otherwise</returns>
        public static bool LookupDefaultPath()
        {
#pragma warning disable CA1416
#if WINDOWS
            RegistryKey biowareKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BioWare", false);
            RegistryKey leKey;
            if (biowareKey != null)
            {
                foreach (var key in biowareKey.GetSubKeyNames())
                {
                    if (key.EndsWith("Legendary Edition", StringComparison.InvariantCultureIgnoreCase))
                    {
                        leKey = biowareKey.OpenSubKey(key, false);
                        if (leKey == null) break;

                        string directory = (string)leKey.GetValue("Install Dir", null);
                        if (directory != null)
                        {
                            LegendaryExplorerCoreLibSettings.Instance.LEDirectory = directory;
                            return true;
                        }
                    }
                }
            }

            // Steam lookup
            RegistryKey steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1328670", false);
            if (steamKey != null)
            {
                string directory = (string) steamKey.GetValue(@"InstallLocation");
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    LegendaryExplorerCoreLibSettings.Instance.LEDirectory = directory;
                    return true;
                }
            }
            return false;
#pragma warning restore CA1416
#else
            return false; // NOT IMPLEMENTED ON OTHER PLATFORMS
#endif
        }

        /// <summary>
        /// Gets the path of the Legendary Edition folder in the user's Documents/BioWare folder. This is where savegames and some configuration files are stored.
        /// </summary>
        /// <remarks>Only useful if executable is run with -NoHomeDir. Otherwise this folder won't exist and will be worthless.</remarks>
        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect Legendary Edition");

        /// <summary>
        /// Gets the path of the Launcher folder in the Legendary Edition install
        /// </summary>
        /// <remarks>Some external tools use this</remarks>
        public static string LauncherPath => GetLauncherPath();

        /// <summary>
        /// The filenames of any valid Launcher executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffectLauncher.exe" });

        /// <summary>
        /// Proxy for <see cref="GetExecutableDirectory"/> with no arguments
        /// </summary>
        /// <remarks>Some external tools use this.</remarks>
        public static string ExecutableFolder => GetExecutableDirectory();

        /// <summary>
        /// Proxy for <see cref="GetLauncherPath"/>
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Launcher folder</returns>
        public static string GetExecutableDirectory(string rootPathOverride = null) => GetLauncherPath(rootPathOverride);

        /// <summary>
        /// Gets the path of the Launcher folder in the Legendary Edition install
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Launcher folder</returns>
        public static string GetLauncherPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = LegendaryExplorerCoreLibSettings.Instance.LEDirectory;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Game", "Launcher");
        }

        /// <summary>
        /// Proxy for <see cref="GetLauncherExecutable"/> with no arguments
        /// </summary>
        public static string LauncherExecutable => GetLauncherExecutable();

        /// <summary>
        /// Gets the path of the MassEffectLauncher.exe executable
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to launcher executable, null if not found</returns>
        public static string GetLauncherExecutable(string rootPathOverride = null)
        {
            var launcherPath = GetLauncherPath(rootPathOverride);
            if (launcherPath == null) return null;
            return Path.Combine(launcherPath, "MassEffectLauncher.exe");
        }

        /// <summary>
        /// The filenames of any vanilla DLLs that are shipped in the Launcher folder
        /// </summary>
        public static readonly ReadOnlyCollection<string> VanillaLauncherDlls = Array.AsReadOnly(new[]
        {
            "bink2w64.dll"
        });

        /// <summary>
        /// Determines if a Mass Effect Legendary Edition folder is a valid game directory by checking for the launcher executable
        /// </summary>
        /// <remarks>Checks rootPath\Game\Launcher\MassEffectLauncher.exe</remarks>
        /// <param name="rootPath">Path to check</param>
        /// <returns>True if directory is valid, false otherwise</returns>
        public static bool IsValidGameDir(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "Game", "Launcher", "MassEffectLauncher.exe"));
        }
    }
}
