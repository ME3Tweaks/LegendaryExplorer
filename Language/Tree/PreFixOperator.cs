using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class PreFixOperator : Expression
    {
        public PreFixOperator(SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.PreFixOperatior, start, end) { }
    }
}
