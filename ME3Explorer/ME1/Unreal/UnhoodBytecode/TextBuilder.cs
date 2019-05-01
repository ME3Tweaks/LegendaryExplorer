using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{
    public class TextBuilder
    {
        private class OffsetMapEntry
        {
            private int _textStart;
            private int _textEnd;
            private int _bytecodeStart;
            private int _bytecodeEnd;

            public OffsetMapEntry(int textStart, int textEnd, int bytecodeStart, int bytecodeEnd)
            {
                _textStart = textStart;
                _textEnd = textEnd;
                _bytecodeStart = bytecodeStart;
                _bytecodeEnd = bytecodeEnd;
            }

            public int TextStart
            {
                get { return _textStart; }
            }

            public int TextEnd
            {
                get { return _textEnd; }
            }

            public int BytecodeStart
            {
                get { return _bytecodeStart; }
            }

            public int BytecodeEnd
            {
                get { return _bytecodeEnd; }
            }
        }

        private readonly StringBuilder _builder = new StringBuilder();
        private readonly List<OffsetMapEntry> _offsetMap = new List<OffsetMapEntry>();
        private int _indent;

        public bool HasErrors { get; set; }

        public TextBuilder Append(string text)
        {
            _builder.Append(text.Replace("\n", "\r\n"));
            return this;
        }

        public TextBuilder Append(string text, int startOffset, int endOffset)
        {
            int textStart = _builder.Length;
            Append(text);
            if (endOffset != -1)
            {
                _offsetMap.Add(new OffsetMapEntry(textStart, _builder.Length, startOffset, endOffset));
            }
            return this;
        }

        public TextBuilder Append(int value)
        {
            _builder.Append(value);
            return this;
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        public void PushIndent()
        {
            _indent++;
        }

        public void PopIndent()
        {
            _indent--;
        }

        public TextBuilder Indent()
        {
            _builder.Append(' ', _indent * 4);
            return this;
        }

        public int GetIndent()
        {
            return _indent;
        }

        public TextBuilder NewLine()
        {
            _builder.Append("\r\n");
            return this;
        }

        public bool GetOffsets(int textOffset, out int bytecodeStartOffset, out int bytecodeEndOffset)
        {
            var entry = _offsetMap.Find(e => e.TextStart <= textOffset && e.TextEnd >= textOffset);
            if (entry == null)
            {
                bytecodeStartOffset = -1;
                bytecodeEndOffset = -1;
                return false;
            }
            bytecodeStartOffset = entry.BytecodeStart;
            bytecodeEndOffset = entry.BytecodeEnd;
            return true;
        }
    }
}
