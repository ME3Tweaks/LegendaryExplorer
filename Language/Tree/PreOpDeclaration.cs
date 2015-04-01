using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class PreOpDeclaration : OperatorDeclaration
    {
        public VariableDeclaration Operand;

        public PreOpDeclaration(ASTNodeType type, String keyword,
            bool delim, CodeBody body, VariableType returnType,
            VariableDeclaration operand,
            SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.PrefixOperator, keyword, delim, body, returnType, start, end) 
        {
            Operand = operand;
        }
    }
}
