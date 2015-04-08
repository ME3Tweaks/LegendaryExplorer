using ME3Script.Analysis.Visitors;
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
        public FunctionParameter Operand;

        public PreOpDeclaration(String keyword,
            bool delim, CodeBody body, VariableType returnType,
            FunctionParameter operand, List<Specifier> specs,
            SourcePosition start, SourcePosition end) 
            : base(ASTNodeType.PrefixOperator, keyword, delim, body, returnType, specs, start, end) 
        {
            Operand = operand;
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
