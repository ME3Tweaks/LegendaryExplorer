using ME3Script.Analysis.Visitors;
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
            FunctionParameter operand, List<Specifier> specs,
            SourcePosition start, SourcePosition end)
            : base(ASTNodeType.PostfixOperator, keyword, delim, body, returnType, specs, start, end)
        {
            Operand = operand;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }

        public bool IdenticalSignature(PostOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && this.Operand.VarType.Name.ToLower() == other.Operand.VarType.Name.ToLower();
        }
    }
}
