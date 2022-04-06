using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public class BreakPoint : IEquatable<BreakPoint>
    {
        public readonly ScriptDatabaseEntry FunctionDBEntry;

        public ushort Position { get; }

        public string FullFunctionPath => FunctionDBEntry.FullFunctionPath;

        //public bool IsEnabled;

        public BreakPoint(ScriptDatabaseEntry dbEntry, ushort position)
        {
            FunctionDBEntry = dbEntry;
            Position = position;
        }

        public bool Equals(BreakPoint other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Position == other.Position && string.Equals(FullFunctionPath, other.FullFunctionPath, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BreakPoint)obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Position);
            hashCode.Add(FullFunctionPath, StringComparer.OrdinalIgnoreCase);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(BreakPoint left, BreakPoint right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BreakPoint left, BreakPoint right)
        {
            return !Equals(left, right);
        }
    }
}
