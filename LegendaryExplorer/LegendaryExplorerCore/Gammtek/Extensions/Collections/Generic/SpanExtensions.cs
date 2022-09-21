using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic
{
    public static class SpanExtensions
    {
        public static void CopyToAndTerminate<T>(ReadOnlySpan<T> source, Span<T> destination, T terminator)
        {
            if (source.Length >= destination.Length)
            {
                ThrowHelper.ThrowArgumentException(nameof(source), "Source length must be less than destination length.");
            }

            source.CopyTo(destination);
            destination[source.Length] = terminator;
        }

        public static ReadOnlySpan<T> SliceUntil<T>(ReadOnlySpan<T> span, T value)
            where T : IEquatable<T>
        {
            int length = span.IndexOf(value);
            return length == -1 ? span : span[..length];
        }
    }
}
