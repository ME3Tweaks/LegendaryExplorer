using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Nodes
{
    public class ClassNode : TypeDeclarationNode
    {
        public ClassNode(TokenType type, String name) : base(type, name)
        {

        }
    }
}
