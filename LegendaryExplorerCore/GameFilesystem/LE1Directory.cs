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
    public static class LE1Directory
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
            return null; //Path.Combine(rootPathOverride, "DLC"); // TODO: Implement in LEX
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
            return Path.Combine(rootPathOverride, "Binaries", "Win64");
        }

        public static string ExecutablePath => GetExecutablePath();
        public static string GetExecutablePath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetExecutableDirectory(rootPathOverride), "MassEffect1.exe");
        }

        public static string ASIPath => GetASIPath();
        public static string GetASIPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return null; // Path.Combine(GetExecutableDirectory(rootPathOverride), "asi");
        }

        public static string TextureModMarkerPath => GetTextureModMarkerPath();
        public static string GetTextureModMarkerPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return null; //Path.Combine(GetCookedPCPath(rootPathOverride), "testVolumeLight_VFX.upk"); //TODO: Implement texture markers in LEX??
        }

        public static string ISACTPath => GetISACTPath();
        public static string GetISACTPath(string rootPathOverride = null)
        {
            if (rootPathOverride == null) rootPathOverride = DefaultGamePath;
            if (rootPathOverride == null) return null; // There is no usable root path
            return Path.Combine(GetBioGamePath(rootPathOverride), "Content", "Packages", "ISACT");
        }

        public static readonly ReadOnlyCollection<string> ExecutableNames = Array.AsReadOnly(new[] { "MassEffect1.exe" });
        public static readonly ReadOnlyCollection<string> VanillaDlls = Array.AsReadOnly(new[]
        {
            "amd_ags_x64.dll",
            "bink2w64.dll",
            "dbdata.dll",
            "ogg.dll",
            "oo2core_8_win64.dll",
            "OpenAL32.dll",
            "PhysXCooking64.dll",
            "PhysXCore64.dll",
            "vorbis.dll",
            "vorbisfile.dll",
            "wrap_oal.dll"
        });

        public static string BioWareDocumentsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"BioWare", @"Mass Effect");
        public static string LODConfigFile => Path.Combine(BioWareDocumentsPath, @"Config", @"BIOEngine.ini"); // I think this changed for LE1
        public static string CookedName => "CookedPCConsole";


        private static string _gamePath;
        public static string DefaultGamePath
        {
            get
            {
                if (string.IsNullOrEmpty(_gamePath))
                    return null;
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

        public static string TocFile => DefaultGamePath != null ? Path.Combine(DefaultGamePath, @"BioGame", "PCConsoleTOC.bin") : null;

        static LE1Directory()
        {
            ReloadDefaultGamePath(false);
        }

        public static void ReloadDefaultGamePath(bool forceUseRegistry = false)
        {
            if (!forceUseRegistry && !string.IsNullOrEmpty(LegendaryExplorerCoreLibSettings.Instance?.LEDirectory))
            {
                DefaultGamePath = Path.Combine(LegendaryExplorerCoreLibSettings.Instance.LEDirectory, "ME1");
            }
            else
            {
#if WINDOWS
                if (LEDirectory.LookupDefaultPath()) ReloadDefaultGamePath(false);
#endif
            }
        }


        public static readonly CaseInsensitiveDictionary<string> OfficialDLCNames = new CaseInsensitiveDictionary<string>
        {
        };

        public static readonly ReadOnlyCollection<string> OfficialDLC = Array.AsReadOnly(Array.Empty<string>());
    }
}
