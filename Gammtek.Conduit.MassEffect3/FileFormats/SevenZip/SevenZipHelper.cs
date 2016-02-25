using System;
using System.IO;
using MassEffect3.FileFormats.SevenZip.Compress.LZMA;

namespace MassEffect3.FileFormats.SevenZip
{
	public static class SevenZipHelper
	{
		private const int Dictionary = 1 << 16;
		//static Int32 posStateBits = 2;
		//static Int32 litContextBits = 3; // for normal files
		//UInt32 litContextBits = 0; // for 32-bit data
		//static Int32 litPosBits = 0;
		//UInt32 litPosBits = 2; // for 32-bit data
		//static Int32 algorithm = 2;
		//static Int32 numFastBytes = 128;

		private const bool Eos = false;

		private static readonly CoderPropId[] PropIDs =
		{
			CoderPropId.DictionarySize,
			CoderPropId.PosStateBits,
			CoderPropId.LitContextBits,
			CoderPropId.LitPosBits,
			CoderPropId.Algorithm,
			CoderPropId.NumFastBytes,
			CoderPropId.MatchFinder,
			CoderPropId.EndMarker
		};

		// these are the default properties, keeping it simple for now:
		private static readonly object[] Properties =
		{
			Dictionary,
			2,
			3,
			0,
			2,
			16,
			"bt4",
			Eos
		};

		public static byte[] Compress(byte[] inputBytes)
		{
			var inStream = new MemoryStream(inputBytes);
			var outStream = new MemoryStream();
			var encoder = new Encoder();

			encoder.SetCoderProperties(PropIDs, Properties);
			encoder.WriteCoderProperties(outStream);

			/*long fileSize = inStream.Length;
            for (int i = 0; i < 8; i++)
                outStream.WriteByte((Byte)(fileSize >> (8 * i)));*/

			encoder.Code(inStream, outStream, inStream.Length, -1, null);

			return outStream.ToArray();
		}

		public static byte[] Decompress(byte[] inputBytes, int outSize)
		{
			var newInStream = new MemoryStream(inputBytes);
			var decoder = new Decoder();

			newInStream.Seek(0, 0);

			var newOutStream = new MemoryStream();
			var properties2 = new byte[5];

			if (newInStream.Read(properties2, 0, 5) != 5)
			{
				throw (new Exception("input .lzma is too short"));
			}

			decoder.SetDecoderProperties(properties2);

			var compressedSize = newInStream.Length - newInStream.Position;
			decoder.Code(newInStream, newOutStream, compressedSize, outSize, null);

			var b = newOutStream.ToArray();

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
			if (val < 5)
			{
				val = 5;
			}

			if (val > 273)
			{
				val = 273;
			}

			Properties[5] = val;
		}
	}
}