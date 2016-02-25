namespace MassEffect3.Conditionals
{
	public struct Token
	{
		public Token(TokenType type = TokenType.Unknown, string value = "")
			: this()
		{
			Type = type;
			Value = value;
		}

		public int Length
		{
			get { return Value.Length; }
		}

		public TokenType Type { get; set; }
		public string Value { get; set; }
	}
}
