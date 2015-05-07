using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public abstract class Expression : ASTNode
    {
        public Expression(ASTNodeType type, SourcePosition start, SourcePosition end) 
            : base(type, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public VariableType ResolveType()
        {
            //TODO
            return null;
        }
    }
}
