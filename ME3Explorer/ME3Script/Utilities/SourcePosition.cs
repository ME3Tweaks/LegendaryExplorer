using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Utilities
{
    public class SourcePosition
    {
        public int Line { get; }
        public int Column { get; }
        public int CharIndex { get; }

        public SourcePosition(int ln, int col, int index)
        {
            Line = ln;
            Column = col;
            CharIndex = index;
        }

        public SourcePosition(SourcePosition pos)
        {
            Line = pos.Line;
            Column = pos.Column;
            CharIndex = pos.CharIndex;
        }

        public SourcePosition GetModifiedPosition(int ln, int col, int index)
        {
            return new SourcePosition(Line + ln, Column + col, CharIndex + index);
        }

        public override bool Equals(object obj)
        {
            if (obj is SourcePosition p)
            {
                return (Line == p.Line) && (Column == p.Column) && (CharIndex == p.CharIndex);
            }

            return false;

        }

        public bool Equals(SourcePosition p)
        {
            if (p == null)
                return false;

            return (Line == p.Line) && (Column == p.Column) && (CharIndex == p.CharIndex);
        }
    }
}
