using ME3Script.Analysis.Visitors;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
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
