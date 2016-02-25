using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect.Localization.Tlk.Binary
{
	public class BinaryTlkFile : TlkFile
	{
		public BinaryTlkFile(IList<TlkString> strings = null)
			: base(strings)
		{}

		public int DataLength { get; set; }

		public int FemaleEntryCount { get; set; }

		public int Magic { get; set; }

		public int MaleEntryCount { get; set; }

		public int MinimumVersion { get; set; }

		public int TreeNodeCount { get; set; }

		public int Version { get; set; }

		//public IList<HuffmanNode> CharacterTree { get; set; }
		public IList<Tuple<int, int>> CharacterTree { get; set; }
		public BitArray Bits { get; set; }

		public static BinaryTlkFile Load(string path)
		{
			if (path == null)
			{
				throw new ArgumentNullException("path");
			}

			return Load(File.OpenRead(path));
		}

		public static BinaryTlkFile Load(Stream input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}

			using (var reader = new DataReader(input))
			{
				var tlkFile = new BinaryTlkFile
				{
					Magic = reader.ReadInt32(),
					Version = reader.ReadInt32(),
					MinimumVersion = reader.ReadInt32(),
					MaleEntryCount = reader.ReadInt32(),
					FemaleEntryCount = reader.ReadInt32(),
					TreeNodeCount = reader.ReadInt32(),
					DataLength = reader.ReadInt32()
				};

				var pos = reader.Position;
				reader.Seek(pos + (tlkFile.MaleEntryCount + tlkFile.FemaleEntryCount) * 8);

				// Left, Right
				var characterTree = new List<Tuple<int, int>>();

				for (var i = 0; i < tlkFile.TreeNodeCount; i++)
				{
					characterTree.Add(new Tuple<int, int>(reader.ReadInt32(), reader.ReadInt32()));
				}

				var data = new byte[tlkFile.DataLength];
				
				reader.Read(data, 0, data.Length);

				var bits = new BitArray(data);

				reader.Seek(pos);

				var rawStrings = new Dictionary<int, string>();
				var offset = 0;

				while (offset < bits.Length)
				{
					var key = offset;
					var s = tlkFile.GetString(ref offset);
					rawStrings.Add(key, s);
				}
				// int maxOffset = 0;

				/*while (offset < bits.Length)
				{
					var key = offset;
					// if (key > maxOffset)
					// maxOffset = key;
					// read the string and update 'offset' variable to store NEXT string offset
					var s = GetString(ref offset);
					rawStrings.Add(key, s);
				}*/

				// -- Step Two: read and store Huffman Tree nodes --
				// jumping to the beginning of Huffmann Tree stored in TLK file

				// -- Step Three: read all of coded data into memory --
				// and store it as raw bits for further processing
				// rewind BinaryReader just after the Header at the beginning of TLK Entries data

				// -- Step Four: decode (basing on Huffman Tree) raw bits data into actual strings --
				// and store them in a Dictionary<int, string> where:
				//	int: bit offset of the beginning of data (offset starting at 0 and counted for Bits array)
				//		so offset == 0 means the first bit in Bits array
				//	string: actual decoded string

				// -- Step Five: bind data to String IDs --
				// go through Entries in TLK file and read it's String ID and offset
				//	then check if offset is a key in rawStrings and if it is, then bind data.
				//	Sometimes there's no such key, in that case, our String ID is probably a substring
				//	of another String present in rawStrings.

				return tlkFile;
			}
		}

		private string GetString(ref int bitOffset)
		{
			var root = CharacterTree[0];
			var curNode = root;

			var curString = "";
			int i;

			for (i = bitOffset; i < Bits.Length; i++)
			{
				// reading bits' sequence and decoding it to Strings while traversing Huffman Tree
				var nextNodeId = Bits[i] ? curNode.Item2 : curNode.Item1;

				// it's an internal node - keep looking for a leaf
				if (nextNodeId >= 0)
				{
					curNode = CharacterTree[nextNodeId];
				}
				else
				{
					// it's a leaf!
					var c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - nextNodeId), 0);

					if (c != '\0')
					{
						// it's not null
						curString += c;
						curNode = root;
					}
					else
					{
						// it's a null terminating processed string, we're done
						bitOffset = i + 1;

						return curString;
					}
				}
			}

			bitOffset = i + 1;

			return null;
		}

		public void Save(string path)
		{
			if (path == null)
			{
				throw new ArgumentNullException("path");
			}

			Save(File.OpenWrite(path));
		}

		public void Save(Stream output)
		{
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}

			using (var writer = new DataWriter(output))
			{
				writer.Write(Magic);
				writer.Write(Version);
				writer.Write(MinimumVersion);
				writer.Write(MaleEntryCount);
				writer.Write(FemaleEntryCount);
				writer.Write(TreeNodeCount);
				writer.Write(DataLength);
			}
		}
	}

	public struct TlkHuffmanNode
	{
		public int LeftNodeId { get; set; }
		public int RightNodeId { get; set; }

		public TlkHuffmanNode(BinaryReader r)
			: this()
		{
			LeftNodeId = r.ReadInt32();
			RightNodeId = r.ReadInt32();
		}
	}
}
