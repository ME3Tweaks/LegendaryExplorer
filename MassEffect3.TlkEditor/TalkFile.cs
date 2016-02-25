using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
//using Gammtek.Conduit.Compression.GenericHuffman;
using Gammtek.Conduit.Compression.Huffman;
using Gammtek.Conduit.Xml.Dynamic;

namespace MassEffect3.TlkEditor
{
	internal class TalkFile
	{
		public delegate void ProgressChangedEventHandler(int percentProgress);

		private BitArray _bits;
		private List<HuffmanNode> _characterTree;

		private TlkHeader _header;
		public List<TlkString> MaleStringRefs;
		private string _path;

		public event ProgressChangedEventHandler ProgressChanged;

		private void OnProgressChanged(int percentProgress)
		{
			var handler = ProgressChanged;
			if (handler != null)
			{
				handler(percentProgress);
			}
		}

		public void LoadTlkData(string path)
		{
			//var huffman = new StaticHuffman<char>();
			/* **************** STEP ONE ****************
             *          -- load TLK file header --
             * 
             * reading first 28 (4 * 7) bytes 
             */
			_path = path;
			Stream fs = File.OpenRead(path);
			var r = new BinaryReader(fs);
			_header = new TlkHeader(r);

			//DebugTools.PrintHeader(Header);

			/* **************** STEP TWO ****************
             *  -- read and store Huffman Tree nodes -- 
             */
			/* jumping to the beginning of Huffmann Tree stored in TLK file */
			var pos = r.BaseStream.Position;
			r.BaseStream.Seek(pos + (_header.MaleEntryCount + _header.FemaleEntryCount) * 8, SeekOrigin.Begin);

			_characterTree = new List<HuffmanNode>();
			for (var i = 0; i < _header.TreeNodeCount; i++)
			{
				_characterTree.Add(new HuffmanNode(r));
			}

			r.BaseStream.Seek(pos + (_header.MaleEntryCount + _header.FemaleEntryCount) * 8, SeekOrigin.Begin);
			var characterTree = new List<Tuple<int, int>>();
			//var queue = new Queue<HuffmanNode<int>>();
			//var queue = new Queue<HuffmanNode<int>>();
			
			for (var i = 0; i < _header.TreeNodeCount; i++)
			{
				characterTree.Add(new Tuple<int, int>(r.ReadInt32(), r.ReadInt32()));
			}

			/*foreach (var tuple in characterTree)
			{
				var leftNode = new HuffmanNode<int>(tuple.Item1);
				var rightNode = new HuffmanNode<int>(tuple.Item2);
				var parent = new HuffmanNode<int>(leftNode, rightNode);

				queue.Enqueue(parent);
			}*/

			//var tree = new HuffmanTree<int>(table);


			/* **************** STEP THREE ****************
             *  -- read all of coded data into memory -- 
             */
			var data = new byte[_header.DataLen];
			r.BaseStream.Read(data, 0, data.Length);
			/* and store it as raw bits for further processing */
			_bits = new BitArray(data);

			/* rewind BinaryReader just after the Header
             * at the beginning of TLK Entries data */
			r.BaseStream.Seek(pos, SeekOrigin.Begin);

			/* **************** STEP FOUR ****************
             * -- decode (basing on Huffman Tree) raw bits data into actual strings --
             * and store them in a Dictionary<int, string> where:
             *   int: bit offset of the beginning of data (offset starting at 0 and counted for Bits array)
             *        so offset == 0 means the first bit in Bits array
             *   string: actual decoded string */
			var rawStrings = new Dictionary<int, string>();
			var offset = 0;

			// int maxOffset = 0;
			while (offset < _bits.Length)
			{
				var key = offset;
				// if (key > maxOffset)
				// maxOffset = key;
				/* read the string and update 'offset' variable to store NEXT string offset */
				var s = GetString(ref offset);
				rawStrings.Add(key, s);
			}
			// Console.WriteLine("Max offset = " + maxOffset);

			/* **************** STEP FIVE ****************
             *         -- bind data to String IDs --
             * go through Entries in TLK file and read it's String ID and offset
             * then check if offset is a key in rawStrings and if it is, then bind data.
             * Sometimes there's no such key, in that case, our String ID is probably a substring
             * of another String present in rawStrings. 
             */
			//StringRefs = new XmlTlkStrings();
			MaleStringRefs = new List<TlkString>();
			FemaleStringRefs = new List<TlkString>();

			for (var i = 0; i < _header.MaleEntryCount/* + _header.FemaleEntryCount*/; i++)
			{
				var sref = new TlkString(r, i);
				
				if (sref.BitOffset >= 0)
				{
					if (!rawStrings.ContainsKey(sref.BitOffset))
					{
						var tmpOffset = sref.BitOffset;
						var partString = GetString(ref tmpOffset);

						/* actually, it should store the fullString and subStringOffset,
                         * but as we don't have to use this compression feature,
                         * we will store only the part of string we need */

						/* int key = rawStrings.Keys.Last(c => c < sref.BitOffset);
                         * string fullString = rawStrings[key];
                         * int subStringOffset = fullString.LastIndexOf(partString);
                         * sref.StartOfString = subStringOffset;
                         * sref.Data = fullString;
                         */
						sref.Value = partString;
					}
					else
					{
						sref.Value = rawStrings[sref.BitOffset];
					}
				}

				if (string.IsNullOrEmpty(sref.Value) || string.IsNullOrWhiteSpace(sref.Value))
				{
					sref.Value = TlkString.EmptyText;
				}

				MaleStringRefs.Add(sref);
			}

			for (var i = 0; i < _header.FemaleEntryCount; i++)
			{
				var sref = new TlkString(r, i);

				if (sref.BitOffset >= 0)
				{
					if (!rawStrings.ContainsKey(sref.BitOffset))
					{
						var tmpOffset = sref.BitOffset;
						var partString = GetString(ref tmpOffset);

						/* actually, it should store the fullString and subStringOffset,
                         * but as we don't have to use this compression feature,
                         * we will store only the part of string we need */

						/* int key = rawStrings.Keys.Last(c => c < sref.BitOffset);
                         * string fullString = rawStrings[key];
                         * int subStringOffset = fullString.LastIndexOf(partString);
                         * sref.StartOfString = subStringOffset;
                         * sref.Data = fullString;
                         */
						sref.Value = partString;
					}
					else
					{
						sref.Value = rawStrings[sref.BitOffset];
					}
				}

				if (string.IsNullOrEmpty(sref.Value) || string.IsNullOrWhiteSpace(sref.Value))
				{
					sref.Value = TlkString.EmptyText;
				}

				FemaleStringRefs.Add(sref);
			}

			r.Close();
		}

