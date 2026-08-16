// This is direct copy of the file from the ME3TweaksCore repo
// Origin Date: 06/06/2022
// Only change is the namespace to prevent issues of same namespace in M3C
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LegendaryExplorerCore.Misc.ME3Tweaks
{
    /// <summary>
    /// Contains (some, not all) ASI Update Group IDs that can be used to request install of an ASI. Makes code easier to read.
    /// </summary>
    public static class ASIModIDs
    {
        // Updated 02/03/2026

        // ME1 ============================================
        public const int ME1_DLC_MOD_ENABLER = 16;

        // ME2 ============================================
        public const int ME2_KISMET_LOGGER = 14; // 'Kismet Logger for ME2'
        public const int ME2_FUNCTION_LOGGER = 15; // 'Function Logger for ME2'
        public const int ME2_ACE_SLAMMER = 18; // 'Ace Slammer for ME2'
        public const int ME2_SPLASH_ENABLER = 20; // 'Splash Enabler'
        public const int ME2_KASUMI_DLC_CRASH_FIX = 23; // 'Kasumi DLC Crash Fix'
        public const int ME2_STREAMING_LEVELS_HUD = 25; // 'Streaming Levels HUD'
        public const int ME2_DEATH_COUNTER_LS = 26; // 'Death Counter LS'
        public const int ME2_SEQACT_LOG_ENABLER = 27; // 'SeqAct_Log Enabler'
        public const int ME2_COMBOLUTION_SUPPORT = 28; // 'Combolution Support'


        // ME3 ============================================
        public const int ME3_BALANCE_CHANGES_REPLACER = 5;
        public const int ME3_AUTOTOC = 9;
        public const int ME3_ORIGIN_MP_STATUS = 1; // 'Origin MP Status'
        public const int ME3_CLIENT_MESSAGE_EXPOSER = 2; // 'Client Message Exposer'
        public const int ME3_ORIGIN_UNLINKER = 4; // 'Origin Unlinker'
        public const int ME3_MOUSE_DISABLER = 0; // 'Mouse Disabler'
        public const int ME3_LIVE_TLK_REPLACER = 6; // 'Live TLK Replacer'
        public const int ME3_ALLOW_MULTIPLE_INSTANCES = 7; // 'Allow mutliple ME3 instances'
        public const int ME3_LOGGER_TRUNCATING = 8; // 'ME3 Logger - Truncating' (alias)
        public const int ME3_KISMET_LOGGER = 10; // 'Kismet Logger'
        public const int ME3_RETALIATION_GLITCH_CRASH_FIX = 11; // 'Retaliation Glitch Crash Fix'
        public const int ME3_100_PERCENT_GALACTIC_READINESS = 12; // '100% Galactic Readiness'
        public const int ME3_POC_COOP_MOVING_SPAWN_POINTS = 13; // 'POC Co-Op Campaign: Moving Spawn Points'
        public const int ME3_DOCUMENTS_FOLDER_REDIRECTOR = 17; // 'Documents Folder Redirector'
        public const int ME3_SEQACT_LOG_ENABLER = 19; // 'SeqAct_Log Enabler'
        public const int ME3_GARBAGE_COLLECTION_FORCER = 22; // 'Garbage Collection Forcer'
        public const int ME3_SPLASH_ENABLER = 21; // 'Splash Enabler'
        public const int ME3_STREAMING_LEVELS_HUD = 24; // 'Streaming Levels HUD'
        public const int ME3_CONTROLLER_INPUT_TESTER = 33; // 'Controller Input Tester'

        // LE1 ============================================
        public const int LE1_AUTOTOC = 29;
        public const int LE1_AUTOLOAD_ENABLER = 32;
        public const int LE1_DEBUGLOGGER_DEV = 70;
        public const int LE1_LEX_INTEROP = 42;
        public const int LE1_SCRIPT_DEBUGGER = 82;
        public const int LE1_TEXTURE_OVERRIDE = 88;

        // Additional LE1 IDs (from ids.txt)
        public const int LE1_AUTOLOAD_ENABLER_DEBUG = 34; // 'Autoload Enabler (Debug)'
        public const int LE1_STREAMING_LEVELS_HUD = 35; // 'Streaming Levels HUD'
        public const int LE1_KISMET_LOGGER = 36; // 'Kismet Logger'
        public const int LE1_SEQACT_LOG_ENABLER = 37; // 'SeqAct_Log Enabler'
        public const int LE1_2DA_PRINTER = 43; // '2DA Printer' / 'Bio2DA Printer'
        public const int LE1_PNG_SCREENSHOTS = 44; // 'PNG ScreenShots'
        public const int LE1_LINKER_PRINTER = 47; // 'LE1 Linker Printer' / 'Linker Printer'
        public const int LE1_SSR_UNLOCKER = 51; // 'SSR Unlocker'
        public const int LE1_CONSOLE_EXTENSION = 75; // 'Console Extension'

        // LE2 ============================================
        public const int LE2_AUTOTOC = 30;
        public const int LE2_DEBUGLOGGER_DEV = 71;
        public const int LE2_HOT_RELOAD = 78;
        public const int LE2_LEX_INTEROP = 79;
        public const int LE2_SCRIPT_DEBUGGER = 81;
        public const int LE2_TEXTURE_OVERRIDE = 89;
        public const int LE2_STREAMING_LEVELS_HUD = 38; // 'Streaming Levels HUD'
        public const int LE2_KISMET_LOGGER = 41; // 'Kismet Logger'
        public const int LE2_SEQACT_LOG_ENABLER = 73; // 'SeqAct_Log Enabler'
        public const int LE2_LINKER_PRINTER = 48; // 'LE2 Linker Printer' / 'Linker Printer'
        public const int LE2_CONSOLE_EXTENSION = 76; // 'Console Extension'
        public const int LE2_FUNCTION_LOGGER = 84; // 'Function Logger'
        public const int LE2_PNG_SCREENSHOTS = 45; // 'PNG ScreenShots'

        // LE3 ============================================
        public const int LE3_AUTOTOC = 31;
        public const int LE3_DEBUGLOGGER_DEV = 72;
        public const int LE3_LEX_INTEROP = 80;
        public const int LE3_SCRIPT_DEBUGGER = 86;
        public const int LE3_TEXTURE_OVERRIDE = 87; // Also Multi TFC
        public const int LE3_STREAMING_LEVELS_HUD = 39; // 'Streaming Levels HUD'
        public const int LE3_KISMET_LOGGER = 40; // 'Kismet Logger'
        public const int LE3_SEQACT_LOG_ENABLER = 74; // 'SeqAct_Log Enabler'
        public const int LE3_LINKER_PRINTER = 49; // 'LE3 Linker Printer' / 'Linker Printer'
        public const int LE3_CONSOLE_EXTENSION = 77; // 'Console Extension'
        public const int LE3_FUNCTION_LOGGER = 85; // 'Function Logger'
        public const int LE3_PNG_SCREENSHOTS = 46; // 'PNG ScreenShots'


        /// <summary>
        /// Gets a list of installed ASI update groups (mod ids) and their versions. only works on ASIs built after 01/26/2026
        /// </summary>
        /// <param name="win64Path">Path to game Win64 folder</param>
        /// <returns></returns>
        public static IEnumerable<(int id,int version)> GetInstalledASIModIds(string win64Path)
        {
            var asiPath = Path.Combine(win64Path, "ASI");
            if (!Directory.Exists(asiPath))
            {
                yield break;
            }

            var asiFiles = Directory.GetFiles(asiPath, "*.asi");
            
            foreach (var asiFile in asiFiles)
            {
                FileVersionInfo fvi = null;
                try
                {
                    fvi = FileVersionInfo.GetVersionInfo(asiFile);
                }
                catch
                {
                    // If the file can't be read or doesn't have version info, skip it.
                }

                if (fvi == null)
                    continue;

                // ASI mod id is stored in ProductPrivatePart, version in ProductMajorPart.
                // Only return an entry when both pieces of information are present (non-zero).
                var asiId = fvi.ProductPrivatePart;
                var asiVersion = fvi.ProductMajorPart;

                if (asiId <= 0 || asiVersion <= 0)
                    continue;

                yield return (asiId, asiVersion);
            }
        }
    }
}