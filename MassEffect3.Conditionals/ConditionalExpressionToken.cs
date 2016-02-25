namespace MassEffect3.Conditionals
{
	public class ConditionalExpressionToken : ConditionalToken
	{
		public ConditionalExpressionToken(int value, TokenOpType opType = TokenOpType.Expression, ConditionalTokens tokens = null) 
			: base(opType, tokens) {}
	}
}