using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class Const : VariableType
    {
        public string Value;
        public Expression Literal;

        public Const(string name, string value, SourcePosition start = null, SourcePosition end = null) : base(name, start, end)
        {
            Type = ASTNodeType.Const;
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
