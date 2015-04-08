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
        public String Value;
        public Specifier(String value, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.Specifier, start, end) 
        {
            Value = value;
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
