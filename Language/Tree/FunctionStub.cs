using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class FunctionStub : Function
    {
        public SourcePosition BodyStart;
        public SourcePosition BodyEnd;

        public FunctionStub(String name, VariableType returntype,
            SourcePosition bodyStart, SourcePosition bodyEnd,
            List<Specifier> specs, List<FunctionParameter> parameters,
            SourcePosition start, SourcePosition end) 
            : base(name, returntype, null, specs, parameters, start, end)
        {
            BodyStart = bodyStart;
            BodyEnd = bodyEnd;
            Type = ASTNodeType.FunctionStub;
        }
    }
}
