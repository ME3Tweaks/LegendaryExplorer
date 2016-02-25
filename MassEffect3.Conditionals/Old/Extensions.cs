namespace MassEffect3.Conditionals
{
	public static class Extensions
	{
		public static bool IsDigit(this char c)
		{
			return char.IsDigit(c);
		}

		public static bool IsEndOfLine(this char c)
		{
			return c == '\0';
		}

		public static bool IsLetter(this char c)
		{
			return char.IsLetter(c);
		}

		public static bool IsQuote(this char c)
		{
			return c == '\"';
		}

		public static bool IsSpace(this char c)
		{
			return c == ' ';
		}

		public static bool IsWhiteSpace(this char c)
		{
			return char.IsWhiteSpace(c);
		}

		public static bool IsWhiteSpace(this char c, bool endOfLine)
		{
			return char.IsWhiteSpace(c) && c.IsEndOfLine();
		}
	}
}
