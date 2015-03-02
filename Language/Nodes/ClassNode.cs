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
        private List<TokenType> Specifiers;
        public String Parent { get; private set; }

        public List<AbstractSyntaxTree> Properties { get; private set; }

        public ClassNode(TokenType type, String name, String parent, List<TokenType> specifiers) : base(type, name)
        {
            Parent = parent;
            Specifiers = specifiers;
        }

        public void AddProperties(List<AbstractSyntaxTree> props)
        {
            // TODO: handle overriding etc?
            Properties = props;
        }

        public bool HasSpecifier(TokenType spec)
        {
            return Specifiers.Contains(spec);
        }
    }
}
