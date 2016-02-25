using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Gammtek.Conduit.IO;
using MassEffect3.FileFormats.SevenZip;

namespace MassEffect3.UpdateDlc
{
	public class DlcPackage
	{
		public static readonly byte[] TocHash =
		{
			0xB5, 0x50, 0x19, 0xCB, 0xF9, 0xD3, 0xDA, 0x65, 0xD5, 0x5B, 0x32, 0x1C, 0x00,
			0x19, 0x69, 0x7C
		};

		private ByteOrderConverter _converter;

		private DlcHeader _header;

		public DlcPackage(string fileName)
		{
			Load(fileName);
		}

		public DlcFileEntry[] DlcFiles { get; private set; }

		public string FileName { get; private set; }

		public void Load(string fileName)
		{
			_converter = ByteOrderConverter.LittleEndian;

			FileName = fileName;
			var con = new SerializingFile(new FileStream(fileName, FileMode.Open, FileAccess.Read));

			Serialize(con);
			con.Memory.Close();
		}

		public void Serialize(SerializingFile con)
		{
			if (con.IsLoading)
			{
				_header = new DlcHeader();
			}

			_header.Serialize(con);
			con.Seek((int) _header.EntryOffset, SeekOrigin.Begin);

			if (con.IsLoading)
			{
				DlcFiles = new DlcFileEntry[_header.FileCount];
			}

			for (var i = 0; i < _header.FileCount; i++)
			{
				DlcFiles[i].Serialize(con, _header);
			}

			if (con.IsLoading)
			{
				ReadFileNames(con);
			}
		}

		public bool CompareByteArray(byte[] a1, byte[] a2)
		{
			if (a1.Length != a2.Length)
			{
				return false;
			}

			return !a1.Where((t, i) => t != a2[i]).Any();
		}

		public void ReadFileNames(SerializingFile con)
		{
			DlcFileEntry e;
			var f = -1;

			for (var i = 0; i < _header.FileCount; i++)
			{
				e = DlcFiles[i];
				e.FileName = "UNKNOWN";
				DlcFiles[i] = e;

				if (CompareByteArray(DlcFiles[i].Hash, TocHash))
				{
					f = i;
				}
			}

			if (f == -1)
			{
				return;
			}

			var m = DecompressEntry(f);

			m.Seek(0, 0);

			var r = new StreamReader(m);

			while (!r.EndOfStream)
			{
				var line = r.ReadLine();
				var hash = ComputeHash(line);

				f = -1;

				for (var i = 0; i < _header.FileCount; i++)
				{
					if (CompareByteArray(DlcFiles[i].Hash, hash))
					{
						f = i;
					}
				}

				if (f != -1)
				{
					e = DlcFiles[f];
					e.FileName = line;
					DlcFiles[f] = e;
				}
			}
		}

		public List<byte[]> GetBlocks(int index)
		{
			var res = new List<byte[]>();
			var e = DlcFiles[index];
			uint count = 0;
			//var outputBlock = new byte[Header.MaxBlockSize];
			var left = e.RealUncompressedSize;
			var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
			fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
			byte[] buff;

			if (e.BlockSizeIndex == 0xFFFFFFFF)
			{
				buff = new byte[e.RealUncompressedSize];
				fs.Read(buff, 0, buff.Length);
				res.Add(buff);
				fs.Close();
				return res;
			}

			while (left > 0)
			{
				uint compressedBlockSize = e.BlockSizes[count];

				if (compressedBlockSize == 0)
				{
					compressedBlockSize = _header.MaxBlockSize;
				}

				if (compressedBlockSize == _header.MaxBlockSize || compressedBlockSize == left)
				{
					buff = new byte[compressedBlockSize];
					fs.Read(buff, 0, buff.Length);
					res.Add(buff);
					left -= compressedBlockSize;
				}
				else
				{
					var uncompressedBlockSize = (uint) Math.Min(left, _header.MaxBlockSize);

					if (compressedBlockSize < 5)
					{
						throw new Exception("compressed block size smaller than 5");
					}

					var inputBlock = new byte[compressedBlockSize];
					
					fs.Read(inputBlock, 0, (int) compressedBlockSize);
					res.Add(inputBlock);
					left -= uncompressedBlockSize;
				}

				count++;
			}

			fs.Close();

			return res;
		}

