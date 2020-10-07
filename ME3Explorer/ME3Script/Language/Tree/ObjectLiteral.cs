using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class ObjectLiteral : Expression
    {
        public NameLiteral Name;
        public VariableType Class;

        public ObjectLiteral(NameLiteral objectName, VariableType @class = null, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.ObjectLiteral, start, end)
        {
            Class = @class;
            Name = objectName;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return Class;
        }
    }
}
