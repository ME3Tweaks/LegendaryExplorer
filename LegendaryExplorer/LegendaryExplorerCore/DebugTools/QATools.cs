using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.DebugTools
{
    public class QATools
    {
        public static string CheckLevelNAVLists(ExportEntry export)
        {
            if (export != null && export.FileRef.Game.IsGame3() && export.GetBinaryData<Level>() is Level plevel)
            {
                if (plevel.NavPoints.Count != plevel.numbers.Count)
                    return $"Export: {export.UIndex} NAV count does not match level int count";
            }
            return null;
        }
    }
}
