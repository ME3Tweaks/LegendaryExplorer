using System;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public sealed class PreOpDeclaration : OperatorDeclaration
    {
        public readonly FunctionParameter Operand;

        public PreOpDeclaration(TokenType operatorType,
                                VariableType returnType, int nativeIndex,
                                FunctionParameter operand) 
            : base(operatorType, returnType, nativeIndex) 
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
