using System;
using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.UnrealScript.Language.Tree;
using ME3ExplorerCore.UnrealScript.Lexing;
using ME3ExplorerCore.UnrealScript.Lexing.Tokenizing;
using ME3ExplorerCore.UnrealScript.Utilities;

namespace ME3ExplorerCore.UnrealScript.Parsing
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

        private readonly Token<T> EndToken;

        private TokenStream(Func<List<Token<T>>> provider) : base(provider)
        {
            Cache = new Dictionary<int, ASTCacheEntry>();
            var endPos = Data.Count > 0 ? Data[Data.Count - 1].EndPos : new SourcePosition(0, 0, 0);
            EndToken = new Token<T>(TokenType.EOF, default, endPos, endPos);
        }

        public TokenStream(LexerBase<T> lexer) : this (() => lexer.LexData().ToList())
        {
        }

        public TokenStream(LexerBase<T> lexer, SourcePosition start, SourcePosition end) : this(() => lexer.LexSubData(start, end).ToList())
        {
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
            return base.LookAhead(reach) ?? EndToken;
        }

        public override Token<T> CurrentItem => AtEnd() ? EndToken : base.CurrentItem;

        public IEnumerable<Token<T>> GetTokensInRange(SourcePosition start, SourcePosition end)
        {
            foreach (Token<T> token in Data)
            {
                if (token.StartPos.CharIndex >= start.CharIndex)
                {
                    yield return token;

                    if (token.EndPos.CharIndex >= end.CharIndex)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