		public List<TlkString> FemaleStringRefs { get; set; }

		public void DumpToFile(string fileName, Fileformat ff)
		{
			File.Delete(fileName);
			/* for now, it's better not to sort, to preserve original order */
			// StringRefs.Sort(CompareTlkStringRef);

			if (ff == Fileformat.Xml)
			{
				SaveToXmlFile(fileName);
			}
			else
			{
				SaveToTextFile(fileName);
			}
		}

		private string GetString(ref int bitOffset)
		{
			var root = _characterTree[0];
			var curNode = root;

			var curString = "";
			int i;
			for (i = bitOffset; i < _bits.Length; i++)
			{
				/* reading bits' sequence and decoding it to Strings while traversing Huffman Tree */
				var bit = _bits[i];
				var nextNodeId = _bits[i] ? curNode.RightNodeId : curNode.LeftNodeId;

				/* it's an internal node - keep looking for a leaf */
				if (nextNodeId >= 0)
				{
					curNode = _characterTree[nextNodeId];
				}
				else
				{
					/* it's a leaf! */
					var c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - nextNodeId), 0);
					
					if (c != '\0')
					{
						/* it's not NULL */
						curString += c;
						curNode = root;
					}
					else
					{
						/* it's a NULL terminating processed string, we're done */
						bitOffset = i + 1;
						
						return curString;
					}
				}
			}

			bitOffset = i + 1;

