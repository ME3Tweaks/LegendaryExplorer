using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    class CodeWriter : IDisposable
    {
        private readonly StreamWriter writer;
        private byte indent;
        private bool writingLine;

        public CodeWriter(Stream stream)
        {
            writer = new StreamWriter(stream);
        }

        public void IncreaseIndent(byte amount = 1)
        {
            indent += amount;
        }

        public void DecreaseIndent(byte amount = 1)
        {
            if (amount > indent)
            {
                throw new InvalidOperationException("Cannot have a negative indent!");
            }
            indent -= amount;
        }

        public void Write(string text)
        {
            if (writingLine)
            {
                writer.Write(text);
            }
            else
            {
                writingLine = true;
                writer.Write($"{new string(' ', indent * 4)}{text}");
            }
        }

        public void WriteLine(string line)
        {
            if (writingLine)
            {
                writer.WriteLine(line);
                writingLine = false;
            }
            else
            {
                writer.WriteLine($"{new string(' ', indent * 4)}{line}");
            }
        }

        public void WriteLine()
        {
            writingLine = false;
            writer.WriteLine();
        }

        public void WriteBlock(string header, Action contents)
        {
            WriteLine(header);
            WriteLine("{");
            IncreaseIndent();
            contents();
            DecreaseIndent();
            WriteLine("}");
        }

        public void Dispose()
        {
            writer.Dispose();
        }
    }
}
