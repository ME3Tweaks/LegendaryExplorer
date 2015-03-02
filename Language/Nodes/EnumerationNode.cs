using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Nodes
{
    public class EnumerationNode : TypeDeclarationNode
    {
        public List<String> Values { get; private set; }

        public EnumerationNode(TokenType type, String name, List<String> values) : base(type, name)
        {
            Values = values;
        }

        public bool HasValue(String name)
        {
            return Values.Contains(name);
        }

        public int ValueIndex(String value)
        {
            return Values.FindIndex(str => str == value);
        }
    }
}
