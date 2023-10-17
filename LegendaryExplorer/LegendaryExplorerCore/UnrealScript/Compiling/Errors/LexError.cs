using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.UnrealScript.Compiling.Errors
{
    public class LexError : LineError
    {
        public LexError(string msg, int start, int end, int line) : base(msg, start, end, line)
        {
        }
    }
}
