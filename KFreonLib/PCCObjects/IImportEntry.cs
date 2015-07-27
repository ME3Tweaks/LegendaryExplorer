using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFreonLib.PCCObjects
{
    public interface IImportEntry
    {
        int link { get; set; }

        string Name { get; set; }

        string ObjectName { get; set; }

        string ClassName { get; set; }

        string PackageFullName { get; set; }

        int idxLink { get; set; }

        int idxObjectName { get; set; }
        byte[] data { get; set; }

        long ObjectFlags { get; set; }
    }
}
