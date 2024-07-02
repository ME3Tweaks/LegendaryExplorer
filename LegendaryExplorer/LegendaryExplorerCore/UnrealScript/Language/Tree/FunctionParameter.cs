using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class FunctionParameter : VariableDeclaration
    {
        public bool IsOptional
        {
            get => Flags.Has(EPropertyFlags.OptionalParm);
            set => Flags = value ? Flags | EPropertyFlags.OptionalParm : Flags & ~EPropertyFlags.OptionalParm;
        }
        public bool IsOut
        {
            get => Flags.Has(EPropertyFlags.OutParm);
            set => Flags = value ? Flags | EPropertyFlags.OutParm : Flags & ~EPropertyFlags.OutParm;
        }
        public Expression DefaultParameter;
        public CodeBody UnparsedDefaultParam;

        public FunctionParameter(VariableType type, EPropertyFlags flags, string Name, int arrayLength = 1, int start = -1, int end = -1)
            : base(type, flags, Name, arrayLength, "None", start, end)
        {
            Type = ASTNodeType.FunctionParameter;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
