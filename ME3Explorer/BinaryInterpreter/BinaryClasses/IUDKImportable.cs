using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace ME3Explorer
{
    public interface IUDKImportable
    {
        void PortToME3Export(ExportEntry export);
        void PortToME2Export(ExportEntry export);
        void PortToME1Export(ExportEntry export);
    }
}
