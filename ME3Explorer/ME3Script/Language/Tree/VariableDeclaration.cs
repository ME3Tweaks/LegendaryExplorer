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
        // Can contain StaticArrayVariables as well
        public VariableIdentifier Variable;
        // Can reference an existing type, or declare a new struct/enum type
        public VariableType VarType;

        public string Category;
        public string Name => Variable.Name;

        public int Size => Variable.Size;

        public bool IsStaticArray => Size > 1;

        public VariableDeclaration(VariableType type, UnrealFlags.EPropertyFlags flags,
                                   VariableIdentifier name, string category, SourcePosition start = null, SourcePosition end = null)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Flags = flags;
            VarType = type;
            Variable = name;
            Category = category;
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
                yield return Variable;
            }
        }
    }
}
