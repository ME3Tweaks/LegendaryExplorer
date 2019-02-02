using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{
    class UnhoodFunction
    {
        public static void Dump(IExportEntry exportToParse)
        {
            if (exportToParse.FileRef.Game != MEGame.ME1)
            {
                return;
            }

        }
    }
}
