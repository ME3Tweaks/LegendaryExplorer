using System;
using System.Collections;
using System.Collections.Generic;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    public sealed class TokenStream : IEnumerable<ScriptToken>
    {
        private readonly List<ScriptToken> Data;
        private readonly Stack<int> Snapshots;
        private readonly ScriptToken EndToken;
        private int CurrentIndex;
        public LineLookup LineLookup { get; }
        public Dictionary<int, ScriptToken> Comments;
        public readonly List<(ASTNode node, int offset, int length)> DefinitionLinks;

        public ReadOnlySpan<ScriptToken> TokensSpan => Data.AsSpan();

        private TokenStream(List<ScriptToken> tokens)
        {
            CurrentIndex = 0;
            Snapshots = new Stack<int>();
            Data = tokens;
            int endPos = Data.Count > 0 ? Data[^1].EndPos : 0;
            EndToken = new ScriptToken(TokenType.EOF, default, endPos, endPos);
        }

        public TokenStream(List<ScriptToken> tokens, LineLookup lineLookup) : this(tokens)
        {

            LineLookup = lineLookup;

            DefinitionLinks = new();
        }

        public TokenStream(List<ScriptToken> tokens, TokenStream parent) : this(tokens)
        {
            LineLookup = parent.LineLookup;
            DefinitionLinks = parent.DefinitionLinks;
        }

        public ScriptToken ConsumeToken(TokenType type)
        {
            ScriptToken token = null;
            if (CurrentItem.Type == type)
            {
                token = CurrentItem;
                Advance();
            }
            return token;
        }

        public ScriptToken LookAhead(int reach)
        {
            return EndOfStream(reach) ? EndToken : Data[CurrentIndex + reach];
        }

        public ScriptToken CurrentItem => CurrentIndex >= Data.Count ? EndToken : Data[CurrentIndex];


        public List<ScriptToken> GetTokensInRange(int start, int end)
        {
            var tokens = new List<ScriptToken>();
            foreach (ScriptToken token in Data)
            {
                if (token.StartPos >= start)
                {
                    tokens.Add(token);

                    if (token.EndPos >= end)
                    {
                        break;
                    }
                }
            }
            return tokens;
        }

        public int GetIndexOfTokenAtOffset(int offset)
        {
            return TokensSpan.BinarySearch(new TokenOffsetComparer(offset));
        }

        private readonly struct TokenOffsetComparer : IComparable<ScriptToken>
        {
            private readonly int Offset;

            public TokenOffsetComparer(int offset) => Offset = offset;
            public int CompareTo(ScriptToken other)
            {
                if (Offset < other.StartPos)
                {
                    return -1;
                }
                return Offset >= other.EndPos ? 1 : 0;
            }
        }

        public List<ScriptToken> GetRestOfScope()
        {
            int startIndex = CurrentIndex;
            int nestedLevel = 1;
            int i;
            for (i = CurrentIndex; i < Data.Count; i++)
            {
                switch (Data[i].Type)
                {
                    case TokenType.LeftBracket:
                        nestedLevel++;
                        break;
                    case TokenType.RightBracket:
                        nestedLevel--;
                        break;
                }

                if (nestedLevel <= 0)
                {
                    CurrentIndex = i;
                    return Data.GetRange(startIndex, CurrentIndex - startIndex);
                }
            }
            CurrentIndex = i;
            return null;
        }

        public void PushSnapshot()
        {
            Snapshots.Push(CurrentIndex);
        }

        public void DiscardSnapshot()
        {
            Snapshots.Pop();
        }

        public void PopSnapshot()
        {
            CurrentIndex = Snapshots.Pop();
        }

        public ScriptToken Prev(int lookBack = 1)
        {
            return CurrentIndex - lookBack < 0 ? null : Data[CurrentIndex - lookBack];
        }

        public void Advance(int num = 1)
        {
            CurrentIndex += num;
        }

        public bool AtEnd()
        {
            return CurrentIndex >= Data.Count;
        }

        private bool EndOfStream(int ahead = 0)
        {
            return CurrentIndex + ahead >= Data.Count;
        }

        public IEnumerator<ScriptToken> GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Data.GetEnumerator();
        }

        public void AddDefinitionLink(ASTNode node, int offset, int length)
        {
            DefinitionLinks.Add((node, offset, length));
        }

        public void AddDefinitionLink(ASTNode node, ScriptToken token)
        {
            DefinitionLinks.Add((node, token.StartPos, token.Length));
        }
    }
}
