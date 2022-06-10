using System;
using System.Collections;
using System.Collections.Generic;
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

        public ReadOnlySpan<ScriptToken> TokensSpan => Data.AsSpan();

        public TokenStream(List<ScriptToken> tokens, LineLookup lineLookup)
        {
            CurrentIndex = 0;
            Snapshots = new Stack<int>();
            Data = tokens;
            LineLookup = lineLookup;

            int endPos = Data.Count > 0 ? Data[^1].EndPos : 0;
            EndToken = new ScriptToken(TokenType.EOF, default, endPos, endPos);
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
    }
}
