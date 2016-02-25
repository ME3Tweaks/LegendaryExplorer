using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class ConditionalEntry
	{
		public ConditionalEntry(int id = 0, byte[] data = null, int offset = 0, int size = -1, int listOffset = 0, TokenNodes nodes = null,
			List<int> refBool = null, List<int> refInt = null)
		{
			Data = data ?? new byte[] {};
			Id = id;
			ListOffset = listOffset;
			Nodes = nodes ?? new TokenNodes();
			Offset = offset;
			RefBool = refBool ?? new List<int>();
			RefInt = refInt ?? new List<int>();
			Size = size;
		}

		public byte[] Data { get; set; }

		public int Id { get; set; }

		public int ListOffset { get; set; }

		public int Offset { get; set; }

		public List<int> RefBool { get; set; }

		public List<int> RefInt { get; set; }

		public int Size { get; set; }

		public TokenNodes Nodes { get; set; }
	}
}
