using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class TokenNodes : List<TokenNode>
	{
		public TokenNodes() {}

		public TokenNodes(int capacity)
			: base(capacity) {}

		public TokenNodes(IEnumerable<TokenNode> collection)
			: base(collection) {}

		public TokenNode Add(string text)
		{
			var tn = new TokenNode(text);

			Add(tn);

			return tn;
		}
	}
}
