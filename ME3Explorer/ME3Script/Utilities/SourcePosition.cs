using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Utilities
{
    public class SourcePosition
    {
        public int Line { get; private set; }
        public int Column { get; private set; }
        public int CharIndex { get; private set; }

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

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;

            SourcePosition p = obj as SourcePosition;
            if ((Object)p == null)
                return false;

            return (Line == p.Line) && (Column == p.Column) && (CharIndex == p.CharIndex);
        }

        public bool Equals(SourcePosition p)
        {
            if ((object)p == null)
                return false;

            return (Line == p.Line) && (Column == p.Column) && (CharIndex == p.CharIndex);
        }
    }
}
