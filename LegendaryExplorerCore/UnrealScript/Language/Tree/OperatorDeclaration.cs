using System;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public abstract class OperatorDeclaration
    {
        public string OperatorKeyword;
        public int NativeIndex;
        public VariableType ReturnType;

        public Function Implementer;

        public abstract bool HasOutParams { get; }

        protected OperatorDeclaration(string keyword, VariableType returnType, int nativeIndex)
        {
            OperatorKeyword = keyword;
            ReturnType = returnType;
            NativeIndex = nativeIndex;
        }

        public bool IdenticalSignature(OperatorDeclaration other)
        {
            if (this.ReturnType == null && other.ReturnType != null)
                return false;
            else if (other.ReturnType == null)
                return false;
                
            return this.OperatorKeyword == other.OperatorKeyword
                && string.Equals(this.ReturnType?.Name, other.ReturnType.Name, StringComparison.Ordinal);
        }
    }
}
