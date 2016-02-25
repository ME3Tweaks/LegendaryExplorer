using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
//using MassEffect3.Core.Compression.Huffman;
using System.Text.RegularExpressions;
using Gammtek.Conduit.Xml.Dynamic;

namespace MassEffect3.TlkEditor
{
	internal class HuffmanCompression
	{
		private readonly Dictionary<char, BitArray> _huffmanCodes = new Dictionary<char, BitArray>();
		private readonly List<HuffmanNode> _huffmanTree = new List<HuffmanNode>();

		private readonly XmlTlkStrings _inputDataXml = new XmlTlkStrings();

		private readonly TlkStrings _maleStrings = new TlkStrings();
		private readonly TlkStrings _femaleStrings = new TlkStrings();

		//private readonly Dictionary<int, TlkString> _strings = new Dictionary<int, TlkString>();
		private readonly Dictionary<char, int> _frequencyCount = new Dictionary<char, int>();
		private Version _inputFileVersion = new Version("1.0.0.0");

		public void LoadInputData(string fileName, TalkFile.Fileformat ff, bool debugVersion)
		{
			_maleStrings.Clear();
			_femaleStrings.Clear();

			//_strings.Clear();
			LoadXmlInputData(fileName, debugVersion);

			_maleStrings.Sort((s1, s2) => (s1.Id & Int32.MaxValue).CompareTo(s2.Id & Int32.MaxValue));
			_femaleStrings.Sort((s1, s2) => (s1.Id & Int32.MaxValue).CompareTo(s2.Id & Int32.MaxValue));

			//var stringsList = _strings.OrderBy(pair => pair.Value.Position);
			
			PrepareHuffmanCoding();
		}

		public void SaveToTlkFile(string fileName)
		{
			File.Delete(fileName);

			/* converts Huffmann Tree to binary form */
			var treeBuffer = ConvertHuffmanTreeToBuffer();

			//var t = new TlkStrings();
			//var h = new Huffman<Char>(_inputData.ToCharArray());
			//var treeBuffer2 = h.Encode(_inputData.ToCharArray());

			//var bitArray = new BitArray(h.LeafCount);
			//var byteArray = bitArray.ToByteArray(treeBuffer2.Count);

			/* preparing data and entries for writing to file
             * entries list consists of pairs <String ID, Offset> */
			var binaryData = new List<BitArray>();
			//var binaryData2 = new List<BitArray>();
			var maleEntries = new Dictionary<Int32, Int32>();
			var femaleEntries = new Dictionary<Int32, Int32>();
			var offset = 0;

			foreach (var entry in _maleStrings)
			{
				if (entry.Id < 0)
				{
					if (!maleEntries.ContainsKey(entry.Id))
					{
						//var temp = Convert.ToInt32(entry.Value);
						maleEntries.Add(entry.Id, Convert.ToInt32(entry.Value));
					}
					else
					{
						//var temp = Convert.ToInt32(entry.Value);
						maleEntries[entry.Id] = Convert.ToInt32(entry.Value);
					}

					continue;
				}

				if (!maleEntries.ContainsKey(entry.Id))
				{
					maleEntries.Add(entry.Id, offset);
				}
				else
				{
					maleEntries[entry.Id] = offset;
				}

				/* for every character in a string, put it's binary code into data array */
				foreach (var c in entry.Value)
				{
					//var enc = h.Encode(c);
					var code = _huffmanCodes[c];

					binaryData.Add(code);
					//binaryData2.Add(new BitArray(enc.ToArray()));
					offset += code.Count;
				}
			}

			foreach (var entry in _femaleStrings)
			{
				if (entry.Id < 0)
				{
					if (!femaleEntries.ContainsKey(entry.Id))
					{
						//var temp = Convert.ToInt32(entry.Value);
						femaleEntries.Add(entry.Id, Convert.ToInt32(entry.Value));
					}
					else
					{
						//var temp = Convert.ToInt32(entry.Value);
						femaleEntries[entry.Id] = Convert.ToInt32(entry.Value);
					}

					continue;
				}

				if (!femaleEntries.ContainsKey(entry.Id))
				{
					femaleEntries.Add(entry.Id, offset);
				}
				else
				{
					femaleEntries[entry.Id] = offset;
				}

				/* for every character in a string, put it's binary code into data array */
				foreach (var c in entry.Value)
				{
					//var enc = h.Encode(c);
					var code = _huffmanCodes[c];

					binaryData.Add(code);

					//binaryData2.Add(new BitArray(enc.ToArray()));
					offset += code.Count;
				}
			}

			/* preparing TLK Header */
			var magic = 0x006B6C54;
			var version = 3;
			var minVersion = 2;

			var maleEntriesCount = maleEntries.Count;
			var femaleEntriesCount = femaleEntries.Count;

			var treeNodeCount = treeBuffer.Count() / 2;
			//var treeNodeCount2 = treeBuffer2.Count() / 2;
			var dataLength = offset / 8;

			if (offset % 8 > 0)
			{
				++dataLength;
			}

			var bw = new BinaryWriter(File.OpenWrite(fileName));

			/* writing TLK Header */
			bw.Write(magic);
			bw.Write(version);
			bw.Write(minVersion);
			bw.Write(maleEntriesCount);
			bw.Write(femaleEntriesCount);
			bw.Write(treeNodeCount);
			bw.Write(dataLength);

			/* writing entries */
			foreach (var entry in maleEntries)
			{
				bw.Write(entry.Key);
				bw.Write(entry.Value);
			}

			foreach (var entry in femaleEntries)
			{
				bw.Write(entry.Key);
				bw.Write(entry.Value);
			}

			/* writing HuffmanTree */
			foreach (var element in treeBuffer)
			{
				bw.Write(element);
			}

			/* writing data */
			var data = BitArrayListToByteArray(binaryData, offset);
			//var data2 = BitArrayListToByteArray(binaryData2, offset);

			var bits = new BitArray(data);
			//var bits2 = new BitArray(data2);

			var diff = 0;
			var diff2 = 0;

			/*for (var i = 0; i < binaryData.Count && i < binaryData2.Count; i++)
			{
				var a = binaryData[i];
				var a2 = binaryData2[i];

				for (var j = 0; j < a.Count && j < a2.Count; j++)
				{
					if (a[j] != a2[j])
					{
						//throw new Exception(string.Format("Not Equal: ({0}) {1} != {2}", i, data[i], data2[i]));
						diff++;
					}
				}
			}

			for (var i = 0; i < data.Length && i < data2.Length; i++)
			{
				if (data[i] != data2[i])
				{
					//throw new Exception(string.Format("Not Equal: ({0}) {1} != {2}", i, data[i], data2[i]));
					diff2++;
				}
			}*/

			var unicodeCharData = Encoding.ASCII.GetChars(data);
			//var unicodeCharData2 = Encoding.ASCII.GetChars(data2);
			
			bw.Write(data);
			//bw.Write(data2);

			bw.Close();
		}

