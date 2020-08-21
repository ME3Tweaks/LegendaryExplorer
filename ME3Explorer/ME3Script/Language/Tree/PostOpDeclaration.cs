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
    public class PostOpDeclaration : OperatorDeclaration
    {
        public FunctionParameter Operand;

        public PostOpDeclaration(string keyword,
                                 VariableType returnType, int nativeIndex,
                                 FunctionParameter operand)
            : base(keyword, returnType, nativeIndex)
        {
            Operand = operand;
        }

        public bool IdenticalSignature(PostOpDeclaration other)
        {
            return base.IdenticalSignature(other)
                && string.Equals(this.Operand.VarType.Name.ToLower(), other.Operand.VarType.Name.ToLower(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
