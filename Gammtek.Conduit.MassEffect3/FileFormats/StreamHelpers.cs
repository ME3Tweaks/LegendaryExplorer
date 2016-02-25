using System;
using System.IO;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.FileFormats
{
	public static class StreamHelpers
	{
		public static bool forceBigEndian { get; set; }

		public static FileNameHash ReadFileNameHash(this Stream stream)
		{
			var a = stream.ReadUInt32(ByteOrder.BigEndian);
			var b = stream.ReadUInt32(ByteOrder.BigEndian);
			var c = stream.ReadUInt32(ByteOrder.BigEndian);
			var d = stream.ReadUInt32(ByteOrder.BigEndian);
			return new FileNameHash(a, b, c, d);
		}

		public static void WriteFileNameHash(this Stream stream, FileNameHash hash)
		{
			stream.WriteUInt32(hash.A, ByteOrder.BigEndian);
			stream.WriteUInt32(hash.B, ByteOrder.BigEndian);
			stream.WriteUInt32(hash.C, ByteOrder.BigEndian);
			stream.WriteUInt32(hash.D, ByteOrder.BigEndian);
		}

		internal static bool ShouldSwap(bool littleEndian)
		{
			if (littleEndian && BitConverter.IsLittleEndian == false)
			{
				return !forceBigEndian;
			}
			if (littleEndian == false && BitConverter.IsLittleEndian)
			{
				return !forceBigEndian;
			}

			return forceBigEndian;
		}

		public static MemoryStream ReadToMemoryStream(this Stream stream, long size)
		{
			var memory = new MemoryStream();

			var left = size;
			var data = new byte[4096];
			while (left > 0)
			{
				var block = (int) (Math.Min(left, 4096));
				stream.Read(data, 0, block);
				memory.Write(data, 0, block);
				left -= block;
			}

			memory.Seek(0, SeekOrigin.Begin);
			return memory;
		}

		public static void WriteFromStream(this Stream stream, Stream input, long size)
		{
			var left = size;
			var data = new byte[4096];
			while (left > 0)
			{
				var block = (int) (Math.Min(left, 4096));
				input.Read(data, 0, block);
				stream.Write(data, 0, block);
				left -= block;
			}
		}
	}
}