		//public static readonly char[] ValidIdGroupSeperators = { ',', '.', '_' };
		public static readonly Regex InvalidIdRegex = new Regex("[,._]+", RegexOptions.Compiled);

		private void LoadDynamicXml(string path, bool debugVersion = false)
		{
			var tlkFile = new TlkFile();
			var tlkStrings = new List<TlkString>();

			using (var stream = new FileStream(path, FileMode.Open))
			{
				dynamic tlkXml = DynamicXml.Load(stream);

				tlkXml.TlkFile(DynamicXml.GetElement(tlkfile =>
				{
					if (tlkfile["id"] != null)
					{
						tlkFile.Id = tlkfile["id"];
					}

					if (tlkfile["name"] != null)
					{
						tlkFile.Name = tlkfile["name"];
					}

					if (tlkfile["source"] != null)
					{
						tlkFile.Source = tlkfile["source"];
					}

					if (tlkfile.Includes != null)
					{
						tlkfile.Includes(DynamicXml.GetElement(includes =>
						{
							var order = -1;

							foreach (var include in includes.Include)
							{
								if (include["order"] != null)
								{
									order = Convert.ToInt32(include["order"]);
								}

								var source = include["source"];

								if (order >= 0)
								{
									tlkFile.Includes.Insert(order, source);
								}
								else
								{
									tlkFile.Includes.Add(source);
								}
							}
						}));
					}

					if (tlkfile.Strings != null)
					{
						tlkfile.Strings(DynamicXml.GetElement(strings =>
						{
							if (strings.String == null)
							{
								return;
							}

							var i = 0;

							foreach (var str in strings.String)
							{
								var strId = InvalidIdRegex.Replace(str["id"], "");
								var id = Convert.ToInt32(strId);
								var value = str.Value.Replace("\r\n", "\n");
								var alwaysAdd = false;

								if (str["alwaysAdd"] != null)
								{
									alwaysAdd = Convert.ToBoolean(str["alwaysAdd"]);
								}

								if (id >= 0)
								{
									value += '\0';
								}

								if (id >= 0 && debugVersion && (id & 0x8000000) != 0x8000000)
								{
									value = "(#" + id + ") " + value;
								}

								tlkStrings.Add(new TlkString(id, value, i++));
							}
						}));
					}
				}));
			}

			var index = 0;

			foreach (var include in tlkFile.Includes)
			{
				var includePath = Path.GetDirectoryName(path);

				if (includePath == null)
				{
					continue;
				}

				includePath = Path.Combine(includePath, include);

				if (!File.Exists(includePath))
				{
					continue;
				}

				using (var stream = new FileStream(includePath, FileMode.Open))
				{
					dynamic tlkXml = DynamicXml.Load(stream);

					tlkXml.TlkFile(DynamicXml.GetElement(tlkfile =>
					{
						if (tlkfile.Strings == null)
						{
							return;
						}

						tlkfile.Strings(DynamicXml.GetElement(strings =>
						{
							if (strings.String == null)
							{
								return;
							}

							foreach (var str in strings.String)
							{
								var strId = InvalidIdRegex.Replace(str["id"], "");
								var id = Convert.ToInt32(strId);
								var value = str.Value.Replace("\r\n", "\n");
								var alwaysAdd = false;

								if (str["alwaysAdd"] != null)
								{
									alwaysAdd = Convert.ToBoolean(str["alwaysAdd"]);
								}

								if (id >= 0)
								{
									value += '\0';
								}

								if (id >= 0 && debugVersion && (id & 0x8000000) != 0x8000000)
								{
									value = "(#" + id + ") " + value;
								}

								/*if (alwaysAdd || _inputData.Count(s => s.Id == id) == 0)
								{
									_inputData.Add(new TlkString(id, value, index++));
								}
								else
								{
									var strIndex = _inputData.FindIndex(s => s.Id == id);

									if (strIndex >= 0)
									{
										_inputData[strIndex] = value;
									}
								}*/
								_maleStrings.Add(new TlkString(id, value, index++));
							}
						}));
					}));
				}
			}

			foreach (var tlkString in tlkStrings)
			{
				_maleStrings.Add(new TlkString(tlkString.Id, tlkString.Value, tlkString.Position + index++));
			}
		}

