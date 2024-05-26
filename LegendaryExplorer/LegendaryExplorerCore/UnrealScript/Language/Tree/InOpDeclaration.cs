using System;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public sealed class InOpDeclaration : OperatorDeclaration
    {
        public readonly FunctionParameter LeftOperand;
        public readonly FunctionParameter RightOperand;
        public readonly int Precedence;
        public override bool HasOutParams => LeftOperand.IsOut || RightOperand.IsOut;

        public InOpDeclaration(TokenType operatorType, int precedence, int nativeIndex,
                               VariableType returnType,
                               FunctionParameter leftOp, FunctionParameter rightOp)
            : base(operatorType, returnType, nativeIndex)
        {
            LeftOperand = leftOp;
            RightOperand = rightOp;
            Precedence = precedence;
        }

        public bool IdenticalSignature(InOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && string.Equals(this.LeftOperand.VarType.Name, other.LeftOperand.VarType.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(this.RightOperand.VarType.Name, other.RightOperand.VarType.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}