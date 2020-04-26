using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;

namespace ME3Explorer.ME3Tweaks
{
    /// <summary>
    /// Contains information about backups provided by ME3Tweaks programs.
    /// ME1, ME2: From ALOT Installer
    /// ME3: From Mass Effect 3 Mod Manager
    ///
    /// All tools that use these backups use these locations, even if it seems misleading to be using it out of another registry key, since they were designed out of sync.
    /// </summary>
    public static class ME3TweaksBackups
    {
        /// <summary>
        /// Registry key for legacy Mass Effect 3 Mod Manager. Used to store the ME3 backup directory
        /// </summary>
        internal const string REGISTRY_KEY_ME3CMM = @"HKEY_CURRENT_USER\Software\Mass Effect 3 Mod Manager"; //Shared. Do not change

        /// <summary>
        /// ALOT Addon Registry Key, used for ME1 and ME2 backups
        /// </summary>
        internal const string REGISTRY_KEY_ALOT_ADDON = @"HKEY_CURRENT_USER\Software\ALOTAddon"; //Shared. Do not change


        /// <summary>
        /// Gets a game backup path from ME3Tweaks. Forcing a cmmvanilla check will make it also require a cmm_vanilla file to be valid. ALOT Installer backups do not write this as they do not have to be vanilla, where as Mod Manager backups always require them.
        /// </summary>
        /// <param name="game">What game to lookup backup for</param>
        /// <param name="forceCmmVanilla">Force check for cmm_vanilla file</param>
        /// <returns>Backup path if it exists (and is valid, if forcecmmvanilla exists). This checks for directory existence, biogame/binaries existence. Returns null if validation fails or path is not set.</returns>
        public static string GetGameBackupPath(MEGame game, bool forceCmmVanilla = true)
        {
            string path;
            switch (game)
            {
                case MEGame.ME1:
                    path = ME3TweaksUtilities.GetRegistrySettingString(REGISTRY_KEY_ALOT_ADDON, "ME1VanillaBackupLocation");
                    break;
                case MEGame.ME2:
                    path = ME3TweaksUtilities.GetRegistrySettingString(REGISTRY_KEY_ALOT_ADDON, "ME2VanillaBackupLocation");
                    break;
                case MEGame.ME3:
                    //Check for backup via registry - Use Mod Manager's game backup key to find backup.
                    path = ME3TweaksUtilities.GetRegistrySettingString(REGISTRY_KEY_ME3CMM, "VanillaCopyLocation");
                    break;
                default:
                    return null;
            }
            if (path == null || !Directory.Exists(path))
            {
                return null;
            }
            //Super basic validation
            if (!Directory.Exists(Path.Combine(path, @"BIOGame")) || !Directory.Exists(Path.Combine(path, @"Binaries")))
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
