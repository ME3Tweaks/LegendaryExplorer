using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Script.Language.Tree
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
    }
}
