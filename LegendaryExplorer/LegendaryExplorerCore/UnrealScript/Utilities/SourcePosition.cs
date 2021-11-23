using System;
using System.Collections.Generic;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public sealed class SourcePosition: IComparable<SourcePosition>, IComparable
    {
        public readonly int Line;
        public readonly int Column;
        public readonly int CharIndex;

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

        #region IComparable

        public int CompareTo(SourcePosition other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return CharIndex.CompareTo(other.CharIndex);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            return obj is SourcePosition other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(SourcePosition)}");
        }

        public static bool operator <(SourcePosition left, SourcePosition right)
        {
            return Comparer<SourcePosition>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(SourcePosition left, SourcePosition right)
        {
            return Comparer<SourcePosition>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(SourcePosition left, SourcePosition right)
        {
            return Comparer<SourcePosition>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(SourcePosition left, SourcePosition right)
        {
            return Comparer<SourcePosition>.Default.Compare(left, right) >= 0;
        }

        #endregion
    }
}
