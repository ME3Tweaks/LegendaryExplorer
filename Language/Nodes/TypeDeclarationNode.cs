using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Nodes
{
    public abstract class TypeDeclarationNode : AbstractSyntaxTree
    {
        public String TypeName { get; protected set; }

        public TypeDeclarationNode(TokenType type, String typeName) : base(type)
        {
            TypeName = typeName;
        }
    }
}
