using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    public static class ME1Directory
    {
        public static string BioGamePath => GetBioGamePath();
        public static string GetBioGamePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "BioGame");
        }

        public static string DLCPath => GetDLCPath();
        public static string GetDLCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "DLC");
        }

        public static string CookedPCPath => GetCookedPCPath();
        public static string GetCookedPCPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), CookedName);
        }

        public static string ExecutableFolder => GetExecutableDirectory();
        public static string GetExecutableDirectory(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(rootPathOverride, "Binaries");
        }

        public static string ExecutablePath => GetExecutablePath();
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect.exe");
        }

        public static string ASIPath => GetASIPath();
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "asi");
        }

        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetCookedPCPath(rootPathOverride), "testVolumeLight_VFX.upk");
        }

        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect.exe" });
        public static readonly ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new[]
        {
            "binkw23.dll", // We say this is vanilla since it will very commonly be present and should not be removed
            "binkw32.dll",
            "MassEffectGDF.dll",
            "NxCooking.dll",
            "ogg.dll",
            "OpenAL32.dll",
            "PhysXCore.dll",
            "PhysXLoader.dll",
            "unicows.dll",
            "unrar.dll",
            "vorbis.dll",
            "vorbisfile.dll",
            "WINUI.dll",
            "wrap_oal.dll"
        });

        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect");
        public static string LODConfigFile => Path.Combine(BioWareDocumentsPath, @"Config", @"BIOEngine.ini");
        public static string CookedName => "CookedPC";


        private static string _gamePath;
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                {
                    if (string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME1Directory))
                    {
                        return null;
                    }
                    _gamePath = LegendaryExplorerCoreLibSettings.Instance.ME1Directory;
                }
                return Path.GetFullPath(_gamePath); //normalize
            }
            set
            {
                if (value != null)
                {
                    if (value.Contains("BioGame", StringComparison.OrdinalIgnoreCase))
                        value = value.Substring(0, value.LastIndexOf("BioGame", StringComparison.OrdinalIgnoreCase));
                }
                _gamePath = value;
            }
        }

        static ME1Directory()
        {
            ReloadDefaultGamePath(false);
        }

        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.ME1Directory))
            {
                DefaultGamePath = LegendaryExplorerCoreLibSettings.Instance.ME1Directory;
            }
            else
            {
#pragma warning disable CA1416
#if WINDOWS
                string hkey32 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\";
                string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\";
                string subkey = @"BioWare\Mass Effect";

                string keyName = hkey32 + subkey;
                string test = (string)Registry.GetValue(keyName, "Path", null);
                if (test != null)
                {
                    DefaultGamePath = test;
                    LegendaryExplorerCoreLibSettings.Instance.ME1Directory = DefaultGamePath;
                    return;
                }

                keyName = hkey64 + subkey;
                DefaultGamePath = (string)Registry.GetValue(keyName, "Path", null);
                if (DefaultGamePath != null)
                {
                    DefaultGamePath += Path.DirectorySeparatorChar;
                    LegendaryExplorerCoreLibSettings.Instance.ME1Directory = DefaultGamePath;
                }
#endif
#pragma warning restore CA1416
            }
        }


        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
            ["DLC_UNC"] = "Bring Down the Sky",
            ["DLC_Vegas"] = "Pinnacle Station"
        };

        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(new[]
        {
            "DLC_UNC",
            "DLC_Vegas"
        });

        public static bool IsValidGameDir(string rootPath)
        {
            return File.Exists(Path.Combine(rootPath, "Binaries", "MassEffect.exe"));
        }
    }
}
