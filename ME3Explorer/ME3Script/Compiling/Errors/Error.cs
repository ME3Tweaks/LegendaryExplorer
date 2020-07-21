using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Compiling.Errors
{
    public class Error : LogMessage
    {
        public Error(String msg) : base(msg) { }

        public override string ToString()
        {
            return "ERROR: " + Message;
        }
    }
}
