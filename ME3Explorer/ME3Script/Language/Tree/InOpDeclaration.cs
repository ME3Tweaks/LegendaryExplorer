using System;

namespace ME3Script.Language.Tree
{
    public class InOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter LeftOperand;
        public FunctionParameter RightOperand;
        public int Precedence;

        //only used if NativeIndex is 0
        public Function Implementer;

        public InOpDeclaration(string keyword, int precedence, int nativeIndex,
                               VariableType returnType,
                               FunctionParameter leftOp, FunctionParameter rightOp)
            : base(keyword, returnType, nativeIndex)
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