using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class IntegerLiteral : Expression
    {
        public int Value;

        public IntegerLiteral(int val, SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.IntegerLiteral, start, end)
        {
            Value = val;
        }
    }
}