		private void LoadXmlInputData(string path, bool debugVersion = false)
		{
			LoadDynamicXml(path, debugVersion);
		}

		private void LoadTxtInputData(string fileName)
		{
			var streamReader = new StreamReader(fileName);
			string line;
			var i = 1;

			while (streamReader.Peek() != -1)
			{
				line = streamReader.ReadLine();
				Console.WriteLine(i++);
				Console.WriteLine(line);
				char[] delimiterChars =
				{
					':'
				};
				var words = line.Split(delimiterChars);
				Console.WriteLine("{0} words in text:", words.Length);
				foreach (var s in words)
				{
					Console.Write(s + " | ");
				}
				Console.WriteLine();
			}
			streamReader.Close();
		}

		private void PrepareHuffmanCoding()
		{
			_frequencyCount.Clear();

			foreach (var entry in _maleStrings)
			{
				if (entry.Id < 0)
				{
					continue;
				}

				foreach (var c in entry.Value)
				{
					if (!_frequencyCount.ContainsKey(c))
					{
						_frequencyCount.Add(c, 0);
					}

					++_frequencyCount[c];
				}
			}

			foreach (var element in _frequencyCount)
			{
				_huffmanTree.Add(new HuffmanNode(element.Value, element.Key));
			}

			BuildHuffmanTree();
			BuildCodingArray();

			// DebugTools.LoadHuffmanTree(_huffmanCodes);
			// DebugTools.PrintLookupTable();
		}

		private void BuildHuffmanTree()
		{
			while (_huffmanTree.Count() > 1)
			{
				/* sort Huffman Nodes by frequency */
				_huffmanTree.Sort(CompareNodes);

				var parent = new HuffmanNode(_huffmanTree[0], _huffmanTree[1]);
				_huffmanTree.RemoveAt(0);
				_huffmanTree.RemoveAt(0);
				_huffmanTree.Add(parent);
			}
		}

		private void BuildCodingArray()
		{
			/* stores a binary code */
			var currentCode = new List<bool>();
			var currenNode = _huffmanTree[0];

			TraverseHuffmanTree(currenNode, currentCode);
		}

