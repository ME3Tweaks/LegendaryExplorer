using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Specifier : ASTNode
    {
        public string Value;
        public Specifier(string value, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.Specifier, start, end) 
        {
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
