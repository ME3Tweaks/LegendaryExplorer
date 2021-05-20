using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Audio
{
    public static class WwiseVersions
    {
        public static string WwiseFullVersion(MEGame game) => game switch
        {
            MEGame.ME1 => null,
            MEGame.ME2 => "2009.1.0.3143",
            MEGame.ME3 => "2010.3.3.3773",
            MEGame.LE1 => null,
            _ => "2019.1.6.7110"
        };

        public static int? WwiseVersion(MEGame game) => game switch
        {
            MEGame.ME1 => null,
            MEGame.ME2 => 3143,
            MEGame.ME3 => 3773,
            MEGame.LE1 => null,
            _ => 7110
        };
    }
}
