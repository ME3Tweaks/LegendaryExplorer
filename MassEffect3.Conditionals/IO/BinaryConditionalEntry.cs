using System.Collections.Generic;

namespace MassEffect3.Conditionals.IO
{
	public class BinaryConditionalEntry : ConditionalEntry
	{
		public byte[] Data { get; set; }

		public int Offset { get; set; }

		public int Size { get; set; }
	}
}