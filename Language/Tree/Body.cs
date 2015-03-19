using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class Body : Statement
    {
        public List<Statement> Statements;

        public Body() : base(ASTNodeType.Body) { }
    }
}
