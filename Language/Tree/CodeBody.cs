using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class CodeBody : Statement
    {
        public List<Statement> Statements;

        public CodeBody(List<Statement> contents) : base(ASTNodeType.CodeBody) 
        {
            Statements = contents;
        }
    }
}
