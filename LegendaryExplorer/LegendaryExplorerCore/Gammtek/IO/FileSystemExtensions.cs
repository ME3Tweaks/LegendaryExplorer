using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LegendaryExplorerCore.Gammtek.IO;

public static class FileSystemExtensions
{
    extension(File)
    {
        public static long GetSize(string path)
        {
            return new FileInfo(path).Length;
        }
    }
}
