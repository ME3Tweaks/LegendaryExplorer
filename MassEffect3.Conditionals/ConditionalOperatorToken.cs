namespace MassEffect3.Conditionals
{
	public class ConditionalOperatorToken : ConditionalToken
	{
		public ConditionalOperatorToken(OperatorType operatorType = OperatorType.EqualTo, TokenOpType opType = TokenOpType.Expression, ConditionalTokens tokens = null)
			: base(opType, tokens)
		{
			OperatorType = operatorType;
		}

		public OperatorType OperatorType { get; set; }
	}
}