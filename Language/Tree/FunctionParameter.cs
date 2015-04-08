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
        Variable ParamVar;
        public FunctionParameter(VariableType type, List<Specifier> specs,
            Variable variable, SourcePosition start, SourcePosition end)
            : base(type, specs, null, start, end)
        {
            Type = ASTNodeType.FunctionParameter;
            ParamVar = variable;
        }

        public override void VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
