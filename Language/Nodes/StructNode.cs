using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Nodes
{
    public class StructNode : TypeDeclarationNode
    {
        private List<TokenType> Specifiers;
        public List<VariableNode> Contents;

        public StructNode(String name, List<VariableNode> contents, List<TokenType> specifiers)
            : base(TokenType.Struct, name)
        {
            Specifiers = specifiers;
            Contents = contents;
        }

        public bool HasSpecifier(TokenType spec)
        {
            return Specifiers.Contains(spec);
        }
    }
}
