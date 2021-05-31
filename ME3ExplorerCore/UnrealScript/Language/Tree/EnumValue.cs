using ME3ExplorerCore.UnrealScript.Analysis.Visitors;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Language.Tree
{
    public class EnumValue : ASTNode
    {
        public string Name;
        public byte IntVal;
        public Enumeration Enum;

        public EnumValue(string name, byte intVal, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableIdentifier, start, end)
        {
            Name = name;
            IntVal = intVal;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
