namespace MassEffect3.Tlk
{
	public class TlkHuffmanNode
	{
		 public readonly char Data;
			public readonly int FrequencyCount;
			public readonly TlkHuffmanNode Left;
			public readonly TlkHuffmanNode Right;
			public int ID;

			public TlkHuffmanNode(char d, int freq)
			{
				Data = d;
				FrequencyCount = freq;
			}

			public TlkHuffmanNode(TlkHuffmanNode left, TlkHuffmanNode right)
			{
				FrequencyCount = left.FrequencyCount + right.FrequencyCount;
				Left = left;
				Right = right;
			}
	}
}