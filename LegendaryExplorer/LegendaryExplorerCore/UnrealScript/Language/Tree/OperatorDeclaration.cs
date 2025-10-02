using System;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class OperatorDeclaration
    {
        public readonly TokenType OperatorType;
        public readonly int NativeIndex;
        public readonly VariableType ReturnType;

        public Function Implementer;

        public string FriendlyName => OperatorType is TokenType.Word ? Implementer?.FriendlyName ?? "None" : OperatorHelper.OperatorTypeToString(OperatorType);

        public abstract bool HasOutParams { get; }

        protected OperatorDeclaration(TokenType operatorType, VariableType returnType, int nativeIndex)
        {
            OperatorType = operatorType;
            ReturnType = returnType;
            NativeIndex = nativeIndex;
        }


    }
}
