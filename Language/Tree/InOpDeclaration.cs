using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class InOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter LeftOperand;
        public FunctionParameter RightOperand;
        public int Precedence;

        public InOpDeclaration(String keyword, int precedence,
        bool delim, CodeBody body, VariableType returnType,
        FunctionParameter leftOp, FunctionParameter rightOp,
        List<Specifier> specs, SourcePosition start, SourcePosition end)
            : base(ASTNodeType.PostfixOperator, keyword, delim, body, returnType, specs, start, end)
        {
            LeftOperand = leftOp;
            RightOperand = rightOp;
            Precedence = precedence;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}