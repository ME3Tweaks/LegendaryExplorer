namespace MassEffect3.Conditionals
{
	public abstract class ConditionalValueToken : ConditionalToken
	{
		protected ConditionalValueToken(TokenOpType opType = TokenOpType.Unknown, ConditionalTokens tokens = null) 
			: base(opType, tokens) {}

		public virtual object Value { get; set; }

		public abstract TokenValueType ValueType { get; }
	}
}