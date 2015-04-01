using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public abstract class OperatorDeclaration : Expression
    {
        public String OperatorKeyword;
        public bool isDelimiter;
        public CodeBody Body;
        public VariableType ReturnType;

        public OperatorDeclaration(ASTNodeType type, String keyword, 
            bool delim, CodeBody body, VariableType returnType,
            SourcePosition start, SourcePosition end) 
            : base(type, start, end)
        {
            OperatorKeyword = keyword;
            isDelimiter = delim;
            Body = body;
            ReturnType = returnType;
        }
    }
}
