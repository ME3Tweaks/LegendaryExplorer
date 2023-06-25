using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorerCore.Misc.ME3Tweaks
{
    /// <summary>
    /// Contains information about backups provided by ME3Tweaks programs.
    /// Backups paths returned by this only work with the new ME3Tweaks
    /// registry key which began being used in June 2021.
    /// </summary>
    public static class ME3TweaksBackups
    {
        /// <summary>
        /// Gets a string value from the registry from the specified key and value name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        private static string GetRegistrySettingString(string key, string name)
        {
            return (string)Registry.GetValue(key, name, null);
        }

        /// <summary>
        /// Gets a game backup path of a ME3Tweaks backup. Forcing a cmmvanilla check will make it also require a cmm_vanilla file to be valid.
        /// ALOT Installer backups do not write this as they do not have to be vanilla, where as Mod Manager backups always require them.
        /// Returns null on platforms other than windows.
        /// </summary>
        /// <param name="game">What game to lookup backup for</param>
        /// <param name="forceCmmVanilla">Force check for cmm_vanilla file</param>
        /// <returns>Backup path if it exists (and is valid, if forcecmmvanilla exists). This checks for directory existence, biogame/binaries existence. Returns null if validation fails or path is not set</returns>
        public static string GetGameBackupPath(MEGame game, bool forceCmmVanilla = true)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }
            // Mod Manager 7
            string path = GetRegistrySettingString(@"HKEY_CURRENT_USER\Software\ME3Tweaks", $"{game}VanillaBackupLocation"); // Do not change
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }
            //Super basic validation
            if (!Directory.Exists(Path.Combine(path, "BioGame")) || !Directory.Exists(Path.Combine(path, @"Binaries")))
            {
                return null;
            }
            if (forceCmmVanilla && !File.Exists(Path.Combine(path, @"cmm_vanilla")))
            {
                return null; //do not accept alot installer backups that are missing cmm_vanilla as they are not vanilla.
            }
            return path;
        }
    }
}
