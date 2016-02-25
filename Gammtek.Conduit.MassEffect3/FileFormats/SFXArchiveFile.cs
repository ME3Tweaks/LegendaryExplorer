using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using MassEffect3.FileFormats.SFXArchive;

namespace MassEffect3.FileFormats
{
	public class SFXArchiveFile
	{
		public List<uint> BlockSizes
			= new List<uint>();

		public CompressionScheme CompressionScheme;
		public ByteOrder Endian;

		public List<Entry> Entries
			= new List<Entry>();

		public uint MaximumBlockSize;

		public void Serialize(Stream output)
		{
			throw new NotImplementedException();
		}

		public void Deserialize(Stream input)
		{
			var magic = input.ReadUInt32();
			if (magic != 0x53464152 && // SFAR
				magic.Swap() != 0x53464152)
			{
				throw new FormatException();
			}
			var endian = magic == 0x53464152 ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

			var version = input.ReadUInt32(endian);
			if (version != 0x00010000)
			{
				throw new FormatException();
			}

			var dataOffset = input.ReadUInt32(endian);
			var fileTableOffset = input.ReadUInt32(endian);
			var fileTableCount = input.ReadUInt32(endian);
			var blockSizeTableOffset = input.ReadUInt32(endian);
			MaximumBlockSize = input.ReadUInt32(endian);
			CompressionScheme = input.ReadEnum<CompressionScheme>(endian);

			if (fileTableOffset != 0x20)
			{
				throw new FormatException();
			}

			if (MaximumBlockSize != 0x010000)
			{
				throw new FormatException();
			}

			/*
            if (this.CompressionScheme != SFXArchive.CompressionScheme.None &&
                this.CompressionScheme != SFXArchive.CompressionScheme.LZMA &&
                this.CompressionScheme != SFXArchive.CompressionScheme.LZX)
            {
                throw new FormatException();
            }
            */

			input.Seek(blockSizeTableOffset, SeekOrigin.Begin);

			var blockSizeTableSize = dataOffset - fileTableOffset;
			var blockSizeTableCount = blockSizeTableSize / 2;
			BlockSizes.Clear();
			for (uint i = 0; i < blockSizeTableCount; i++)
			{
				BlockSizes.Add(input.ReadUInt16(endian));
			}

			input.Seek(fileTableOffset, SeekOrigin.Begin);
			for (uint i = 0; i < fileTableCount; i++)
			{
				// ReSharper disable UseObjectOrCollectionInitializer
				var entry = new Entry();
				// ReSharper restore UseObjectOrCollectionInitializer
				entry.NameHash = input.ReadFileNameHash();
				entry.BlockSizeIndex = input.ReadInt32(endian);
				entry.UncompressedSize = input.ReadUInt32(endian);
				entry.UncompressedSize |= ((long) input.ReadUInt8()) << 32;
				entry.Offset = input.ReadUInt32(endian);
				entry.Offset |= ((long) input.ReadUInt8()) << 32;
				Entries.Add(entry);
			}
		}
	}
}