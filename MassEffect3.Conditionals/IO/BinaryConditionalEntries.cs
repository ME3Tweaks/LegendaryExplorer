using System.Collections.Generic;

namespace MassEffect3.Conditionals.IO
{
	public class BinaryConditionalEntries : ConditionalEntries<BinaryConditionalEntry>
	{
		public BinaryConditionalEntries() {}

		public BinaryConditionalEntries(int capacity) 
			: base(capacity) {}

		public BinaryConditionalEntries(IEnumerable<BinaryConditionalEntry> collection) 
			: base(collection) {}
	}
}