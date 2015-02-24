using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script
{
    public class BytecodeTokenizer : TokenizableDataStream<ByteCode>
    {
        public BytecodeTokenizer(ByteCode[] data)
            : base(() => data.ToList())
        {

        }
    }
}

