using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ME3LibWV
{
	public class PCCPackage
	{
		public List<ExportEntry> Exports;
		public MetaInfo GeneralInfo;
		public HeaderInfo Header;
		public List<ImportEntry> Imports;
		public List<string> Names;
		public Stream Source;
		public bool Verbose;

		public PCCPackage()
		{
			GeneralInfo = new MetaInfo();
			GeneralInfo.loaded = false;
		}

		public PCCPackage(DLCPackage dlc, int index, bool loadfull = true, bool verbosemode = false)
		{
			try
			{
				Verbose = verbosemode;

				var m = dlc.DecompressEntry(index);

				GeneralInfo = new MetaInfo
				{
					inDLC = true,
					loadfull = loadfull,
					filepath = dlc.MyFileName,
					inDLCPath = dlc.Files[index].FileName,
					dlc = dlc,
					inDLCIndex = index
				};

				Load(m);

				GeneralInfo.loaded = true;
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::PCCPACKAGE ERROR:\n" + ex.Message);
			}
		}

		public PCCPackage(string pccpath, bool loadfull = true, bool verbosemode = false, bool closestream = false)
		{
			try
			{
				Verbose = verbosemode;
				var fs = new FileStream(pccpath, FileMode.Open, FileAccess.ReadWrite);

				GeneralInfo = new MetaInfo
				{
					loadfull = loadfull,
					inDLC = false,
					filepath = pccpath
				};

				Load(fs);

				GeneralInfo.loaded = true;

				if (closestream)
				{
					fs.Close();
				}
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::PCCPACKAGE ERROR:\n" + ex.Message);
			}
		}

		public void CloneEntry(int uIndex)
		{
			if (uIndex > 0)
			{
				var e = Exports[uIndex - 1];

				var n = new ExportEntry
				{
					Data = CopyArray(GetObjectData(uIndex - 1)),
					DataLoaded = true
				};

				n.Datasize = n.Data.Length;
				n.idxClass = e.idxClass;
				n.idxParent = e.idxParent;
				n.idxLink = e.idxLink;
				n.idxName = e.idxName;
				n.Index = GetLargestIndex() + 1;
				n.idxArchetype = e.idxArchetype;
				n.Unk1 = e.Unk1;
				n.ObjectFlags = e.ObjectFlags;
				n.Unk2 = e.Unk2;
				n.Unk3 = new int[e.Unk3.Length];

				for (var i = 0; i < e.Unk3.Length; i++)
				{
					n.Unk3[i] = e.Unk3[i];
				}

				n.Unk2 = e.Unk4;
				n.Unk2 = e.Unk5;
				n.Unk2 = e.Unk6;
				n.Unk2 = e.Unk7;
				n.Unk2 = e.Unk8;

				Exports.Add(n);

				Header.ExportCount++;
			}
			else
			{
				var e = Imports[-uIndex - 1];

				var n = new ImportEntry
				{
					idxPackage = e.idxPackage,
					Unk1 = e.Unk1,
					idxClass = e.idxClass,
					Unk2 = e.Unk2,
					idxLink = e.idxLink,
					idxName = e.idxName,
					Unk3 = e.Unk3
				};

				Imports.Add(n);

				Header.ImportCount++;
			}
		}

		public byte[] CopyArray(byte[] buff)
		{
			var res = new byte[buff.Length];

			for (var i = 0; i < buff.Length; i++)
			{
				res[i] = buff[i];
			}

			return res;
		}

		public int FindClass(string name)
		{
			for (var i = 0; i < Imports.Count; i++)
			{
				if (GetName(Imports[i].idxName) == name)
				{
					return (-i - 1);
				}
			}

			for (var i = 0; i < Exports.Count; i++)
			{
				if (GetName(Exports[i].idxName) == name)
				{
					return (i + 1);
				}
			}

			return 0;
		}

		public int FindName(string name, bool add = false)
		{
			for (var i = 0; i < Names.Count; i++)
			{
				if (Names[i] == name)
				{
					return i;
				}
			}

			if (!add)
			{
				return (int) Header.NameCount;
			}

			Names.Add(name);

			return (int)Header.NameCount++;
		}

		public int GetLargestIndex()
		{
			return Exports.Select(e => e.Index).Concat(new[] { 0 }).Max();
		}

		public int[] GetLinkList(int uindex)
		{
			var res = new List<int>();
			
			while (uindex != 0)
			{
				res.Add(uindex);

				uindex = uindex > 0 
					? Exports[uindex - 1].idxLink 
					: Imports[-uindex - 1].idxLink;
			}

			res.Reverse();

			return res.ToArray();
		}

		public string GetName(int index)
		{
			var s = "";

			if (IsName(index))
			{
				s = Names[index];
			}

			return s;
		}

		public string GetObject(int uindex)
		{
			if (uindex == 0)
			{
				return "Class";
			}
			if (uindex > 0)
			{
				return GetName(Exports[uindex - 1].idxName);
			}
			return GetName(Imports[-uindex - 1].idxName);
		}

		public string GetObjectClass(int uindex)
		{
			if (uindex == 0)
			{
				return "Class";
			}
			if (uindex > 0)
			{
				return GetObject(Exports[uindex - 1].idxClass);
			}
			return GetObject(Imports[-uindex - 1].idxClass);
		}

		public byte[] GetObjectData(int index)
		{
			if (index >= 0 && index < Header.ExportCount)
			{
				var e = Exports[index];
				if (e.DataLoaded)
				{
					return e.Data;
				}
				if (GeneralInfo.compressed)
				{
					UncompressRange((uint) e.Dataoffset, (uint) e.Datasize);
					e.Data = new byte[e.Datasize];
					Header.DeCompBuffer.Seek(e.Dataoffset, 0);
					Header.DeCompBuffer.Read(e.Data, 0, e.Datasize);
				}
				else
				{
					e.Data = new byte[e.Datasize];
					Source.Seek(e.Dataoffset, 0);
					Source.Read(e.Data, 0, e.Datasize);
				}
				e.DataLoaded = true;
				Exports[index] = e;
				return e.Data;
			}
			return new byte[0];
		}

		public byte[] GetObjectData(int offset, int size)
		{
			var res = new byte[size];
			if (GeneralInfo.compressed)
			{
				UncompressRange((uint) offset, (uint) size);
				Header.DeCompBuffer.Seek(offset, 0);
				Header.DeCompBuffer.Read(res, 0, size);
			}
			else
			{
				Source.Seek(offset, 0);
				Source.Read(res, 0, size);
			}
			return res;
		}

		public string GetObjectPath(int uindex)
		{
			var s = "";
			if (uindex == 0)
			{
				return s;
			}
			if (uindex > 0)
			{
				uindex = Exports[uindex - 1].idxLink;
			}
			else
			{
				uindex = Imports[-uindex - 1].idxLink;
			}
			while (uindex != 0)
			{
				s = GetObject(uindex) + "." + s;
				if (uindex > 0)
				{
					uindex = Exports[uindex - 1].idxLink;
				}
				else
				{
					uindex = Imports[-uindex - 1].idxLink;
				}
			}
			return s;
		}

		public bool IsName(int index)
		{
			return index >= 0 && index < Header.NameCount;
		}

		public float ReadFloat(Stream s)
		{
			var buff = new byte[4];
			s.Read(buff, 0, 4);
			var f = BitConverter.ToSingle(buff, 0);
			if (Verbose)
			{
				//DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " Float = " + f);
			}
			return f;
		}

		public int ReadInt(Stream s)
		{
			var buff = new byte[4];
			s.Read(buff, 0, 4);
			var i = BitConverter.ToInt32(buff, 0);

			if (Verbose)
			{
				//DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " Int32 = 0x" + i.ToString("X8"));
			}

			return i;
		}

		public uint ReadUInt(Stream s)
		{
			var buff = new byte[4];
			s.Read(buff, 0, 4);
			var u = BitConverter.ToUInt32(buff, 0);
			if (Verbose)
			{
				//DebugLog.PrintLn("Read @0x" + (s.Position - 4).ToString("X8") + " UInt32 = 0x" + u.ToString("X8"));
			}
			return u;
		}

		public ushort ReadUInt16(Stream s)
		{
			var buff = new byte[2];
			s.Read(buff, 0, 2);
			var u = BitConverter.ToUInt16(buff, 0);
			if (Verbose)
			{
				//DebugLog.PrintLn("Read @0x" + (s.Position - 2).ToString("X8") + " UInt16 = 0x" + u.ToString("X8"));
			}
			return u;
		}

		public string ReadUString(Stream s)
		{
			var res = "";
			var count = ReadInt(s);
			if (count < 0)
			{
				for (var i = 0; i < -count - 1; i++)
				{
					res += (char) s.ReadByte();
					s.ReadByte();
				}
				s.ReadByte();
				s.ReadByte();
				if (Verbose)
				{
					//DebugLog.PrintLn("Read @0x" + (s.Position + count * 2).ToString("X8") + " String = " + res);
				}
			}
			return res;
		}

		public void Save(string path = null)
		{
			try
			{
				//DebugLog.PrintLn("Writing Header...", true);
				var m = new MemoryStream();
				m.Write(BitConverter.GetBytes(Header.magic), 0, 4);
				m.Write(BitConverter.GetBytes(Header.ver1), 0, 2);
				m.Write(BitConverter.GetBytes(Header.ver2), 0, 2);
				m.Write(BitConverter.GetBytes(Header.HeaderLength), 0, 4);
				WriteUString(Header.Group, m);

				m.Write(GeneralInfo.compressed
					? BitConverter.GetBytes(Header.Flags ^ 0x02000000)
					: BitConverter.GetBytes(Header.Flags),
					0, 4);

				m.Write(BitConverter.GetBytes(Header.unk1), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk2), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk3), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk4), 0, 4);
				m.Write(Header.GUID, 0, 16);
				m.Write(BitConverter.GetBytes(Header.Generations.Count), 0, 4);
				
				foreach (var g in Header.Generations)
				{
					m.Write(BitConverter.GetBytes(g.ExportCount), 0, 4);
					m.Write(BitConverter.GetBytes(g.ImportCount), 0, 4);
					m.Write(BitConverter.GetBytes(g.NetObjCount), 0, 4);
				}

				m.Write(BitConverter.GetBytes(Header.EngineVersion), 0, 4);
				m.Write(BitConverter.GetBytes(Header.CookerVersion), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk5), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk6), 0, 4);
				m.Write(BitConverter.GetBytes(Header.CompressionFlag), 0, 4);
				m.Write(BitConverter.GetBytes(0), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk7), 0, 4);
				m.Write(BitConverter.GetBytes(Header.unk8), 0, 4);
				
				//DebugLog.PrintLn("Writing Name Table...", true);
				Header.NameOffset = (uint) m.Position;
				Header.NameCount = (uint) Names.Count;
				
				foreach (var s in Names)
				{
					WriteUString(s, m);
				}
				
				//DebugLog.PrintLn("Writing Import Table...", true);
				Header.ImportOffset = (uint) m.Position;
				Header.ImportCount = (uint) Imports.Count;
				
				foreach (var e in Imports)
				{
					m.Write(BitConverter.GetBytes(e.idxPackage), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk1), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxClass), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk2), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxLink), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxName), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk3), 0, 4);
				}
				
				//DebugLog.PrintLn("Writing Export Table...", true);
				Header.ExportOffset = (uint) m.Position;
				Header.ExportCount = (uint) Exports.Count;
				
				for (var i = 0; i < Exports.Count; i++)
				{
					var e = Exports[i];
					
					e._infooffset = (uint) m.Position;
					Exports[i] = e;
					
					m.Write(BitConverter.GetBytes(e.idxClass), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxParent), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxLink), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxName), 0, 4);
					m.Write(BitConverter.GetBytes(e.Index), 0, 4);
					m.Write(BitConverter.GetBytes(e.idxArchetype), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk1), 0, 4);
					m.Write(BitConverter.GetBytes(e.ObjectFlags), 0, 4);
					m.Write(BitConverter.GetBytes(0), 0, 4);
					m.Write(BitConverter.GetBytes(0), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk2), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk3.Length), 0, 4);
				
					foreach (var j in e.Unk3)
					{
						m.Write(BitConverter.GetBytes(j), 0, 4);
					}
					
					m.Write(BitConverter.GetBytes(e.Unk4), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk5), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk6), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk7), 0, 4);
					m.Write(BitConverter.GetBytes(e.Unk8), 0, 4);
				}
			
				Header.FreeZoneStart = Header.FreeZoneEnd = Header.HeaderLength = (uint) m.Position;
				//DebugLog.PrintLn("Writing Export Data...", true);
			
				for (var i = 0; i < Exports.Count; i++)
				{
					var e = Exports[i];
					var buff = GetObjectData(i);
					
					e.Dataoffset = (int) m.Position;
					e.Datasize = buff.Length;
					m.Write(buff, 0, buff.Length);
					
					var pos = m.Position;
					
					m.Seek(e._infooffset + 32, 0);
					m.Write(BitConverter.GetBytes(e.Datasize), 0, 4);
					m.Write(BitConverter.GetBytes(e.Dataoffset), 0, 4);
					m.Seek(pos, 0);
				}
			
				//DebugLog.PrintLn("Updating Header...", true);
				m.Seek(8, 0);
				m.Write(BitConverter.GetBytes(Header.HeaderLength), 0, 4);
				m.Seek(24 + (Header.Group.Length + 1) * 2, 0);
				m.Write(BitConverter.GetBytes(Header.NameCount), 0, 4);
				m.Write(BitConverter.GetBytes(Header.NameOffset), 0, 4);
				m.Write(BitConverter.GetBytes(Header.ExportCount), 0, 4);
				m.Write(BitConverter.GetBytes(Header.ExportOffset), 0, 4);
				m.Write(BitConverter.GetBytes(Header.ImportCount), 0, 4);
				m.Write(BitConverter.GetBytes(Header.ImportOffset), 0, 4);
				m.Write(BitConverter.GetBytes(Header.FreeZoneStart), 0, 4);
				m.Write(BitConverter.GetBytes(Header.FreeZoneEnd), 0, 4);
				//DebugLog.PrintLn("Done generating.", true);

				if (GeneralInfo.inDLC)
				{
					return;
				}

				if (Source != null)
				{
					Source.Close();
				}

				if (string.IsNullOrEmpty(path))
				{
					path = GeneralInfo.filepath;
				}

				File.WriteAllBytes(path, m.ToArray());

				//DebugLog.PrintLn("Done.", true);
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::SAVE ERROR:\n" + ex.Message);
			}
		}

		public void WriteUString(string text, Stream s)
		{
			if (!text.EndsWith("\0"))
			{
				text += "\0";
			}

			s.Write(BitConverter.GetBytes(-text.Length), 0, 4);
			
			foreach (var c in text)
			{
				s.WriteByte((byte) c);
				s.WriteByte(0);
			}
		}

		private void Load(Stream s)
		{
			try
			{
				Source = s;

				ReadHeader(s);
				ReadNameTable();
				ReadImportTable();
				ReadExportTable();
				//DebugLog.Update();
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::LOAD ERROR:\n" + ex.Message);
			}
		}

		private void ReadChunks(Stream s)
		{
			try
			{
				//DebugLog.PrintLn("Reading Chunks...");
				for (var i = 0; i < Header.Chunks.Count; i++)
				{
					if (Verbose)
					{
						//DebugLog.PrintLn("Reading Chunk(" + i + ") Header...");
					}

					var c = Header.Chunks[i];
					s.Seek(c.CompOffset, 0);
					c.Magic = ReadUInt(s);
					
					if (c.Magic != 0x9E2A83C1)
					{
						throw new Exception("Not a valid Chunkheader, wrong magic!(#" + i + ")");
					}

					c.BlockSize = ReadUInt(s);
					
					ReadUInt(s);
					ReadUInt(s);
					
					var count = (c.UnCompSize + c.BlockSize - 1) / c.BlockSize;
					c.Blocks = new List<CompressedChunkBlock>();
				
					if (Verbose)
					{
						//DebugLog.PrintLn("Reading Chunk(" + i + ") Blocks...");
					}
				
					for (var j = 0; j < count; j++)
					{
						var b = new CompressedChunkBlock();
						b.CompSize = ReadUInt(s);
						b.UnCompSize = ReadUInt(s);
						b.loaded = false;
						c.Blocks.Add(b);
					}
					
					Header.Chunks[i] = c;
				}

				if (Header.Chunks.Count == 0)
				{
					return;
				}

				var fullSize = Header.Chunks[Header.Chunks.Count - 1].UnCompOffset + Header.Chunks[Header.Chunks.Count - 1].UnCompSize;
				
				Header.DeCompBuffer = new MemoryStream(new byte[fullSize]);
				Header.DeCompBuffer.Seek(0, 0);
				Source.Seek(0, 0);
			
				var buff = new byte[Header._offsetCompFlagEnd];
				
				Source.Read(buff, 0, (int) Header._offsetCompFlagEnd);
				Header.DeCompBuffer.Write(buff, 0, (int) Header._offsetCompFlagEnd);
				Header.DeCompBuffer.Write(BitConverter.GetBytes(0), 0, 4);
				Header.DeCompBuffer.Write(BitConverter.GetBytes(Header.unk7), 0, 4);
				Header.DeCompBuffer.Write(BitConverter.GetBytes(Header.unk8), 0, 4);
				Header.DeCompBuffer.Seek(Header._offsetFlag, 0);
				
				var newFlags = (Header.Flags ^ 0x02000000);
				
				Header.DeCompBuffer.Write(BitConverter.GetBytes(newFlags), 0, 4);
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::READCHUNKS ERROR:\n" + ex.Message);
			}
		}

		private void ReadExportTable()
		{
			try
			{
				//DebugLog.PrintLn("Reading Export Table...");
				Exports = new List<ExportEntry>();
			
				if (GeneralInfo.compressed)
				{
					UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
					Header.DeCompBuffer.Seek(Header.ExportOffset, 0);
					
					for (var i = 0; i < Header.ExportCount; i++)
					{
						var e = new ExportEntry
						{
							idxClass = ReadInt(Header.DeCompBuffer),
							idxParent = ReadInt(Header.DeCompBuffer),
							idxLink = ReadInt(Header.DeCompBuffer),
							idxName = ReadInt(Header.DeCompBuffer),
							Index = ReadInt(Header.DeCompBuffer),
							idxArchetype = ReadInt(Header.DeCompBuffer),
							Unk1 = ReadInt(Header.DeCompBuffer),
							ObjectFlags = ReadInt(Header.DeCompBuffer),
							Datasize = ReadInt(Header.DeCompBuffer),
							Dataoffset = ReadInt(Header.DeCompBuffer)
						};

						var pos = Header.DeCompBuffer.Position;

						if (!GeneralInfo.loadfull)
						{
							e.DataLoaded = false;
						}
						else
						{
							e.Data = GetObjectData(e.Dataoffset, e.Datasize);
							e.DataLoaded = true;
						}

						Header.DeCompBuffer.Seek(pos, 0);
						e.Unk2 = ReadInt(Header.DeCompBuffer);
						
						var count = ReadInt(Header.DeCompBuffer);
						e.Unk3 = new int[count];
						
						for (var j = 0; j < count; j++)
						{
							e.Unk3[j] = ReadInt(Header.DeCompBuffer);
						}

						e.Unk4 = ReadInt(Header.DeCompBuffer);
						e.Unk5 = ReadInt(Header.DeCompBuffer);
						e.Unk6 = ReadInt(Header.DeCompBuffer);
						e.Unk7 = ReadInt(Header.DeCompBuffer);
						e.Unk8 = ReadInt(Header.DeCompBuffer);

						Exports.Add(e);
					}
				}
				else
				{
					Source.Seek(Header.ExportOffset, 0);
					
					for (var i = 0; i < Header.ExportCount; i++)
					{
						var e = new ExportEntry
						{
							idxClass = ReadInt(Source),
							idxParent = ReadInt(Source),
							idxLink = ReadInt(Source),
							idxName = ReadInt(Source),
							Index = ReadInt(Source),
							idxArchetype = ReadInt(Source),
							Unk1 = ReadInt(Source),
							ObjectFlags = ReadInt(Source),
							Datasize = ReadInt(Source),
							Dataoffset = ReadInt(Source)
						};

						var pos = Source.Position;
					
						if (!GeneralInfo.loadfull)
						{
							e.DataLoaded = false;
						}
						else
						{
							e.Data = GetObjectData(e.Dataoffset, e.Datasize);
							e.DataLoaded = true;
						}

						Source.Seek(pos, 0);
						e.Unk2 = ReadInt(Source);

						var count = ReadInt(Source);

						e.Unk3 = new int[count];

						for (var j = 0; j < count; j++)
						{
							e.Unk3[j] = ReadInt(Source);
						}

						e.Unk4 = ReadInt(Source);
						e.Unk5 = ReadInt(Source);
						e.Unk6 = ReadInt(Source);
						e.Unk7 = ReadInt(Source);
						e.Unk8 = ReadInt(Source);
						Exports.Add(e);
					}
				}
				//DebugLog.PrintLn("Done.");
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::READEXPORTTABLE ERROR:\n" + ex.Message);
			}
		}

		private void ReadHeader(Stream s)
		{
			try
			{
				s.Seek(0, 0);
				//DebugLog.PrintLn("Reading Package Summary...");
				var h = new HeaderInfo { magic = ReadUInt(s) };
				
				if (h.magic != 0x9E2A83C1)
				{
					throw new Exception("Not a valid PCC Package, wrong magic!");
				}

				h.ver1 = ReadUInt16(s);
				h.ver2 = ReadUInt16(s);
				h.HeaderLength = ReadUInt(s);
				h.Group = ReadUString(s);
				h._offsetFlag = (uint) s.Position;
				h.Flags = ReadUInt(s);
				GeneralInfo.compressed = (h.Flags & 0x02000000) != 0;
				//DebugLog.PrintLn("Is Compressed : " + GeneralInfo.compressed);
				h.unk1 = ReadUInt(s);
				
				if (h.unk1 != 0)
				{
					throw new Exception("Not a valid PCC Package, Unk1 != 0");
				}

				h.NameCount = ReadUInt(s);
				h.NameOffset = ReadUInt(s);
				h.ExportCount = ReadUInt(s);
				h.ExportOffset = ReadUInt(s);
				h.ImportCount = ReadUInt(s);
				h.ImportOffset = ReadUInt(s);
				h.FreeZoneStart = ReadUInt(s);
				h.FreeZoneEnd = ReadUInt(s);
				h.unk2 = ReadUInt(s);
				h.unk3 = ReadUInt(s);
				h.unk4 = ReadUInt(s);
				h.GUID = new byte[16];
				
				s.Read(h.GUID, 0, 16);
				
				var count = ReadInt(s);
				
				//DebugLog.PrintLn("Reading Generations...");
				h.Generations = new List<Generation>();
				
				for (var i = 0; i < count; i++)
				{
					var g = new Generation();
					g.ExportCount = ReadUInt(s);
					g.ImportCount = ReadUInt(s);
					g.NetObjCount = ReadUInt(s);
					h.Generations.Add(g);
				}

				//DebugLog.PrintLn("Done.");
				h.EngineVersion = ReadUInt(s);
				h.CookerVersion = ReadUInt(s);
				h.unk5 = ReadUInt(s);
				h.unk6 = ReadUInt(s);
				h.CompressionFlag = ReadUInt(s);
				h._offsetCompFlagEnd = (uint) s.Position;
				
				count = ReadInt(s);
				h.Chunks = new List<CompressedChunk>();
				
				if (GeneralInfo.compressed)
				{
					//DebugLog.PrintLn("Reading Chunktable...");
					for (var i = 0; i < count; i++)
					{
						var c = new CompressedChunk();
						c.UnCompOffset = ReadUInt(s);
						c.UnCompSize = ReadUInt(s);
						c.CompOffset = ReadUInt(s);
						c.CompSize = ReadUInt(s);
						h.Chunks.Add(c);
					}

					h.DeCompBuffer = new MemoryStream();
					//DebugLog.PrintLn("Done.");
				}
			
				h.unk7 = ReadUInt(s);
				h.unk8 = ReadUInt(s);
				Header = h;
				
				if (GeneralInfo.compressed)
				{
					ReadChunks(s);
				}
				//DebugLog.PrintLn("Done.");
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::READHEADER ERROR:\n" + ex.Message);
			}
		}

		private void ReadImportTable()
		{
			try
			{
				//DebugLog.PrintLn("Reading Import Table...");
				Imports = new List<ImportEntry>();
				
				if (GeneralInfo.compressed)
				{
					UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
					Header.DeCompBuffer.Seek(Header.ImportOffset, 0);
					
					for (var i = 0; i < Header.ImportCount; i++)
					{
						var e = new ImportEntry
						{
							idxPackage = ReadInt(Header.DeCompBuffer),
							Unk1 = ReadInt(Header.DeCompBuffer),
							idxClass = ReadInt(Header.DeCompBuffer),
							Unk2 = ReadInt(Header.DeCompBuffer),
							idxLink = ReadInt(Header.DeCompBuffer),
							idxName = ReadInt(Header.DeCompBuffer),
							Unk3 = ReadInt(Header.DeCompBuffer)
						};

						Imports.Add(e);
					}
				}
				else
				{
					Source.Seek(Header.ImportOffset, 0);

					for (var i = 0; i < Header.ImportCount; i++)
					{
						var e = new ImportEntry
						{
							idxPackage = ReadInt(Source),
							Unk1 = ReadInt(Source),
							idxClass = ReadInt(Source),
							Unk2 = ReadInt(Source),
							idxLink = ReadInt(Source),
							idxName = ReadInt(Source),
							Unk3 = ReadInt(Source)
						};

						Imports.Add(e);
					}
				}
				//DebugLog.PrintLn("Done.");
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::READIMPORTTABLE ERROR:\n" + ex.Message);
			}
		}

		private void ReadNameTable()
		{
			try
			{
				//DebugLog.PrintLn("Reading Name Table...");
				Names = new List<string>();
				
				if (GeneralInfo.compressed)
				{
					UncompressRange(Header._offsetCompFlagEnd + 0xC, Header.HeaderLength - (Header._offsetCompFlagEnd + 0xC));
					Header.DeCompBuffer.Seek(Header.NameOffset, 0);
					for (var i = 0; i < Header.NameCount; i++)
					{
						Names.Add(ReadUString(Header.DeCompBuffer));
					}
				}
				else
				{
					Source.Seek(Header.NameOffset, 0);
					for (var i = 0; i < Header.NameCount; i++)
					{
						Names.Add(ReadUString(Source));
					}
				}

				//DebugLog.PrintLn("Done.");
				if (!Verbose)
				{
					return;
				}

				for (var i = 0; i < Header.NameCount; i++)
				{
					//DebugLog.PrintLn(i.ToString("d5") + " : " + Names[i]);
				}
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::READNAMETABLE ERROR:\n" + ex.Message);
			}
		}

		private byte[] UncompressBlock(int chunkIdx, int blockIdx)
		{
			try
			{
				var c = Header.Chunks[chunkIdx];
				
				Source.Seek(c.CompOffset, 0);
				Source.Seek(0x10 + 0x08 * c.Blocks.Count, SeekOrigin.Current);
			
				for (var i = 0; i < blockIdx; i++)
				{
					Source.Seek(c.Blocks[i].CompSize, SeekOrigin.Current);
				}

				return UncompressBlock(Source, c.Blocks[blockIdx].CompSize, c.Blocks[blockIdx].UnCompSize);
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSBLOCK ERROR:\n" + ex.Message);
				return new byte[0];
			}
		}

		private byte[] UncompressBlock(Stream s, uint compSize, uint uncompSize)
		{
			var res = new byte[uncompSize];

			try
			{
				var zipstream = new InflaterInputStream(s);
				zipstream.Read(res, 0, (int) uncompSize);
				zipstream.Flush();
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSBLOCK ERROR:\n" + ex.Message);                
			}

			return res;
		}

		private void UncompressRange(uint offset, uint size)
		{
			try
			{
				var startchunk = 0;
				var endchunk = -1;

				for (var i = 0; i < Header.Chunks.Count; i++)
				{
					if (Header.Chunks[i].UnCompOffset > offset)
					{
						break;
					}
					startchunk = i;
				}

				for (var i = 0; i < Header.Chunks.Count; i++)
				{
					if (Header.Chunks[i].UnCompOffset >= offset + size)
					{
						break;
					}
					endchunk = i;
				}

				if (startchunk == -1 || endchunk == -1)
				{
					return;
				}

				for (var i = startchunk; i <= endchunk; i++)
				{
					var c = Header.Chunks[i];
					Header.DeCompBuffer.Seek(c.UnCompOffset, 0);

					for (var j = 0; j < c.Blocks.Count; j++)
					{
						var b = c.Blocks[j];
						var startblock = (uint) Header.DeCompBuffer.Position;
						var endblock = (uint) Header.DeCompBuffer.Position + b.UnCompSize;
						
						if (((startblock >= offset && startblock < offset + size) ||
							 (endblock >= offset && endblock < offset + size) ||
							 (offset >= startblock && offset < endblock) ||
							 (offset + size > startblock && offset + size <= endblock)) &&
							!b.loaded)
						{
							Header.DeCompBuffer.Write(UncompressBlock(i, j), 0, (int) b.UnCompSize);
							b.loaded = true;
							c.Blocks[j] = b;
							Header.Chunks[i] = c;
						}
						else
						{
							Header.DeCompBuffer.Seek(b.UnCompSize, SeekOrigin.Current);
						}
					}
				}
			}
			catch (Exception ex)
			{
				//DebugLog.PrintLn("PCCPACKAGE::UNCOMPRESSRANGE ERROR:\n" + ex.Message);
			}
		}

		public struct CompressedChunk
		{
			public List<CompressedChunkBlock> Blocks;
			public uint BlockSize;
			public uint CompOffset;
			public uint CompSize;
			public uint Magic;
			public uint UnCompOffset;
			public uint UnCompSize;
		}

		public struct CompressedChunkBlock
		{
			public uint CompSize;
			public bool loaded;
			public uint UnCompSize;
		}

		public struct ExportEntry
		{
			public uint _infooffset;
			public byte[] Data;
			public bool DataLoaded;
			public int Dataoffset; //0x24 36
			public int Datasize; //0x20 32
			public int idxArchetype; //0x14 20
			public int idxClass; //0x00 0
			public int idxLink; //0x08 8
			public int idxName; //0x0C 12
			public int idxParent; //0x04 4
			public int Index; //0x10 16
			public int ObjectFlags; //0x1C 28
			public int Unk1; //0x18 24
			public int Unk2; //0x28 40
			public int[] Unk3; //0x2C 44
			public int Unk4;
			public int Unk5;
			public int Unk6;
			public int Unk7;
			public int Unk8;
		}

		public struct Generation
		{
			public uint ExportCount;
			public uint ImportCount;
			public uint NetObjCount;
		}

		public struct HeaderInfo
		{
			public uint _offsetCompFlagEnd;
			public uint _offsetFlag;
			public List<CompressedChunk> Chunks;
			public uint CompressionFlag;
			public uint CookerVersion;
			public MemoryStream DeCompBuffer;
			public uint EngineVersion;
			public uint ExportCount;
			public uint ExportOffset;
			public uint Flags;
			public uint FreeZoneEnd;
			public uint FreeZoneStart;
			public List<Generation> Generations;
			public string Group;
			public byte[] GUID;
			public uint HeaderLength;
			public uint ImportCount;
			public uint ImportOffset;
			public uint magic;
			public uint NameCount;
			public uint NameOffset;
			public uint unk1;
			public uint unk2;
			public uint unk3;
			public uint unk4;
			public uint unk5;
			public uint unk6;
			public uint unk7;
			public uint unk8;
			public ushort ver1;
			public ushort ver2;
		}

		public struct ImportEntry
		{
			public int idxClass; //0x08 8
			public int idxLink; //0x10 16
			public int idxName; //0x14 20
			public int idxPackage; //0x00 0
			public int Unk1; //0x04 4
			public int Unk2; //0x0C 12
			public int Unk3; //0x18 24
		}

		public struct MetaInfo
		{
			public bool compressed;
			public DLCPackage dlc;
			public string filepath;
			public bool inDLC;
			public int inDLCIndex;
			public string inDLCPath;
			public bool loaded;
			public bool loadfull;
		}
	}
}
