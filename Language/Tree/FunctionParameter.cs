using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class FunctionParameter : VariableDeclaration
    {
        VariableIdentifier ParamVar;
        public FunctionParameter(VariableType type, List<Specifier> specs,
            VariableIdentifier variable, SourcePosition start, SourcePosition end)
            : base(type, specs, null, start, end)
        {
            Type = ASTNodeType.FunctionParameter;
            ParamVar = variable;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
