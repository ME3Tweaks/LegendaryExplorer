using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioSoundNodeWaveStreamingData : ObjectBinary
    {
        public byte[] EmbeddedICB;
        public byte[] EmbeddedISB;

        protected override void Serialize(SerializingContainer2 sc)
        {
            int totalLength = sc.IsSaving ? 4 + EmbeddedICB.Length + EmbeddedISB.Length : 0;
            sc.Serialize(ref totalLength);
            int ISBOffset = sc.IsSaving ? 4 + EmbeddedICB.Length : 0;
            sc.Serialize(ref ISBOffset);
            sc.Serialize(ref EmbeddedICB, ISBOffset - 4);
            sc.Serialize(ref EmbeddedISB, totalLength - ISBOffset);
        }

        public static BioSoundNodeWaveStreamingData Create()
        {
            return new()
            {
                EmbeddedICB = Array.Empty<byte>(),
                EmbeddedISB = Array.Empty<byte>()
            };
        }
    }
}
