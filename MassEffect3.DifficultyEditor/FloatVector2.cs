namespace MassEffect3.DifficultyEditor
{
	public struct FloatVector2
	{
		public FloatVector2(float f)
			: this(f, f)
		{ }

		public FloatVector2(float x, float y)
			: this()
		{
			X = x;
			Y = y;
		}

		public float X { get; set; }

		public float Y { get; set; }
	}
}
