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
        public List<Token<String>> BodyTokens;
        public FunctionStub(String name, VariableType returntype, List<Token<String>> tokens,
            List<Specifier> specs, List<FunctionParameter> parameters,
            SourcePosition start, SourcePosition end) 
            : base(name, returntype, null, specs, parameters, start, end)
        {
            BodyTokens = tokens;
            Type = ASTNodeType.FunctionStub;
        }
    }
}
