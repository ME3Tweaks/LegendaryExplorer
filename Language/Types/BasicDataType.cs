using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Types
{
    public class BasicDataType : AbstractType
    {
        public TokenType Type { get; private set; }

        public BasicDataType(String name, TokenType type)
            : base(name)
        {
            Type = type;
        }
    }
}
