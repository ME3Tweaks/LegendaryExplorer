using System;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class OperatorDeclaration
    {
        public readonly TokenType OperatorType;
        public readonly int NativeIndex;
        public readonly VariableType ReturnType;

        public Function Implementer;

        public abstract bool HasOutParams { get; }

        protected OperatorDeclaration(TokenType operatorType, VariableType returnType, int nativeIndex)
        {
            OperatorType = operatorType;
            ReturnType = returnType;
            NativeIndex = nativeIndex;
        }

        public bool IdenticalSignature(OperatorDeclaration other)
        {
            if (this.ReturnType is null && other.ReturnType is not null)
                return false;
            if (this.ReturnType is not null && other.ReturnType is null)
                return false;

            return this.OperatorType == other.OperatorType
                   && string.Equals(this.ReturnType?.Name, other.ReturnType?.Name, StringComparison.Ordinal);
        }
    }
}
