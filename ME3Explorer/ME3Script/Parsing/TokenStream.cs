using ME3Script.Language;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    internal struct ASTCacheEntry
    {
        public ASTNode AST;
        public int EndIndex;
    }

    public class TokenStream<T> : TokenizableDataStream<Token<T>> 
        where T : class
    {
        private readonly Dictionary<int, ASTCacheEntry> Cache;

        public TokenStream(LexerBase<T> lexer) : base (() => lexer.LexData().ToList())
        {
            Cache = new Dictionary<int, ASTCacheEntry>();
        }

        public bool TryRoute(Func<ASTNode> nodeParser)
        {
            PushSnapshot();
            bool valid = false;

            var startIndex = CurrentIndex;
            var tree = nodeParser();
            if (tree != null)
            {
                valid = true;
                Cache[startIndex] = new ASTCacheEntry { AST = tree, EndIndex = CurrentIndex };
            }

            PopSnapshot();
            return valid;
        }

        public ASTNode GetTree(Func<ASTNode> nodeParser)
        {
            if (Cache.TryGetValue(CurrentIndex, out ASTCacheEntry entry))
            {
                CurrentIndex = entry.EndIndex;
                return entry.AST;
            }

            return nodeParser();
        }

        public ASTNode TryGetTree(Func<ASTNode> nodeParser)
        {
            return TryRoute(nodeParser) ? GetTree(nodeParser) : null;
        }

        public Token<T> ConsumeToken(TokenType type)
        {
            Token<T> token = null;
            if (CurrentItem.Type == type)
            {
                token = CurrentItem;
                Advance();
            }
            return token;
        }

        public override Token<T> LookAhead(int reach)
        {
            return base.LookAhead(reach) ?? new Token<T>(TokenType.EOF);
        }

        public override Token<T> CurrentItem => AtEnd() ? new Token<T>(TokenType.EOF) : base.CurrentItem;
    }
}
