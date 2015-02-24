using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script
{
    //TODO: clean up / redesign
    public class ByteCode
    {
        #region Members

        public Byte Value { get; set; }
        #endregion

        public ByteCode()
        {
            Value = 0;
        }

        public ByteCode(Byte value)
        {
            Value = value;
        }
    }
}
