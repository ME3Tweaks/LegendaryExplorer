using System;
using System.IO;

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
					(Int32)(dictionary),
					(Int32)(2),
					(Int32)(3),
					(Int32)(0),
					(Int32)(2),
					(Int32)(16),
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

        public static byte[] Decompress(byte[] inputBytes, int outSize)
        {
            MemoryStream newInStream = new MemoryStream(inputBytes);

            SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();

            newInStream.Seek(0, 0);
            MemoryStream newOutStream = new MemoryStream();

            byte[] properties2 = new byte[5];
            if (newInStream.Read(properties2, 0, 5) != 5)
                throw (new Exception("input .lzma is too short"));
            decoder.SetDecoderProperties(properties2);

            long compressedSize = newInStream.Length - newInStream.Position;
            decoder.Code(newInStream, newOutStream, compressedSize, outSize, null);

            byte[] b = newOutStream.ToArray();
			
			/*if(b.Length > outSize)
			{
				byte[] output = new byte[outSize];
				Array.Copy(b,output,outSize);
				return output;
			}*/

            return b;
        }

		public static void SetFastByte(Int32 val)
		{
			if(val < 5) val = 5;
			if(val > 273) val = 273;
			properties[5] = val;
		}

    }
}