		public MemoryStream DecompressEntry(int index)
		{
			var result = new MemoryStream();
			var e = DlcFiles[index];
			uint count = 0;
			var left = e.RealUncompressedSize;
			var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
			fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
			byte[] buff;

			if (e.BlockSizeIndex == 0xFFFFFFFF)
			{
				buff = new byte[e.RealUncompressedSize];
				fs.Read(buff, 0, buff.Length);
				result.Write(buff, 0, buff.Length);
			}
			else
			{
				while (left > 0)
				{
					uint compressedBlockSize = e.BlockSizes[count];

					if (compressedBlockSize == 0)
					{
						compressedBlockSize = _header.MaxBlockSize;
					}

					if (compressedBlockSize == _header.MaxBlockSize || compressedBlockSize == left)
					{
						buff = new byte[compressedBlockSize];
						fs.Read(buff, 0, buff.Length);
						result.Write(buff, 0, buff.Length);
						left -= compressedBlockSize;
					}
					else
					{
						var uncompressedBlockSize = (uint) Math.Min(left, _header.MaxBlockSize);

						if (compressedBlockSize < 5)
						{
							throw new Exception("compressed block size smaller than 5");
						}

						var inputBlock = new byte[compressedBlockSize];
						fs.Read(inputBlock, 0, (int) compressedBlockSize);
						var actualUncompressedBlockSize = uncompressedBlockSize;
						//var actualCompressedBlockSize = compressedBlockSize;
						var outputBlock = SevenZipHelper.Decompress(inputBlock, (int) actualUncompressedBlockSize);

						if (outputBlock.Length != actualUncompressedBlockSize)
						{
							throw new Exception("Decompression Error");
						}

						result.Write(outputBlock, 0, (int) actualUncompressedBlockSize);
						left -= uncompressedBlockSize;
					}

					count++;
				}
			}

			fs.Close();

			return result;
		}

		public MemoryStream DecompressEntry(int index, FileStream fs)
		{
			var result = new MemoryStream();
			var e = DlcFiles[index];
			uint count = 0;
			var left = e.RealUncompressedSize;
			fs.Seek(e.BlockOffsets[0], SeekOrigin.Begin);
			byte[] buff;

			if (e.BlockSizeIndex == 0xFFFFFFFF)
			{
				buff = new byte[e.RealUncompressedSize];
				fs.Read(buff, 0, buff.Length);
				result.Write(buff, 0, buff.Length);
			}
			else
			{
				while (left > 0)
				{
					uint compressedBlockSize = e.BlockSizes[count];

					if (compressedBlockSize == 0)
					{
						compressedBlockSize = _header.MaxBlockSize;
					}

					if (compressedBlockSize == _header.MaxBlockSize || compressedBlockSize == left)
					{
						buff = new byte[compressedBlockSize];
						fs.Read(buff, 0, buff.Length);
						result.Write(buff, 0, buff.Length);
						left -= compressedBlockSize;
					}
					else
					{
						var uncompressedBlockSize = (uint) Math.Min(left, _header.MaxBlockSize);

						if (compressedBlockSize < 5)
						{
							throw new Exception("compressed block size smaller than 5");
						}

						var inputBlock = new byte[compressedBlockSize];
						fs.Read(inputBlock, 0, (int) compressedBlockSize);
						var actualUncompressedBlockSize = uncompressedBlockSize;
						//var actualCompressedBlockSize = compressedBlockSize;
						var outputBlock = SevenZipHelper.Decompress(inputBlock, (int) actualUncompressedBlockSize);

						if (outputBlock.Length != actualUncompressedBlockSize)
						{
							throw new Exception("Decompression Error");
						}

						result.Write(outputBlock, 0, (int) actualUncompressedBlockSize);
						left -= uncompressedBlockSize;
					}

					count++;
				}
			}

			return result;
		}

		public static byte[] ComputeHash(string input)
		{
			var bytes = new byte[input.Length];

			for (var i = 0; i < input.Length; i++)
			{
				bytes[i] = (byte) Sanitize(input[i]);
			}

			var md5 = MD5.Create();

			return md5.ComputeHash(bytes);
		}

