using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class ConditionalEntries : ConditionalEntries<ConditionalEntry>
	{
		public ConditionalEntries() {}

		public ConditionalEntries(int capacity) 
			: base(capacity) {}

		public ConditionalEntries(IEnumerable<ConditionalEntry> collection) 
			: base(collection) {}
	}
}