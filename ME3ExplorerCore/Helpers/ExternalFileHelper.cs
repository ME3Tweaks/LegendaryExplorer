using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ME3ExplorerCore.Helpers
{
    public class ExternalFileHelper
    {
        /// <summary>
        /// Reads data from a file at the specified offset, of specified size into a stream and returns the stream. If no stream is specified, a new memory stream is created.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Stream ReadExternalData(string filePath, long offset, int size, Stream outStream = null)
        {
            if (!File.Exists(filePath))
                return null;

            using Stream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (offset + size > fs.Length)
                return null; //invalid pointer, outside bounds

            outStream ??= new MemoryStream();
            fs.Seek(offset, SeekOrigin.Begin);
            var startPos = outStream.Position;
            fs.CopyToEx(outStream, size);
            outStream.Position = startPos;
            return outStream;
        }
    }
}
