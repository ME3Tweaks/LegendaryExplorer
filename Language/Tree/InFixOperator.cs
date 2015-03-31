using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class InFixOperator : Expression
    {
        public InFixOperator(SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.InFixOperatior, start, end) { }
    }
}
