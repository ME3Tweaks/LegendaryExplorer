using System;

namespace Unrealscript.Language.Tree
{
    public class PreOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter Operand;

        public PreOpDeclaration(string keyword,
                                VariableType returnType, int nativeIndex,
                                FunctionParameter operand) 
            : base(keyword, returnType, nativeIndex) 
        {
            Operand = operand;
        }

        public bool IdenticalSignature(PreOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && string.Equals(this.Operand.VarType.Name, other.Operand.VarType.Name, StringComparison.Ordinal);
        }

        public override bool HasOutParams => Operand.IsOut;
    }
}