		public static char Sanitize(char c)
		{
			switch ((ushort) c)
			{
				case 0x008C:
					return (char) 0x9C;
				case 0x009F:
					return (char) 0xFF;
				case 0x00D0:
				case 0x00DF:
				case 0x00F0:
				case 0x00F7:
					return c;
			}

			if ((c >= 'A' && c <= 'Z') || (c >= 'À' && c <= 'Þ'))
			{
				return char.ToLowerInvariant(c);
			}

			return c;
		}

		public void WriteString(MemoryStream m, string s)
		{
			foreach (var c in s)
			{
				m.WriteByte((byte) c);
			}
		}

		public static string BytesToString(long byteCount)
		{
			string[] suf = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; //Longs run out around EB

			if (byteCount == 0)
			{
				return "0" + suf[0];
			}

			var bytes = Math.Abs(byteCount);
			var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
			var num = Math.Round(bytes/Math.Pow(1024, place), 1);

			return (Math.Sign(byteCount)*num) + suf[place];
		}

		public void ReBuild()
		{
			var path = Path.GetDirectoryName(FileName) + "\\" + Path.GetFileNameWithoutExtension(FileName) + ".tmp";
			var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
			//EndianBitConverter.IsLittleEndian = true;
			//DebugOutput.PrintLn("Creating Header Dummy...");

			for (var i = 0; i < 8; i++)
			{
				fs.Write(_converter.GetBytes(0), 0, 4);
			}

			_header.EntryOffset = 0x20;
			//DebugOutput.PrintLn("Creating File Table...");

			for (var i = 0; i < _header.FileCount; i++)
			{
				var e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);
				fs.Write(_converter.GetBytes(e.BlockSizeIndex), 0, 4);
				fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
				fs.WriteByte(e.UncompressedSizeAdder);
				fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
				fs.WriteByte(e.DataOffsetAdder);
			}

			_header.BlockTableOffset = (uint) fs.Position;
			//DebugOutput.PrintLn("Creating Block Table...");

			for (var i = 0; i < _header.FileCount; i++)
			{
				if (DlcFiles[i].BlockSizeIndex != 0xFFFFFFFF)
				{
					foreach (var u in DlcFiles[i].BlockSizes)
					{
						fs.Write(_converter.GetBytes(u), 0, 2);
					}
				}
			}
			_header.DataOffset = (uint) fs.Position;
			//DebugOutput.PrintLn("Appending DlcFiles...");
			var pos = (uint) fs.Position;

			for (var i = 0; i < _header.FileCount; i++)
			{
				var blocks = GetBlocks(i);
				var e = DlcFiles[i];
				/*DebugOutput.PrintLn("Rebuilding \"" + e.FileName + "\" (" + (i + 1) + "/" + Header.FileCount + ") " +
									BytesToString(e.UncompressedSize) + " ...");*/
				e.DataOffset = pos;
				e.DataOffsetAdder = 0;

				foreach (var t in blocks)
				{
					var m = new MemoryStream(t);

					fs.Write(m.ToArray(), 0, (int) m.Length);
					pos += (uint) m.Length;
				}

				DlcFiles[i] = e;
			}

			//DebugOutput.PrintLn("Updating FileTable...");
			fs.Seek(0x20, 0);
			pos = (uint) fs.Position;
			uint blocksizeindex = 0;

			for (var i = 0; i < _header.FileCount; i++)
			{
				var e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);

				if (e.BlockSizeIndex != 0xFFFFFFFF)
				{
					fs.Write(_converter.GetBytes(blocksizeindex), 0, 4);
					e.BlockSizeIndex = blocksizeindex;
					blocksizeindex += (uint) e.BlockSizes.Length;
				}
				else
				{
					fs.Write(_converter.GetBytes(0xFFFFFFFF), 0, 4);
				}

				fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
				fs.WriteByte(e.UncompressedSizeAdder);
				fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
				fs.WriteByte(e.DataOffsetAdder);
				e.MyOffset = pos;
				DlcFiles[i] = e;
				pos += 0x1E;
			}

			fs.Seek(0, 0);
			//DebugOutput.PrintLn("Rebuilding Header...");
			fs.Write(_converter.GetBytes(_header.Magic), 0, 4);
			fs.Write(_converter.GetBytes(_header.Version), 0, 4);
			fs.Write(_converter.GetBytes(_header.DataOffset), 0, 4);
			fs.Write(_converter.GetBytes(_header.EntryOffset), 0, 4);
			fs.Write(_converter.GetBytes(_header.FileCount), 0, 4);
			fs.Write(_converter.GetBytes(_header.BlockTableOffset), 0, 4);
			fs.Write(_converter.GetBytes(_header.MaxBlockSize), 0, 4);

