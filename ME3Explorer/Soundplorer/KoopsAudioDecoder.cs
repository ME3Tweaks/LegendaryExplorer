using Gammtek.Conduit.Extensions.IO;
using System;
using System.IO;
using System.Text;

namespace ME3Explorer.Soundplorer
{
    class KoopsAudioDecoder
    {
        static int coeff1, coeff2, shift, adpcmHistory1 = 0, adpcmHistory2 = 0;

        static readonly int[] EA_XA_TABLE = new int[] {
        0,    0,
      240,    0,
      460, -208,
      392, -220,
    };

        static void DecodeSingleFrame(MemoryStream stream, MemoryStream outbuf)
        {
            int frameInfo = stream.ReadByte();
            int shifted = (frameInfo >> 4) & 15;
            coeff1 = EA_XA_TABLE[((frameInfo >> 4) & 15) * 2];
            coeff2 = EA_XA_TABLE[((frameInfo >> 4) & 15) * 2 + 1];
            shift = (frameInfo & 15) + 8;

            for (int i = 0; i < 14; i++)
            {
                int sample_byte = stream.ReadByte();

                int[] nibbles = { sample_byte >> 4, sample_byte & 15 };

                foreach (int nibble in nibbles)
                {
                    int sample = GetSample(nibble);

                    outbuf.WriteInt16(Clamp16(sample));
                }
            }
        }

        private static int GetSample(int nibble)
        {
            int sample = ((nibble << 28 >> shift) + (coeff1 * adpcmHistory1) + (coeff2 * adpcmHistory2)) >> 8;

            adpcmHistory2 = adpcmHistory1;
            adpcmHistory1 = sample;

            return sample;
        }

        static private short Clamp16(int sample)
        {
            if (sample > 32767)
            {
                return 32767;
            }

            if (sample < -32768)
            {
                return -32768;
            }

            return (short)sample;
        }

        public static MemoryStream Decode(MemoryStream inputStream)
        {
            MemoryStream outStream = new MemoryStream();
            while (inputStream.Length - inputStream.Position >= 15)
            {
                DecodeSingleFrame(inputStream, outStream);
            }
            return outStream;
        }
    }
}
