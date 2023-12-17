using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class VariableIdentifier : ASTNode
    {
        public string Name;
        public int Size;
        public VariableIdentifier(string name, int start = -1, int end = -1, int size = 1) 
            : base(ASTNodeType.VariableIdentifier, start, end) 
        {
            Size = size;
            Name = name;
        }

        public override bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop)
        {
            return visitor.VisitNode(this, usop);
        }
    }
}
