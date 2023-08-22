// This is direct copy of the file from the ME3TweaksCore repo
// Origin Date: 06/06/2022
// Only change is the namespace to prevent issues of same namespace in M3C
namespace LegendaryExplorerCore.Misc.ME3Tweaks
{
    /// <summary>
    /// Contains (some, not all) ASI Update Group IDs that can be used to request install of an ASI. Makes code easier to read.
    /// </summary>
    public static class ASIModIDs
    {
        // This is not comprehensive list. Just here for convenience.

        // ME1 ============================================
        public static readonly int ME1_DLC_MOD_ENABLER = 16;

        // ME2 ============================================

        // ME3 ============================================
        public static readonly int ME3_BALANCE_CHANGES_REPLACER = 5;
        public static readonly int ME3_AUTOTOC = 9;
        public static readonly int ME3_LOGGER = 8;

        // LE1 ============================================
        public static readonly int LE1_AUTOTOC = 29;
        public static readonly int LE1_AUTOLOAD_ENABLER = 32;
        public static readonly int LE1_DEBUGLOGGER_DEV = 70;
        public static readonly int LE1_LEX_INTEROP = 42;
        public static readonly int LE1_SCRIPT_DEBUGGER = 82;

        // LE2 ============================================
        public static readonly int LE2_AUTOTOC = 30;
        public static readonly int LE2_DEBUGLOGGER_DEV = 71;
        public static readonly int LE2_LEX_INTEROP = 79;
        public static readonly int LE2_SCRIPT_DEBUGGER = 81;

        // LE3 ============================================
        public static readonly int LE3_AUTOTOC = 31;
        public static readonly int LE3_DEBUGLOGGER_DEV = 72;
        public static readonly int LE3_LEX_INTEROP = 80;

    }
}