namespace MassEffect3.Conditionals
{
	public class ConditionalFloatToken : ConditionalValueToken
	{
		public ConditionalFloatToken(float value = 0f, TokenOpType opType = TokenOpType.Unknown, ConditionalTokens tokens = null) 
			: base(opType, tokens)
		{
			Value = value;
		}

		#region Overrides of ConditionalToken

		public new float Value { get; set; }

		public override TokenValueType ValueType
		{
			get { return TokenValueType.Float; }
		}

		#endregion
	}
}
