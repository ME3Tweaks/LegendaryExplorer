using System.Collections.Generic;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    public sealed class LineLookup
    {
        public readonly List<int> Lines;

        public LineLookup(List<int> lines)
        {
            Lines = lines;
        }

        public int GetLineFromCharIndex(int charIndex)
        {
            int arrIdx = Lines.BinarySearch(charIndex);
            
            if (arrIdx < 0)
            {
                arrIdx = ~arrIdx;
                if (arrIdx == 0)
                {
                    arrIdx++;
                }
            }
            else
            {
                arrIdx++;
            }

            return arrIdx;
        }

        public int GetColumnFromCharIndex(int charIndex)
        {
            return charIndex - Lines[GetLineFromCharIndex(charIndex) - 1];
        }

        public (int, int) GetLineandColumnFromCharIndex(int charIndex)
        {
            int line = GetLineFromCharIndex(charIndex);
            return (line, charIndex - Lines[line - 1]);
        }
    }
}