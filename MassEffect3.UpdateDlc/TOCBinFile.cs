using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gammtek.Conduit.IO;

namespace MassEffect3.UpdateDlc
{
	public class TocBinFile
	{
		private readonly MemoryStream _memory;
		private ByteOrderConverter _converter;

		public TocBinFile(MemoryStream memory)
		{
			_memory = memory;

			ReadFile();
		}

		public TocBinFile(string path)
		{
			_memory = new MemoryStream(File.ReadAllBytes(path).ToArray());

			ReadFile();
		}

		public List<Entry> Entries { get; set; }

		public void ReadFile()
		{
			if (_memory == Stream.Null)
			{
				return;
			}

			_converter = ByteOrderConverter.LittleEndian;

			_memory.Seek(0, 0);

			var magic = (uint) ReadInt(_memory);

			if (magic != 0x3AB70C13)
			{
				//DebugOutput.PrintLn("Not a SFAR File");
				return;
			}

			_memory.Seek(8, 0);
			var count = ReadInt(_memory);
			_memory.Seek(0xC + 8*count, 0);
			Entries = new List<Entry>();
			int blocksize;
			var pos = (int) _memory.Position;

			do
			{
				var e = new Entry {Offset = pos};

				_memory.Seek(pos, 0);
				blocksize = ReadInt16(_memory);
				_memory.Seek(pos + 0x4, 0);
				e.Size = ReadInt(_memory);
				_memory.Seek(pos + 0x1C, 0);
				e.Name = ReadString(_memory);
				pos += blocksize;
				Entries.Add(e);
			} while (blocksize != 0);
		}

		private int ReadInt(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}

			var buff = new byte[4];
			stream.Read(buff, 0, 4);

			//return EndianBitConverter.ToInt32(buff, 0);
			return _converter.ToInt32(buff, 0);
		}

		private ushort ReadInt16(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}

			var buff = new byte[2];
			stream.Read(buff, 0, 2);

			//return EndianBitConverter.ToUInt16(buff, 0);
			return _converter.ToUInt16(buff, 0);
		}

		public string ReadString(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}

			var s = "";
			byte b;

			while ((b = (byte) stream.ReadByte()) != 0)
			{
				s += (char) b;
			}

			return s;
		}

		public void UpdateEntry(int index, int size)
		{
			if (Entries == null || index < 0 || index >= Entries.Count)
			{
				return;
			}

			var e = Entries[index];
			e.Size = size;
			Entries[index] = e;
		}

		public MemoryStream Save()
		{
			//EndianBitConverter.IsLittleEndian = true;

			foreach (var e in Entries)
			{
				_memory.Seek(e.Offset + 4, 0);
				//_memory.Write(EndianBitConverter.GetBytes(e.Size), 0, 4);
				_memory.Write(_converter.GetBytes(e.Size), 0, 4);
			}

			return _memory;
		}

		public void DebugPrint()
		{
			//var count = 0;
			//DebugOutput.PrintLn("Listing Files...(" + Entries.Count + ")");
			//foreach (Entry e in Entries)
			//	DebugOutput.PrintLn((count++) + " : " + e.name + " Size: " + DLCPackage.BytesToString(e.size), false);
			//DebugOutput.Update();
		}

		public struct Entry
		{
			public string Name { get; set; }
			public int Offset { get; set; }
			public int Size { get; set; }
		}
	}
}