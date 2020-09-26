using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;

namespace ME3Script.Language.Tree
{
    public class VariableDeclaration : Statement
    {
        public UnrealFlags.EPropertyFlags Flags;

        public VariableType VarType;

        public string Category;
        public string Name;

        public int ArrayLength;

        public bool IsStaticArray => ArrayLength > 1;

        public VariableDeclaration(VariableType type, UnrealFlags.EPropertyFlags flags,
                                   string name, int arrayLength = 0, string category = null, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Flags = flags;
            Name = name;
            ArrayLength = arrayLength;
            Category = category;
            VarType = IsStaticArray  && !(type is StaticArrayType) ? new StaticArrayType(type, ArrayLength) : type;
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

        public int GetSize() => VarType?.Size ?? 0 * ArrayLength;
    }
}