			foreach (var c in _header.CompressionScheme)
			{
				fs.WriteByte((byte) c);
			}

			fs.Close();
			File.Delete(FileName);
			File.Move(path, FileName);
		}

		public void AddFileQuick(string filein, string path)
		{
			//EndianBitConverter.IsLittleEndian = true;
			var dlcPath = FileName;
			var fs = new FileStream(dlcPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			var fileIn = File.ReadAllBytes(filein);
			//Create Entry
			var tmp = new List<DlcFileEntry>(DlcFiles);

			var e = new DlcFileEntry
			{
				FileName = path,
				BlockOffsets = new long[0],
				Hash = ComputeHash(path),
				BlockSizeIndex = 0xFFFFFFFF,
				UncompressedSize = (uint) fileIn.Length,
				UncompressedSizeAdder = 0
			};

			tmp.Add(e);
			//e = new DlcFileEntry();
			DlcFiles = tmp.ToArray();
			//
			//Find TOC
			var f = -1;

			for (var i = 0; i < _header.FileCount; i++)
			{
				//e = DlcFiles[i];
				if (CompareByteArray(DlcFiles[i].Hash, TocHash))
				{
					f = i;
				}
			}

			var m = DecompressEntry(f, fs);
			//
			//Update TOC
			WriteString(m, path);
			m.WriteByte(0xD);
			m.WriteByte(0xA);
			//
			//Append new FileTable
			var count = (int) _header.FileCount + 1;
			var oldsize = fs.Length;
			var offset = oldsize;
			fs.Seek(oldsize, 0);
			_header.EntryOffset = (uint) offset;

			for (var i = 0; i < count; i++)
			{
				e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);
				fs.Write(_converter.GetBytes(e.BlockSizeIndex), 0, 4);
				fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
				fs.WriteByte(e.UncompressedSizeAdder);
				fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
				fs.WriteByte(e.DataOffsetAdder);
			}

			offset += count*0x1E;
			_header.BlockTableOffset = (uint) offset;
			//
			//Append blocktable

			for (var i = 0; i < count; i++)
			{
				e = DlcFiles[i];

				if (e.BlockSizeIndex != 0xFFFFFFFF && i != f)
				{
					foreach (var u in e.BlockSizes)
					{
						fs.Write(_converter.GetBytes(u), 0, 2);
					}
				}
			}

			//
			//Update Filetable with new Blockoffsets
			fs.Seek(oldsize, 0);
			uint blocksizeindex = 0;

			for (var i = 0; i < count; i++)
			{
				e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);

				if (e.BlockSizeIndex == 0xFFFFFFFF)
				{
					fs.Write(_converter.GetBytes(e.BlockSizeIndex), 0, 4);
				}
				else
				{
					fs.Write(_converter.GetBytes(blocksizeindex), 0, 4);
					e.BlockSizeIndex = blocksizeindex;
					blocksizeindex += (uint) e.BlockSizes.Length;
					DlcFiles[i] = e;
				}

				fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
				fs.WriteByte(e.UncompressedSizeAdder);
				fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
				fs.WriteByte(e.DataOffsetAdder);
			}

			offset += blocksizeindex*2; //offset of new data
			//append new file raw
			fs.Seek(offset, 0);
			fs.Write(fileIn, 0, fileIn.Length);
			//
			//update new entry
			fs.Seek(oldsize, 0);
			blocksizeindex = 0;

			for (var i = 0; i < count; i++)
			{
				e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);

				if (i < count - 1)
				{
					if (e.BlockSizeIndex == 0xFFFFFFFF)
					{
						fs.Write(_converter.GetBytes(e.BlockSizeIndex), 0, 4);
					}
					else
					{
						fs.Write(_converter.GetBytes(blocksizeindex), 0, 4);
						blocksizeindex += (uint) e.BlockOffsets.Length;
					}
				}
				else
				{
					fs.Write(_converter.GetBytes(0xFFFFFFFF), 0, 4);
				}

				if (i < count - 1)
				{
					fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
					fs.WriteByte(e.UncompressedSizeAdder);
					fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
					fs.WriteByte(e.DataOffsetAdder);
				}
				else //new entry
				{
					if (offset <= 0xFFFFFFFF)
					{
						fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
						fs.WriteByte(e.UncompressedSizeAdder);
						fs.Write(_converter.GetBytes((int) offset), 0, 4);
						fs.WriteByte(0);
						e.DataOffset = (uint) offset;
						DlcFiles[i] = e;
					}
					else
					{
						fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
						fs.WriteByte(e.UncompressedSizeAdder);
						fs.Write(_converter.GetBytes((int) offset), 0, 4);
						fs.WriteByte((byte) ((offset & 0xFF00000000) >> 32));
					}
				}
			}

			offset += filein.Length;
			fs.Seek(offset, 0);

			//
			//Append TOC
			fs.Write(m.ToArray(), 0, (int) m.Length);

			//
			//update new entry
			fs.Seek(oldsize, 0);
			blocksizeindex = 0;

			for (var i = 0; i < count; i++)
			{
				e = DlcFiles[i];
				fs.Write(e.Hash, 0, 16);

				if (i != f)
				{
					if (e.BlockSizeIndex == 0xFFFFFFFF)
					{
						fs.Write(_converter.GetBytes(e.BlockSizeIndex), 0, 4);
					}
					else
					{
						fs.Write(_converter.GetBytes(blocksizeindex), 0, 4);
						blocksizeindex += (uint) e.BlockSizes.Length;
					}
				}
				else
				{
					fs.Write(_converter.GetBytes(0xFFFFFFFF), 0, 4);
				}

				if (i != f)
				{
					fs.Write(_converter.GetBytes(e.UncompressedSize), 0, 4);
					fs.WriteByte(e.UncompressedSizeAdder);
					fs.Write(_converter.GetBytes(e.DataOffset), 0, 4);
					fs.WriteByte(e.DataOffsetAdder);
				}
				else //new entry
				{
					if (offset <= 0xFFFFFFFF)
					{
						fs.Write(_converter.GetBytes((uint) m.Length), 0, 4);
						fs.WriteByte(e.UncompressedSizeAdder);
						fs.Write(_converter.GetBytes((int) offset), 0, 4);
						fs.WriteByte(0);
					}
					else
					{
						fs.Write(_converter.GetBytes((uint) m.Length), 0, 4);
						fs.WriteByte(e.UncompressedSizeAdder);
						fs.Write(_converter.GetBytes((int) offset), 0, 4);
						fs.WriteByte((byte) ((offset & 0xFF00000000) >> 32));
					}
				}
			}

			//Update Header
			fs.Seek(0xC, 0);
			fs.Write(_converter.GetBytes(_header.EntryOffset), 0, 4);
			fs.Write(_converter.GetBytes(count), 0, 4);
			fs.Write(_converter.GetBytes(_header.BlockTableOffset), 0, 4);

			//
			fs.Close();
		}

		public void ReplaceEntry(string filein, int index)
		{
			var fileIn = File.ReadAllBytes(filein);
			ReplaceEntry(fileIn, index);
		}

		public void ReplaceEntry(byte[] fileIn, int index)
		{
			//EndianBitConverter.IsLittleEndian = true;
			var dlcPath = FileName;
			var fs = new FileStream(dlcPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
			fs.Seek(0, SeekOrigin.End);
			var offset = (uint) fs.Length;
			fs.Write(fileIn, 0, fileIn.Length);
			var e = DlcFiles[index];
			e.BlockSizes = new ushort[0];
			e.BlockOffsets = new long[1];
			e.BlockOffsets[0] = offset;
			e.BlockSizeIndex = 0xFFFFFFFF;
			e.DataOffset = offset;
			e.UncompressedSize = (uint) fileIn.Length;
			fs.Seek(e.MyOffset, 0);
			fs.Write(e.Hash, 0, 16);
			fs.Write(_converter.GetBytes(0xFFFFFFFF), 0, 4);
			fs.Write(_converter.GetBytes(fileIn.Length), 0, 4);
			fs.WriteByte(e.UncompressedSizeAdder);
			fs.Write(_converter.GetBytes(offset), 0, 4);
			fs.WriteByte(0);
			DlcFiles[index] = e;
			fs.Close();
		}

		public void UpdateToCbin(bool rebuild = false)
		{
			//DebugOutput.PrintLn("File opened\nSearching TOCbin...");
			var f = -1;

			for (var i = 0; i < DlcFiles.Length; i++)
			{
				if (DlcFiles[i].FileName.Contains("PCConsoleTOC.bin"))
				{
					f = i;
				}
			}

			if (f == -1)
			{
				//DebugOutput.PrintLn("Couldnt Find PCConsoleTOC.bin");
				return;
			}

			var indexToc = f;
			//DebugOutput.PrintLn("Found PCConsoleTOC.bin(" + f + ")!\nLoading Entries...");
			var toc = new TocBinFile(DecompressEntry(f));
			//DebugOutput.PrintLn("Checking Entries...");
			//var count = 0;

			for (var i = 0; i < toc.Entries.Count; i++)
			{
				var e = toc.Entries[i];
				f = -1;

				for (var j = 0; j < DlcFiles.Length; j++)
				{
					if (DlcFiles[j].FileName.Replace('/', '\\').Contains(e.Name))
					{
						f = j;
					}
				}

				if (f == -1)
				{
					//DebugOutput.PrintLn((count++) + " : Entry not found " + e.name);
				}
				else
				{
					if (DlcFiles[f].UncompressedSize == e.Size)
					{
						//DebugOutput.PrintLn((count++) + " : Entry is correct " + e.name);
					}
					else if (DlcFiles[f].UncompressedSize != e.Size)
					{
						e.Size = (int) DlcFiles[f].UncompressedSize;
						//DebugOutput.PrintLn((count++) + " : Entry will be updated " + e.name);
						toc.Entries[i] = e;
					}
				}
			}

			//DebugOutput.PrintLn("Replacing TOC back...");
			ReplaceEntry(toc.Save().ToArray(), indexToc);

			if (rebuild)
			{
				//DebugOutput.PrintLn("Reopening SFAR...");
				Load(FileName);
				//DebugOutput.PrintLn("Rebuild...");
				ReBuild();
			}
		}

		/*public TreeNode ToTree()
		{
			var result = new TreeNode(FileName);
			result.Nodes.Add(Header.ToTree());
			var t = new TreeNode("FileEntries");
			for (var i = 0; i < Header.FileCount; i++)
			{
				t.Nodes.Add(DlcFiles[i].ToTree(i));
			}
			result.Nodes.Add(t);
			return result;
		}*/

		public struct DlcFileEntry
		{
			public long[] BlockOffsets;
			public uint BlockSizeIndex;
			public ushort[] BlockSizes;
			public long BlockTableOffset;
			public uint DataOffset;
			public byte DataOffsetAdder;
			public DlcHeader DlcHeader;
			public string FileName;
			public byte[] Hash;
			public uint MyOffset;
			public long RealDataOffset;
			public long RealUncompressedSize;
			public uint UncompressedSize;
			public byte UncompressedSizeAdder;

			public void Serialize(SerializingFile con, DlcHeader dlcHeader)
			{
				DlcHeader = dlcHeader;
				MyOffset = (uint) con.GetPos();

				if (con.IsLoading)
				{
					Hash = new byte[16];
				}

				for (var i = 0; i < 16; i++)
				{
					Hash[i] = con + Hash[i];
				}
				BlockSizeIndex = con + BlockSizeIndex;
				UncompressedSize = con + UncompressedSize;
				UncompressedSizeAdder = con + UncompressedSizeAdder;
				RealUncompressedSize = UncompressedSize + UncompressedSizeAdder << 32;
				DataOffset = con + DataOffset;
				DataOffsetAdder = con + DataOffsetAdder;
				RealDataOffset = DataOffset + DataOffsetAdder << 32;

				if (BlockSizeIndex == 0xFFFFFFFF)
				{
					BlockOffsets = new long[1];
					BlockOffsets[0] = RealDataOffset;
					BlockSizes = new ushort[1];
					BlockSizes[0] = (ushort) UncompressedSize;
					BlockTableOffset = 0;
				}
				else
				{
					var numBlocks = (int) Math.Ceiling(UncompressedSize/(double) dlcHeader.MaxBlockSize);

					if (con.IsLoading)
					{
						BlockOffsets = new long[numBlocks];
						BlockSizes = new ushort[numBlocks];
					}

					BlockOffsets[0] = RealDataOffset;
					var pos = con.Memory.Position;
					con.Seek((int) getBlockOffset((int) BlockSizeIndex, dlcHeader.EntryOffset, dlcHeader.FileCount), SeekOrigin.Begin);
					BlockTableOffset = con.Memory.Position;
					BlockSizes[0] = con + BlockSizes[0];

					for (var i = 1; i < numBlocks; i++)
					{
						BlockSizes[i] = con + BlockSizes[i];
						BlockOffsets[i] = BlockOffsets[i - 1] + BlockSizes[i];
					}

					con.Seek((int) pos, SeekOrigin.Begin);
				}
			}

			private long getBlockOffset(int blockIndex, uint entryOffset, uint numEntries)
			{
				return entryOffset + (numEntries*0x1E) + (blockIndex*2);
			}

			/*public TreeNode ToTree(int myIndex)
			{
				var result = new TreeNode(myIndex + " : @0x" + MyOffset.ToString("X8") + " Filename: " + FileName);
				var h = "Hash : ";
				foreach (var b in Hash)
				{
					h += b.ToString("X2");
				}
				result.Nodes.Add("Hash : " + h);
				result.Nodes.Add("BlockSizeIndex : " + BlockSizeIndex.ToString("X8"));
				result.Nodes.Add("UncompressedSize : " + UncompressedSize.ToString("X8"));
				result.Nodes.Add("UncompressedSizeAdder : " + UncompressedSizeAdder.ToString("X2"));
				result.Nodes.Add("RealUncompressedSize : " + RealUncompressedSize.ToString("X8"));
				result.Nodes.Add("DataOffset : " + DataOffset.ToString("X8"));
				result.Nodes.Add("DataOffsetAdder : " + DataOffsetAdder.ToString("X2"));
				result.Nodes.Add("RealDataOffset : " + RealDataOffset.ToString("X8"));
				result.Nodes.Add("BlockTableOffset : " + BlockTableOffset.ToString("X8"));
				var t = new TreeNode("Blocks : " + BlockOffsets.Length);
				for (var i = 0; i < BlockOffsets.Length; i++)
				{
					t.Nodes.Add(i + " : Offset: 0x" + BlockOffsets[i].ToString("X8") + " Size: 0x" + BlockSizes[i].ToString("X8"));
				}
				result.Nodes.Add(t);
				return result;
			}*/
		}

		public struct DlcHeader
		{
			public uint BlockTableOffset;
			public char[] CompressionScheme;
			public uint DataOffset;
			public uint EntryOffset;
			public uint FileCount;
			public uint Magic;
			public uint MaxBlockSize;
			public uint Version;

			public void Serialize(SerializingFile con)
			{
				Magic = con + Magic;
				Version = con + Version;
				DataOffset = con + DataOffset;
				EntryOffset = con + EntryOffset;
				FileCount = con + FileCount;
				BlockTableOffset = con + BlockTableOffset;
				MaxBlockSize = con + MaxBlockSize;

				if (con.IsLoading)
				{
					CompressionScheme = new char[4];
				}

				for (var i = 0; i < 4; i++)
				{
					CompressionScheme[i] = con + CompressionScheme[i];
				}

				if (Magic != 1397113170 || Version != 0x00010000 || MaxBlockSize != 0x00010000)
				{
					throw new Exception("Not supported DLC file!");
				}
			}

			/*public TreeNode ToTree()
			{
				var result = new TreeNode("Header");
				result.Nodes.Add("Magic : " + Magic.ToString("X8"));
				result.Nodes.Add("Version : " + Version.ToString("X8"));
				result.Nodes.Add("DataOffset : " + DataOffset.ToString("X8"));
				result.Nodes.Add("EntryOffset : " + EntryOffset.ToString("X8"));
				result.Nodes.Add("FileCount : " + FileCount.ToString("X8"));
				result.Nodes.Add("BlockTableOffset : " + BlockTableOffset.ToString("X8"));
				result.Nodes.Add("MaxBlockSize : " + MaxBlockSize.ToString("X8"));
				var scheme = "";
				for (var i = 3; i >= 0; i--)
				{
					scheme += CompressionScheme[i];
				}
				result.Nodes.Add("MaxBlockSize : " + scheme);
				return result;
			}*/
		}
	}
}