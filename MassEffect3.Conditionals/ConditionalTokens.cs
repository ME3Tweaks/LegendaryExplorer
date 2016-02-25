using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class ConditionalTokens : List<ConditionalToken>
	{
		public ConditionalTokens() {}

		public ConditionalTokens(int capacity) 
			: base(capacity) {}

		public ConditionalTokens(IEnumerable<ConditionalToken> collection) 
			: base(collection) {}
	}
}
