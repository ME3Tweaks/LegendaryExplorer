using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class FunctionParameter : VariableDeclaration
    {
        Variable ParamVar;
        public FunctionParameter(VariableType type, List<Specifier> specs, 
            Variable variable) : base(type, specs, null)
        {
            Type = ASTNodeType.FunctionParameter;
            ParamVar = variable;
        }
    }
}
