using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StateLabel : Variable
    {
        public int StartOffset;

        public StateLabel(String name, int offset) : base(name)
        {
            StartOffset = offset;
            Type = ASTNodeType.StateLabel;
        }
    }
}
