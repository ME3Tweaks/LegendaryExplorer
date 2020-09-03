using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System.Collections.Generic;
using ME3ExplorerCore.Unreal;

namespace ME3Script.Language.Tree
{
    public class VariableDeclaration : Statement
    {
        public UnrealFlags.EPropertyFlags Flags;

        public VariableType VarType;

        public string Category;
        public string Name;

        public int Size;

        public bool IsStaticArray => Size > 1;

        public VariableDeclaration(VariableType type, UnrealFlags.EPropertyFlags flags,
                                   string name, int size = 0, string category = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Flags = flags;
            Name = name;
            Size = size;
            Category = category;
            VarType = IsStaticArray ? new StaticArrayType(type, Size) : type;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
        public override IEnumerable<ASTNode> ChildNodes
        {
            get
            {
                yield return VarType;
            }
        }
    }
}
