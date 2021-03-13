using System;
using Unrealscript.Analysis.Visitors;
using Unrealscript.Utilities;

namespace Unrealscript.Language.Tree
{
    public abstract class Expression : ASTNode
    {
        protected Expression(ASTNodeType type, SourcePosition start, SourcePosition end) 
            : base(type, start, end) { }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public abstract VariableType ResolveType();
    }
}
