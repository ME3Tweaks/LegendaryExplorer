using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class PostOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter Operand;

        public PostOpDeclaration(String keyword,
            bool delim, CodeBody body, VariableType returnType,
            FunctionParameter operand,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.PostfixOperator, keyword, delim, body, returnType, start, end)
        {
            Operand = operand;
        }
    }
}
