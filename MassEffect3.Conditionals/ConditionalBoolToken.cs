namespace MassEffect3.Conditionals
{
	public class ConditionalBoolToken : ConditionalValueToken
	{
		public ConditionalBoolToken(bool value = false, TokenOpType opType = TokenOpType.StaticBool, ConditionalTokens tokens = null)
			: base(opType, tokens)
		{
			Value = value;
		}

		#region Overrides of ConditionalToken

		public new bool Value { get; set; }

		public override TokenValueType ValueType
		{
			get { return TokenValueType.Bool; }
		}

		#endregion
	}
}
