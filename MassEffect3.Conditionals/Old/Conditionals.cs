using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class Conditionals : List<Conditional>
	{
		public Conditionals() {}

		public Conditionals(int capacity)
			: base(capacity) {}

		public Conditionals(IEnumerable<Conditional> collection)
			: base(collection) {}
	}
}
