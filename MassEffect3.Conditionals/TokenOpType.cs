namespace MassEffect3.Conditionals
{
	public enum TokenOpType : byte
	{
		Argument = 3,
		Expression = 5,
		StaticBool = 0,
		StaticFloat = 2,
		StaticInt = 1,
		Table = 6,
		Unknown = 7
	}
}