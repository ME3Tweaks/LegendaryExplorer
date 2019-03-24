using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME1Explorer
{
    public interface ITalkFile
    {
        string findDataById(int strRefID, bool withFileName = false);
    }
}