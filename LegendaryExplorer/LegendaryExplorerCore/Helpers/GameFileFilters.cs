using System;
using System.Collections.Generic;
using System.Text;

namespace LegendaryExplorerCore.Helpers
{
    /// <summary>
    /// Contains a list of filters for various games, for use with open/close dialogs. Options change with compilation flags of the library
    /// </summary>
    public static class GameFileFilters
    {
        public const string OpenFileFilter = "Supported package files|*.pcc;*.u;*.upk;*sfm;*udk;*.xxx|All files (*.*)|*.*";
        public const string UDKFileFilter = "UDK package files|*.upk;*udk";
        public const string ME1SaveFileFilter = "ME1 package files|*.u;*.upk;*sfm";
        public const string ME3ME2SaveFileFilter = "ME2/ME3 package files|*.pcc";
        public const string LESaveFileFilter = "LE package files|*.pcc";
    }
}
