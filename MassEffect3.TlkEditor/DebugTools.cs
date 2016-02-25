using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace MassEffect3.TlkEditor
{
	/// <summary>
	///     Various debugging methods for both HuffmanCompression.cs and TalkFile.cs.
	///     Mainly consists of methods for printing some additional stuff.
	/// </summary>
	internal class DebugTools
	{
		private static readonly SortedDictionary<char, string> lookupDict = new SortedDictionary<char, string>();

		/* tags for unprintable Unicode characters (refer to wikipedia for meaning) */

		private static readonly string[] LowNames =
		{
			"NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL",
			"BS", "HT", "LF", "VT", "FF", "CR", "SO", "SI",
			"DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB",
			"CAN", "EM", "SUB", "ESC", "FS", "GS", "RS", "US"
		};

		public static string GetFixedLengthString(string input, int length)
		{
			input = input ?? string.Empty;
			input = input.Length > length ? input.Substring(0, length) : input;
			return string.Format("{0,-" + length + "}", input);
		}

		/// <summary>
		///     Prints TLK Header from TalkFile.cs module.
		/// </summary>
		/// <param name="Header"></param>
		public static void PrintHeader(TalkFile.TlkHeader Header)
		{
			Console.Write("Printing TLK Header info:");
			Console.WriteLine("magic: " + Header.Magic);
			Console.WriteLine("ver: " + Header.Version);
			Console.WriteLine("min_ver: " + Header.MinVersion);
			Console.WriteLine("entry1Count: " + Header.MaleEntryCount);
			Console.WriteLine("entry2Count: " + Header.FemaleEntryCount);
			Console.WriteLine("treeNodeLength: " + Header.TreeNodeCount);
			Console.WriteLine("dataLen: " + Header.DataLen);
		}

		/// <summary>
		///     Loads HuffmanTree for further debugging (eg. printing out).
		///     Start with parameters: (CharacterTree, 0, "")
		/// </summary>
		/// <remarks>
		///     Intended for TalkFile.cs data structures.
		/// </remarks>
		/// <param name="HuffmanTree"></param>
		/// <param name="curNodeID"></param>
		/// <param name="curCode"></param>
		public static void LoadHuffmanTree(List<TalkFile.HuffmanNode> HuffmanTree, int curNodeID, string curCode)
		{
			if (curNodeID < 0)
			{
				var c = BitConverter.ToChar(BitConverter.GetBytes(0xffff - curNodeID), 0);

				lookupDict.Add(c, curCode);
			}
			else
			{
				var curNode = HuffmanTree[curNodeID];

				var nextNodeID = curNode.LeftNodeId;
				LoadHuffmanTree(HuffmanTree, nextNodeID, curCode + "0");

				nextNodeID = curNode.RightNodeId;
				LoadHuffmanTree(HuffmanTree, nextNodeID, curCode + "1");
			}
		}

		/// <summary>
		///     Loads HuffmanTree for further debugging (eg. printing out).
		/// </summary>
		/// <remarks>
		///     Intended for HuffmanCompression.cs data structures.
		/// </remarks>
		/// <param name="tree"></param>
		public static void LoadHuffmanTree(Dictionary<char, BitArray> tree)
		{
			lookupDict.Clear();

			foreach (var pair in tree)
			{
				var s = "";
				foreach (bool b in pair.Value)
				{
					if (b)
					{
						s += "1";
					}
					else
					{
						s += "0";
					}
				}
				lookupDict.Add(pair.Key, s);
			}
		}

		/// <summary>
		///     Prints code for every character coded in Huffman Tree (previously loaded).
		/// </summary>
		public static void PrintLookupTable()
		{
			foreach (var x in lookupDict)
			{
				var c = x.Key;
				var code = x.Value;

				var fixedLengthCode = GetFixedLengthString(code, 30);
				Console.Write("Code = " + fixedLengthCode + "| char = ");

				if (c < 32)
				{
					var str = "<" + LowNames[c] + ">";
					Console.WriteLine("{0} | U+{1:x4}", GetFixedLengthString(str, 5), (int) c);
				}
				else
				{
					var str = "'" + c + "'";
					Console.WriteLine("{0} | U+{1:x4}", GetFixedLengthString(str, 5), (int) c);
				}
			}

			lookupDict.Clear();
			Console.WriteLine();
		}

		/// <summary>
		///     For testing binary representation of HuffmanTree form module HuffmanCompression.cs.
		/// </summary>
		/// <param name="r"></param>
		/// <param name="treeNodeCount"></param>
		public static void TestDictionaryBytesReading(BinaryReader r, int treeNodeCount)
		{
			Console.WriteLine("**********************************");
			Console.WriteLine("TESTING Huffman Tree");
			Console.WriteLine("**********************************");
			var CharacterTree = new List<TalkFile.HuffmanNode>();
			for (var i = 0; i < treeNodeCount; i++)
			{
				CharacterTree.Add(new TalkFile.HuffmanNode(r));
			}

			lookupDict.Clear();
			LoadHuffmanTree(CharacterTree, 0, "");
			PrintLookupTable();
		}
	}
}