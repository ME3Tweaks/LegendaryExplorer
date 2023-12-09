using System;
using System.Diagnostics;
using System.IO;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorerCore.Misc.ME3Tweaks
{
    public static class ModManagerIntegration
    {
        /// <summary>
        /// Reads the version number of the last executed version of ME3Tweaks Mod Manager as listed in the registry
        /// </summary>
        /// <returns>0 if not found (or could not read), the build number (the last set of digits) otherwise</returns>
        public static int GetModManagerBuildNumber()
        {
            var m3ExecutableLocation = GetModManagerExecutableLocation();
            if (m3ExecutableLocation != null && File.Exists(m3ExecutableLocation))
            {
                // Get version information.
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(m3ExecutableLocation);
                return fvi.ProductPrivatePart; // This is the 'build' number
            }

            // It was not found
            return 0;
        }

        /// <summary>
        /// Fetches the ME3Tweaks Mod Manager executable location, if it is exists. The value returned is the last instance run by the user.
        /// </summary>
        /// <returns>Path to last run session executable if found; null otherwise</returns>
        public static string GetModManagerExecutableLocation()
        {
            var m3ExecutableLocation = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\ME3Tweaks", "ExecutableLocation", null);
            if (m3ExecutableLocation is string str && File.Exists(str))
            {
                return str;
            }

            return null; // not found
        }

        /// <summary>
        /// Fetches the donor output path from the last run ME3Tweaks tool that supports it.
        /// </summary>
        /// <returns>Path to last run session executable if found; null otherwise</returns>
        public static string GetDonorOutputPath()
        {
            var donorOutputPath = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\ME3Tweaks", "DonorOutputPath", null);
            if (donorOutputPath is string str && Directory.Exists(str))
            {
                return str;
            }

            return null; // not found
        }

        /// <summary>
        /// Instructs Mod Manager to install the Bink ASI loader to the specified game root path (GameTarget)
        /// </summary>
        /// <param name="game">Game to request installation for. In Mod Manager, this will select the default game target (which is what LEC uses, unless the user specifies otherwise)</param>
        /// <returns>True if request was made, false otherwise</returns>
        public static bool RequestBinkInstallation(MEGame game)
        {
            return InternalRequestModManagerTask($"--installbink --game {game}");
        }

        /// <summary>
        /// Instructs Mod Manager to install the specified ASI group ASI loader to the specified game.
        /// </summary>
        /// <param name="game">Game to request installation for. In Mod Manager, this will select the default game target (which is what LEC uses, unless the user specifies otherwise)</param>
        /// <param name="ASIid">The updategroup of the ASI to install for.</param>
        /// <param name="version">The version of the ASI to install for. Can be used to hardcode against a known working version. Setting a value of 0 will install the latest from the ASI manifest.</param>
        /// <returns>True if request was made, false otherwise</returns>
        public static bool RequestASIInstallation(MEGame game, int ASIid, int version = 0)
        {
            return InternalRequestModManagerTask($"--installasi {ASIid} --game {game}");
        }

        /// <summary>
        /// Invokes ME3Tweaks Mod Manager with the specified arguments.
        /// </summary>
        /// <param name="arguments">Arguments to pass to M3 - invalid arguments will be ignored by M3</param>
        /// <returns>True if the process was started, false otherwise</returns>
        private static bool InternalRequestModManagerTask(string arguments)
        {
            var m3ExecutableLocation = GetModManagerExecutableLocation();
            if (m3ExecutableLocation == null)
            {
                return false;
            }

            ProcessStartInfo psi = new ProcessStartInfo(m3ExecutableLocation)
            {
                Arguments = arguments,
                WorkingDirectory = Directory.GetParent(m3ExecutableLocation).FullName,
            };
            Process.Start(psi);
            return true;
        }
    }
}
