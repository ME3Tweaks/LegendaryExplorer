using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public class TokenStream<T> : TokenizableDataStream<Token<T>> 
        where T : class
    {
        public TokenStream(LexerBase<T> lexer) : base (() => lexer.LexData().ToList())
        {

        }
    }
}
