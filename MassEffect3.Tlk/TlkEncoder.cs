using System.Collections;
using System.Collections.Generic;
using HuffmanNode = MassEffect3.Tlk.TlkHuffmanNode;

namespace MassEffect3.Tlk
{
	public class TlkEncoder
	{
		internal static readonly int TlkFormatPrefix = 7040084;
		private readonly Dictionary<char, BitArray> _huffmanCodes;
		private readonly List<HuffmanNode> _huffmanTree;
		private readonly List<TlkEntry> _inputData;
		private readonly Dictionary<char, int> frequencyCount;

		public TlkEncoder()
		{
			_inputData = new List<TlkEntry>();
			frequencyCount = new Dictionary<char, int>();
			_huffmanTree = new List<HuffmanNode>();
			_huffmanCodes = new Dictionary<char, BitArray>();
		}
	}
}