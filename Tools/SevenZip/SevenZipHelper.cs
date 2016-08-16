using System;
using System.IO;
using System.Threading.Tasks;

namespace SevenZip.Compression.LZMA
{
    public static class SevenZipHelper
    {
		static int dictionary = 1 << 16;
		//static Int32 posStateBits = 2;
		//static Int32 litContextBits = 3; // for normal files
        //UInt32 litContextBits = 0; // for 32-bit data
		//static Int32 litPosBits = 0;
        //UInt32 litPosBits = 2; // for 32-bit data
		//static Int32 algorithm = 2;
		//static Int32 numFastBytes = 128;

		static bool eos = false;

		static CoderPropID[] propIDs =
				{
					CoderPropID.DictionarySize,
					CoderPropID.PosStateBits,
					CoderPropID.LitContextBits,
					CoderPropID.LitPosBits,
					CoderPropID.Algorithm,
					CoderPropID.NumFastBytes,
					CoderPropID.MatchFinder,
					CoderPropID.EndMarker
				};

        // these are the default properties, keeping it simple for now:
		static object[] properties =
				{
                    dictionary,
                    2,
                    3,
                    0,
                    2,
                    16,
					"bt4",
					eos
				};


        public static byte[] Compress(byte[] inputBytes)
        {
            MemoryStream inStream = new MemoryStream(inputBytes);
            MemoryStream outStream = new MemoryStream();
            SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(outStream);
            /*long fileSize = inStream.Length;
            for (int i = 0; i < 8; i++)
                outStream.WriteByte((Byte)(fileSize >> (8 * i)));*/
            encoder.Code(inStream, outStream, inStream.Length, -1, null);
            return outStream.ToArray();
        }

        public static byte[] Decompress(byte[] inputBytes, long decompressedSize)
        {
            var compressed = new MemoryStream(inputBytes);
            var decoder = new Decoder();

            var properties2 = new byte[5];
            if (compressed.Read(properties2, 0, 5) != 5)
            {
                throw (new Exception("input .lzma is too short"));
            }

            decoder.SetDecoderProperties(properties2);

            var compressedSize = compressed.Length - compressed.Position;
            var decompressed = new MemoryStream();
            decoder.Code(compressed, decompressed, compressedSize, decompressedSize, null);

            if (decompressed.Length != decompressedSize)
                throw new Exception("Decompression Error");

            return decompressed.ToArray();
        }

        public static Task<byte[]> DecompressAsync(byte[] inputBytes, int outSize)
        {
            return Task.Run(() =>
            {
                MemoryStream newInStream = new MemoryStream(inputBytes);

                Decoder decoder = new Decoder();

                newInStream.Seek(0, 0);

                byte[] properties2 = new byte[5];
                if (newInStream.Read(properties2, 0, 5) != 5)
                    throw (new Exception("input .lzma is too short"));
                decoder.SetDecoderProperties(properties2);

                long compressedSize = newInStream.Length - newInStream.Position;
                MemoryStream newOutStream = new MemoryStream();
                decoder.Code(newInStream, newOutStream, compressedSize, outSize, null);
                if (newOutStream.Length != outSize)
                    throw new Exception("Decompression Error");
                return newOutStream.ToArray();
            });
        }

        public static void SetFastByte(Int32 val)
		{
			if(val < 5) val = 5;
			if(val > 273) val = 273;
			properties[5] = val;
		}

    }
}
