using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Compiling.Errors
{
    public class LineError : PositionedMessage
    {
        public LineError(String msg, SourcePosition start, SourcePosition end)
            : base(msg, start, end) { }

        public override string ToString()
        {
            return "ERROR| Line " + Line + " |: " + Message; 
        }
    }
}
