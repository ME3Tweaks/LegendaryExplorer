namespace MassEffect2.SaveEdit.Squad
{
	public static class SquadVariables
	{
		public const int MaxPlayerLevel = 60;

		static SquadVariables()
		{
			PlayerTalentPoints = new[]
			{
				3, 
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 
				2, 2, 2, 2, 2, 
				3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 
				3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 
				4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 
				4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 
				4, 4, 4, 4
				
			};

			HenchTalentPoints = new[]
			{
				1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
				1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 
				1, 1, 1, 1, 1, 
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 
				2, 2, 2, 2, 2, 
				3, 3, 3, 3, 3, 3, 3, 3, 3, 3
			};

			TotalPlayerTalentPoints = 0;
			TotalHenchTalentPoints = 0;

			foreach (var talentPoint in PlayerTalentPoints)
			{
				TotalPlayerTalentPoints += talentPoint;
			}

			foreach (var talentPoint in HenchTalentPoints)
			{
				TotalHenchTalentPoints += talentPoint;
			}
		}

		public static int[] PlayerTalentPoints { get; private set; }
		public static int[] HenchTalentPoints { get; private set; }

		public static int TotalPlayerTalentPoints { get; private set; }
		public static int TotalHenchTalentPoints { get; private set; }

		public static int GetPlayerTalentPoints(int rank)
		{
			var points = 0;

			if (rank > MaxPlayerLevel)
			{
				rank = MaxPlayerLevel;
			}

			for (var i = 0; i < rank; i++)
			{
				points += PlayerTalentPoints[i];
			}

			return points;
		}

		public static int GetHenchTalentPoints(int rank)
		{
			var points = 0;

			if (rank > MaxPlayerLevel)
			{
				rank = MaxPlayerLevel;
			}

			for (var i = 0; i < rank; i++)
			{
				points += HenchTalentPoints[i];
			}

			return points;
		}
	}
}