		private void TraverseHuffmanTree(HuffmanNode node, List<bool> code)
		{
			/* check if both sons are null */
			if (node.Left == node.Right)
			{
				var ba = new BitArray(code.ToArray());
				_huffmanCodes.Add(node.Value, ba);
			}
			else
			{
				/* adds 0 to the code - process left son*/
				code.Add(false);
				TraverseHuffmanTree(node.Left, code);
				code.RemoveAt(code.Count() - 1);

				/* adds 1 to the code - process right son*/
				code.Add(true);
				TraverseHuffmanTree(node.Right, code);
				code.RemoveAt(code.Count() - 1);
			}
		}

		private List<Int32> ConvertHuffmanTreeToBuffer()
		{
			var q = new Queue<HuffmanNode>();
			var indices = new Dictionary<int, HuffmanNode>();

			var index = 0;
			q.Enqueue(_huffmanTree[0]);

			while (q.Count > 0)
			{
				var node = q.Dequeue();
				/* if it's a leaf - set it's ID to reflect char data the node contains */
				if (node.Left == node.Right)
				{
					/* store the char data */
					node.Id = -1 - node.Value;

					/* that's how it's going to be decoded when parsing TLK file:
                     * char c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - node.ID), 0); */
				}
				else
				{
					node.Id = index++;
					indices.Add(node.Id, node);
				}
				if (node.Right != null)
				{
					q.Enqueue(node.Right);
				}
				if (node.Left != null)
				{
					q.Enqueue(node.Left);
				}
			}

			var output = new List<Int32>();

			foreach (var node in indices.Values)
			{
				output.Add(node.Left.Id);
				output.Add(node.Right.Id);
			}

			return output;
		}

		private static byte[] IntArrayListToByteArray(List<List<int>> bitArrays, int bitsCount)
		{
			const int bitsperbyte = 8;

			var bytesize = bitsCount / bitsperbyte;

			if (bitsCount % bitsperbyte > 0)
			{
				bytesize++;
			}

			var bytes = new byte[bytesize];
			var bytepos = 0;
			var bitsRead = 0;
			byte value = 0;
			byte significance = 1;

			foreach (var bits in bitArrays)
			{
				var bitpos = 0;

				while (bitpos < bits.Count)
				{
					if (bits[bitpos] > 0)
					{
						value += significance;
					}

					++bitpos;
					++bitsRead;

					if (bitsRead % bitsperbyte == 0)
					{
						bytes[bytepos] = value;
						++bytepos;
						value = 0;
						significance = 1;
						bitsRead = 0;
					}
					else
					{
						significance <<= 1;
					}
				}
			}

			if (bitsRead % bitsperbyte != 0)
			{
				bytes[bytepos] = value;
			}

			return bytes;
		}

		private static byte[] BitArrayListToByteArray(List<BitArray> bitArrays, int bitsCount)
		{
			const int bitsperbyte = 8;

			var bytesize = bitsCount / bitsperbyte;

			if (bitsCount % bitsperbyte > 0)
			{
				bytesize++;
			}

			var bytes = new byte[bytesize];
			var bytepos = 0;
			var bitsRead = 0;
			byte value = 0;
			byte significance = 1;

			foreach (var bits in bitArrays)
			{
				var bitpos = 0;

				while (bitpos < bits.Length)
				{
					if (bits[bitpos])
					{
						value += significance;
					}

					++bitpos;
					++bitsRead;

					if (bitsRead % bitsperbyte == 0)
					{
						bytes[bytepos] = value;
						++bytepos;
						value = 0;
						significance = 1;
						bitsRead = 0;
					}
					else
					{
						significance <<= 1;
					}
				}
			}

			if (bitsRead % bitsperbyte != 0)
			{
				bytes[bytepos] = value;
			}

			return bytes;
		}

		private static int CompareNodes(HuffmanNode L1, HuffmanNode L2)
		{
			return L1.Probability.CompareTo(L2.Probability);
		}

		// Huffman<char>
		// HuffmanNode<char>
		private class HuffmanNode
		{
			public readonly char Value;
			public readonly int Probability;
			public readonly HuffmanNode Left;
			public readonly HuffmanNode Right;

			public int Id;

			public HuffmanNode(int freq, char d)
			{
				Value = d;
				Probability = freq;
			}

			public HuffmanNode(HuffmanNode left, HuffmanNode right)
			{
				Probability = left.Probability + right.Probability;
				Left = left;
				Right = right;
			}
		}
	}
}