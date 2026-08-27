using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Contains information about the UDK Custom directory
    /// </summary>
    public static class UDKDirectory
    {
        /// <summary>
        /// Gets the path to the UDKGame folder for UDK
        /// </summary>
        public static string UDKGamePath => GetUDKGamePath();
        /// <summary>
        /// Gets the path to the UDKGame folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to UDKGame folder, null if no usable root path</returns>
        public static string GetUDKGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "UDKGame");
        }

        /// <summary>
        /// Gets the path to the Script folder for UDK
        /// </summary>
        public static string ScriptPath => GetScriptPath();
        /// <summary>
        /// Gets the path to Script folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Cooked folder, null if no usable root path</returns>
        public static string GetScriptPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetUDKGamePath(rootPathOverride), "Script");
        }

        /// <summary>
        /// Gets the path to the Content folder for UDK
        /// </summary>
        public static string ContentPath => GetContentPath();
        /// <summary>
        /// Gets the path to Content folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Cooked folder, null if no usable root path</returns>
        public static string GetContentPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetUDKGamePath(rootPathOverride), "Content");
        }

        /// <summary>
        /// Gets the path to the Maps folder for UDK
        /// </summary>
        public static string MapsPath => GetMapsPath();
        /// <summary>
        /// Gets the path to Maps folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Cooked folder, null if no usable root path</returns>
        public static string GetMapsPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetContentPath(rootPathOverride), "Maps");
        }

        /// <summary>
        /// Gets the path to the Shared folder for UDK
        /// </summary>
        public static string SharedPath => GetSharedPath();
        /// <summary>
        /// Gets the path to Shared folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to Cooked folder, null if no usable root path</returns>
        public static string GetSharedPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetContentPath(rootPathOverride), "Shared");
        }

        /// <summary>
        /// Gets the path to the executable folder for UDK
        /// </summary>
        public static string ExecutableFolder => GetExecutableDirectory();
        /// <summary>
        /// Gets the path to the executable folder for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to executable folder, null if no usable root path</returns>
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries", "Win64");
        }

        /// <summary>
        /// Gets the path to the game executable for UDK
        /// </summary>
        public static string ExecutablePath => GetExecutablePath();
        /// <summary>
        /// Gets the path to the game executable for UDK
        /// </summary>
        /// <param name="rootPathOverride">Optional: override game path root</param>
        /// <returns>Path to game executable, null if no usable root path</returns>
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "UDK.exe");
        }

        /// <summary>
        /// The filenames of any valid UDK executables
        /// </summary>
        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "UDK.exe" });

        private static string _DefaultGamePath;
        /// <summary>
        /// Gets or sets the default game root path that is used when locating game folders.
        /// By default, this path is loaded from the <see cref="LegendaryExplorerCoreLibSettings"/> instance.
        /// Updating this path will not update the value in the CoreLibSettings.
        /// </summary>
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_DefaultGamePath))
                {
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.UDKCustomDirectory))
                    {
                        return null;
                    }
                    _DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory;
                }
                return Path.GetFullPath(_DefaultGamePath); //normalize
            }
            set => _DefaultGamePath = value;
        }

        /// <summary>
        /// Reloads the default ME3 game path, either from LEC settings or from the registry
        /// </summary>
        /// <param name="forceUseRegistry">If true, registry will be used to determine game path. If false, LEC settings may be used instead</param>
        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.UDKCustomDirectory))
            {
                DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory;
            }
            else
            {
#pragma warning disable CA1416
#if WINDOWS
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

                var uninstallList = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\");
                if (uninstallList != null)
                {
                    var key = uninstallList.GetSubKeyNames().FirstOrDefault(x => x.StartsWith("UDK-"));
                    if (key != null)
                    {
                        var installEntry = uninstallList.OpenSubKey(key);
                        if (installEntry != null)
                        {
                            string installLoc = (string) installEntry.GetValue("InstallLocation");
                            if (installLoc != null)
                            {
                                DefaultGamePath = installLoc;
                                if (!DefaultGamePath.EndsWith(Path.DirectorySeparatorChar))
                                {
                                    DefaultGamePath += Path.DirectorySeparatorChar;
                                }
                                LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = DefaultGamePath;
                                return;
                            }
                        }
                    }
                }
#endif
#pragma warning restore CA1416
            }
        }


        /// <summary>
        /// Determines if a UDK folder is a valid game directory by checking for the udk executable
        /// </summary>
        /// <remarks>Checks rootPath\Binaries\Win64\UDK.exe</remarks>
        /// <param name="rootPath">Path to check</param>
        /// <returns>True if directory is valid, false otherwise</returns>
        public static bool IsValidGameDir(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "Binaries", "Win64", "UDK.exe"));
        }
    }
}
