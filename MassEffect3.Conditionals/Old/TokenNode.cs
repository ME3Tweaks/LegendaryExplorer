namespace MassEffect3.Conditionals
{
	public class TokenNode
	{
		public TokenNode(string text = "")
		{
			Nodes = new TokenNodes();
			Text = text;
		}

		public TokenNodes Nodes { get; private set; }

		public string Text { get; set; }
	}
}
