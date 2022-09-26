using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Audio
{
    /// <summary>
    /// Class identifier for Ogg-encoded data
    /// </summary>
    public class OggWaveStream : MemoryStream
    {
        public OggWaveStream(byte[] buffer) : base(buffer) { }
    }
}
