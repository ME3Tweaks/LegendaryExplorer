namespace MassEffect3.Conditionals
{
	public abstract class ConditionalToken
	{
		protected ConditionalToken(TokenOpType opType = TokenOpType.Unknown, ConditionalTokens tokens = null)
		{
			Tokens = tokens ?? new ConditionalTokens();
			OpType = opType;
		}

		public ConditionalTokens Tokens { get; set; }

		public TokenOpType OpType { get; set; }
	}
}
