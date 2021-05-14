#if WINDOWS
using Microsoft.Win32;
#endif

namespace LegendaryExplorerCore.GameFilesystem
{
    class LEDirectory
    {
        /// <summary>
        /// Uses the registry to find the default game path for the Legendary Edition installation. On non-windows platforms, this method does nothing and simply returns false.
        /// </summary>
        /// <returns></returns>
        public static bool LookupDefaultPath()
        {
#if WINDOWS
            string hkey64 = @"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\Mass Effect Legendary Edition";
            string test = (string)Registry.GetValue(hkey64, "Path", null);
            if (test != null)
            {
                LegendaryExplorerCoreLibSettings.Instance.LEDirectory = test;
                return true;
            }

            return false;
#else
            return false; // NOT IMPLEMENTED ON OTHER PLATFORMS
#endif
        }

        // Todo: Launcher utility methods
        // Will be useful for Mod Manager mods to find launcher for launcher modding
    }
}
