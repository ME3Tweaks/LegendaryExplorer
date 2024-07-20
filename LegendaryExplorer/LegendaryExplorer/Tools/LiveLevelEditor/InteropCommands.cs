using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.LiveLevelEditor
{
    /// <summary>
    /// Commands for sending to the LEXInterop ASI. Here for consistency.
    /// </summary>
    internal static class InteropCommands
    {
        public const string INTEROP_LOADPACKAGE = "LOADPACKAGE";
        public const string INTEROP_SHOWLOADINGINDICATOR = "SHOWLOADINGINDICATOR";
        public const string INTEROP_HIDELOADINGINDICATOR = "HIDELOADINGINDICATOR";


        // LIVE LEVEL EDITOR COMMANDS
        public const string LLE_SELECT_ACTOR = "LLE_SELECT_ACTOR";

        // LIVE MATERIAL EDITOR COMMANDS
        public const string LME_SET_SCALAR_EXPRESSION = "LLE_SET_MATEXPR_SCALAR";
        public const string LME_SET_VECTOR_EXPRESSION = "LLE_SET_MATEXPR_VECTOR";
        public const string LME_SET_MATERIAL = "LLE_SET_MATERIAL";
        public const string LME_GET_LOADED_MATERIALS = "LLE_GET_LOADED_MATERIALS";
    }
}
