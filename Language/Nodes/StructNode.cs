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
        public ScopeNode Contents;
        public String Parent { get; private set; }

        public StructNode(String name, ScopeNode contents, String parent, List<TokenType> specifiers)
            : base(TokenType.Struct, name)
        {
            Specifiers = specifiers;
            Contents = contents;
            Parent = parent;
        }

        public bool HasSpecifier(TokenType spec)
        {
            return Specifiers.Contains(spec);
        }
    }
}
