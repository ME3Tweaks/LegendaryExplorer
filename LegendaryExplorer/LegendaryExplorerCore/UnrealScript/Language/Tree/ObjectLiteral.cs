using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ObjectLiteral : Expression
    {
        public NameLiteral Name;
        public VariableType Class;

        public ObjectLiteral(NameLiteral objectName, VariableType @class = null, int start = -1, int end = -1) : base(ASTNodeType.ObjectLiteral, start, end)
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
