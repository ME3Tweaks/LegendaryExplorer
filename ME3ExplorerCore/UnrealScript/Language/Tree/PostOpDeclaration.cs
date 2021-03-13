using System;

namespace Unrealscript.Language.Tree
{
    public class PostOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter Operand;
        public override bool HasOutParams => Operand.IsOut;

        public PostOpDeclaration(string keyword,
                                 VariableType returnType, int nativeIndex,
                                 FunctionParameter operand)
            : base(keyword, returnType, nativeIndex)
        {
            Operand = operand;
        }

        public bool IdenticalSignature(PostOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && string.Equals(this.Operand.VarType.Name.ToLower(), other.Operand.VarType.Name.ToLower(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
