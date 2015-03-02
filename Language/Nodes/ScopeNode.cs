using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Nodes
{
    public class ScopeNode : AbstractSyntaxTree
    {
        public List<AbstractSyntaxTree> Contents { get; protected set; }

        public ScopeNode(List<AbstractSyntaxTree> contents) : base(TokenType.Scope)
        {
            Contents = contents;
        }
    }
}
