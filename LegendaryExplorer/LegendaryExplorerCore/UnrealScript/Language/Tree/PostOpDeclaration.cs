using System;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public sealed class PostOpDeclaration : OperatorDeclaration
    {
        public readonly FunctionParameter Operand;
        public override bool HasOutParams => Operand.IsOut;

        public PostOpDeclaration(TokenType operatorType,
                                 VariableType returnType, int nativeIndex,
                                 FunctionParameter operand)
            : base(operatorType, returnType, nativeIndex)
        {
            Operand = operand;
        }
    }
}
