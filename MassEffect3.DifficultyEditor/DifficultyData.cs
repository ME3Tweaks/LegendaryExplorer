namespace MassEffect3.DifficultyEditor
{
	public struct DifficultyData
	{
		public DifficultyData(string statName, FloatVector2? statRange = null)
			: this()
		{
			StatName = statName;
			StatRange = statRange ?? new FloatVector2(0.0f);
		}

		public string StatName { get; set; }

		public FloatVector2 StatRange { get; set; }
	}
}
