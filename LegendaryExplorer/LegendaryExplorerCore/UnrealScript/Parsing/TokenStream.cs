using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    public sealed class TokenStream : IEnumerable<ScriptToken>
    {
        private readonly List<ScriptToken> Data;
        private readonly Stack<int> Snapshots;
        private readonly ScriptToken EndToken;
        private int CurrentIndex;

        public TokenStream(List<ScriptToken> tokens)
        {
            CurrentIndex = 0;
            Snapshots = new Stack<int>();
            Data = tokens;
            SourcePosition endPos = Data.Count > 0 ? Data[^1].EndPos : new SourcePosition(0, 0, 0);
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

        public List<ScriptToken> GetTokensInRange(SourcePosition start, SourcePosition end)
        {
            var tokens = new List<ScriptToken>();
            for (int i = 0; i < Data.Count; i++)
            {
                ScriptToken token = Data[i];
                if (token.StartPos.CharIndex >= start.CharIndex)
                {
                    tokens.Add(token);

                    if (token.EndPos.CharIndex >= end.CharIndex)
                    {
                        break;
                    }
                }
            }
            return tokens;
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
