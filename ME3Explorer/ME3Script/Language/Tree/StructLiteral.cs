using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class StructLiteral : Expression
    {
        public List<Statement> Statements;

        public string StructType;

        public StructLiteral(string structType, List<Statement> statements, SourcePosition start = null, SourcePosition end = null) : base(ASTNodeType.StructLiteral, start, end)
        {
            Statements = statements;
            StructType = structType;

            foreach (AssignStatement assignStatement in statements.OfType<AssignStatement>())
            {
                assignStatement.Value.Outer = this;
            }
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }

        public override VariableType ResolveType()
        {
            return new VariableType(StructType, null, null);
        }
    }
}
