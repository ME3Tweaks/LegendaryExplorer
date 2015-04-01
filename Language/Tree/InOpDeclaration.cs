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
        public VariableDeclaration LeftOperand;
        public VariableDeclaration RightOperand;
        public int Precedence;

        public InOpDeclaration(ASTNodeType type, String keyword, int precedence,
        bool delim, CodeBody body, VariableType returnType,
        VariableDeclaration leftOp, VariableDeclaration rightOp,
        SourcePosition start, SourcePosition end)
            : base(ASTNodeType.PostfixOperator, keyword, delim, body, returnType, start, end) 
        {
            LeftOperand = leftOp;
            RightOperand = rightOp;
            Precedence = precedence;
        }
    }
}