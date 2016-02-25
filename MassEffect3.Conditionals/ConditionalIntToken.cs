namespace MassEffect3.Conditionals
{
	public class ConditionalIntToken : ConditionalValueToken
	{
		public ConditionalIntToken(int value = 0, TokenOpType opType = TokenOpType.Unknown, ConditionalTokens tokens = null) 
			: base(opType, tokens)
		{
			Value = value;
		}

		#region Overrides of ConditionalToken

		public new int Value { get; set; }

		public override TokenValueType ValueType
		{
			get { return TokenValueType.Int; }
		}

		#endregion
	}
}