			return null;
		}

		private static string GetId(string path)
		{
			if (path.LastIndexOf('_') == (path.Length - 4))
			{
				return path.Remove(path.LastIndexOf('_'), 4);
			}

			return path;
		}

		private void SaveToDynamicXml(string path)
		{
			// To reduce size of XML files
			const int maxEntries = 10000;
			
			var tlkFile = new TlkFile
			{
				Id = GetId(Path.GetFileNameWithoutExtension(_path)),
				Name = Path.GetFileNameWithoutExtension(_path),
				Source = Path.GetFileName(_path)
			};

			dynamic tlkXml = new DynamicXml();

			var maleIncludeCount = MaleStringRefs.Count / maxEntries;
			var femaleIncludeCount = FemaleStringRefs.Count / maxEntries;
			var includeCount = Math.Max(maleIncludeCount, femaleIncludeCount);

			if ((MaleStringRefs.Count % maxEntries > 0)
				|| (FemaleStringRefs.Count % maxEntries > 0))
			{
				includeCount++;
			}

			MaleStringRefs.Sort((s1, s2) => (s1.Id & Int32.MaxValue).CompareTo(s2.Id & Int32.MaxValue));
			FemaleStringRefs.Sort((s1, s2) => (s1.Id & Int32.MaxValue).CompareTo(s2.Id & Int32.MaxValue));

			/*for (var i = 0; i < MaleStringRefs.Count; i++)
			{
				var s = MaleStringRefs[i];
				s.Position = i;

				tlkFile.MaleStrings.Add(s);
			}*/

			/*for (var i = 0; i < FemaleStringRefs.Count; i++)
			{
				var s = FemaleStringRefs[i];
				s.Position = i;

				tlkFile.FemaleStrings.Add(s);
			}*/

			tlkXml.TlkFile(DynamicXml.CreateElement(tlkfile =>
			{
				tlkfile["id"] = tlkFile.Id;
				tlkfile["name"] = tlkFile.Name;
				tlkfile["source"] = tlkFile.Source;

				tlkfile.Includes(DynamicXml.CreateElement(includes =>
				{
					for (var i = 0; i < includeCount; i++)
					{
						var item = string.Format("{0}/{0}{1}.xml", tlkFile.Name, i);

						includes.Include(DynamicXml.CreateElement(include =>
						{
							include["source"] = item;
						}));

						tlkFile.Includes.Add(item);
					}
				}));
			}));

			tlkXml.Save(path);

			var index = 0;

			foreach (var include in tlkFile.Includes)
			{
				dynamic includeXml = new DynamicXml();

				var include1 = include;

				includeXml.TlkFile(DynamicXml.CreateElement(tlkfile =>
				{
					tlkfile["name"] = include1;

					tlkfile.MaleStrings(DynamicXml.CreateElement(strings =>
					{
						var count = MaleStringRefs.Count;

						for (var x = 0; x < maxEntries && index < count; x++, index++)
						{
							var s = MaleStringRefs[index];
							
							strings.String(DynamicXml.CreateElement(str =>
							{
								str["id"] = s.Id;
								str.Value = s.Value;
							}));
						}
					}));

					tlkfile.FemaleStrings(DynamicXml.CreateElement(strings =>
					{
						var count = FemaleStringRefs.Count;

						for (var x = 0; x < maxEntries && index < count; x++, index++)
						{
							var s = FemaleStringRefs[index];

							strings.String(DynamicXml.CreateElement(str =>
							{
								str["id"] = s.Id;
								str.Value = s.Value;
							}));
						}
					}));
				}));

				var destPath = Path.GetDirectoryName(path);

				if (destPath != null)
				{
					destPath = Path.Combine(destPath, include1);

					if (!Directory.Exists(Path.GetDirectoryName(destPath)))
					{
						Directory.CreateDirectory(Path.GetDirectoryName(destPath));
					}

					includeXml.Save(destPath);
				}
			}
		}

		private void SaveToXmlFile(string path)
		{
			SaveToDynamicXml(path);
			/*var tlk = new XmlTlkFile();
			var ser = new XmlSerializer(typeof(XmlTlkFile));
			var i = 0;

			foreach (var s in StringRefs)
			{
				tlk.Strings.Add(new XmlTlkString(s.Id, s.Value, i++));
			}

			using (var writer = new XmlTextWriter(path, Encoding.UTF8))
			{
				writer.Formatting = Formatting.Indented;
				writer.Indentation = 4;

				ser.Serialize(writer, tlk);
			}*/
		}

		private void SaveToTextFile(string fileName)
		{
			var totalCount = MaleStringRefs.Count();
			var count = 0;
			var lastProgress = -1;

			foreach (var s in MaleStringRefs)
			{
				var line = s.Id + ": " + s.Value + "\r\n";
				File.AppendAllText(fileName, line);

				var progress = (++count * 100) / totalCount;
				if (progress > lastProgress)
				{
					lastProgress = progress;
					OnProgressChanged(lastProgress);
				}
			}
		}

		/* for sorting */

		private static int CompareTlkStringRef(XmlTlkString strRef1, XmlTlkString strRef2)
		{
			var result = strRef1.Id.CompareTo(strRef2.Id);
			return result;
		}

		internal enum Fileformat
		{
			Txt,
			Csv,
			Xml
		}

		public struct HuffmanNode
		{
			public int LeftNodeId { get; set; }
			public int RightNodeId { get; set; }

			public HuffmanNode(BinaryReader r)
				: this()
			{
				LeftNodeId = r.ReadInt32();
				RightNodeId = r.ReadInt32();
			}
		}

		public struct TlkHeader
		{
			public int DataLen { get; set; }
			public int MaleEntryCount { get; set; }
			public int FemaleEntryCount { get; set; }
			public int Magic { get; set; }
			public int MinVersion { get; set; }
			public int TreeNodeCount { get; set; }
			public int Version { get; set; }

			public TlkHeader(BinaryReader r)
				: this()
			{
				Magic = r.ReadInt32();
				Version = r.ReadInt32();
				MinVersion = r.ReadInt32();
				MaleEntryCount = r.ReadInt32();
				FemaleEntryCount = r.ReadInt32();
				TreeNodeCount = r.ReadInt32();
				DataLen = r.ReadInt32();
			}
		};
	}
}