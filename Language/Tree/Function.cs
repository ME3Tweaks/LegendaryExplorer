using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Function : ASTNode
    {
        public String Name;
        public CodeBody Body;
        public VariableType ReturnType;
        public List<Specifier> Specifiers;
        public List<FunctionParameter> Parameters;

        public Function(String name, VariableType returntype, CodeBody body,
            List<Specifier> specs, List<FunctionParameter> parameters)
            : base(ASTNodeType.Function)
        {
            Name = name;
            Body = body;
            ReturnType = returntype;
            Specifiers = specs;
            Parameters = parameters;
        }
    }
}
