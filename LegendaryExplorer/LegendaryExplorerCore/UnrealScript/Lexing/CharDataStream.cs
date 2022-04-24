using System.Collections.Generic;

namespace LegendaryExplorerCore.UnrealScript.Lexing
{
    public sealed class CharDataStream
    {
        #region Members
        private readonly string Data;
        private readonly Stack<int> Snapshots;
        private int _currentIndex;

        //Do not convert to auto-property! Causes performance degradation
        public int CurrentIndex => _currentIndex;

        public char CurrentItem => _currentIndex >= Data.Length ? '\0' : Data[_currentIndex];

        #endregion

        #region Public Methods
        public CharDataStream(string data)
        {
            _currentIndex = 0;
            Snapshots = new Stack<int>();
            Data = data;
        }

        public void PushSnapshot()
        {
            Snapshots.Push(_currentIndex);
        }

        public void DiscardSnapshot()
        {
            Snapshots.Pop();
        }

        public void PopSnapshot()
        {
            _currentIndex = Snapshots.Pop();
        }

        public char LookAhead(int reach)
        {
            return EndOfStream(reach) ? '\0' : Data[_currentIndex + reach];
        }

        public char Prev(int lookBack = 1)
        {
            return _currentIndex - lookBack < 0 ? '\0' : Data[_currentIndex - lookBack];
        }

        public void Advance()
        {
            ++_currentIndex;
        }

        public void Advance(int num)
        {
            _currentIndex += num;
        }

        public bool AtEnd() => _currentIndex >= Data.Length;

        public string Slice(int startIndex, int length)
        {
            return Data.Substring(startIndex, length);
        }

        #endregion

        #region Private Methods
        private bool EndOfStream(int ahead = 0)
        {
            return _currentIndex + ahead >= Data.Length;
        }
        #endregion
    }
}