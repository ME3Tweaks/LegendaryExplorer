using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class VariableDeclaration : Statement
    {
        public List<Specifier> Specifiers;
        // Can contain StaticArrayVariables as well
        public List<VariableIdentifier> Variables;
        // Can reference an existing type, or declare a new struct/enum type
        public VariableType VarType;

        public VariableDeclaration(VariableType type, List<Specifier> specs,
            List<VariableIdentifier> names, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.VariableDeclaration, start, end)
        {
            Specifiers = specs;
            VarType = type;
            Variables = names;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
