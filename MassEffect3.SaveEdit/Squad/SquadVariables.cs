using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public static class SquadVariables
	{
		public const int MaxPlayerLevel = 60;
		public static readonly IList<int> DefaultExperienceRequired;
		public static readonly IList<int> DefaultHenchTalentPoints;
		public static readonly IList<int> DefaultPlayerTalentPoints;
		private static IList<int> _experienceRequired;
		private static IList<int> _henchTalentPoints;
		private static IList<int> _playerTalentPoints;

		static SquadVariables()
		{
			DefaultExperienceRequired = new[]
			{
				0, 1000, 2000, 3000, 4000, 5000, 6100, 7200, 8300, 9400,
				10625, 11850, 13075, 14300, 15525, 16875, 18225, 19575, 20925, 22275,
				23775, 25275, 26775, 28725, 29775, 31425, 33075, 34725, 36375, 38025,
				39850, 41675, 43500, 45325, 47150, 49175, 51200, 53225, 55250, 57275,
				59525, 61775, 64025, 66275, 68525, 71000, 73475, 75950, 78425, 80900,
				83600, 86300, 89000, 91700, 94400, 99800, 105200, 110600, 116000, 121400
			};

			DefaultPlayerTalentPoints = new[]
			{
				3, 2, 2, 2, 2, 2, 2, 2, 2, 2,
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
				4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
				4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
				4, 4, 4, 4, 4, 4, 4, 4, 4, 4
			};

			DefaultHenchTalentPoints = new[]
			{
				1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
				1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
				2, 2, 2, 2, 2, 2, 2, 2, 2, 2
			};

			ExperienceRequired = DefaultExperienceRequired;
			PlayerTalentPoints = DefaultPlayerTalentPoints;
			HenchTalentPoints = DefaultHenchTalentPoints;

			/*TotalPlayerTalentPoints = 0;
			TotalHenchTalentPoints = 0;
			TotalExperienceRequired = 0;

			foreach (var talentPoint in PlayerTalentPoints)
			{
				TotalPlayerTalentPoints += talentPoint;
			}

			foreach (var talentPoint in HenchTalentPoints)
			{
				TotalHenchTalentPoints += talentPoint;
			}

			foreach (var xp in ExperienceRequired)
			{
				TotalExperienceRequired += xp;
			}*/
		}

		public static IList<int> ExperienceRequired
		{
			get { return _experienceRequired; }
			set
			{
				_experienceRequired = value;
				TotalExperienceRequired = 0;

				foreach (var xp in value)
				{
					TotalExperienceRequired += xp;
				}
			}
		}

		public static IList<int> HenchTalentPoints
		{
			get { return _henchTalentPoints; }
			set
			{
				_henchTalentPoints = value;
				TotalHenchTalentPoints = 0;

				foreach (var talentPoint in value)
				{
					TotalHenchTalentPoints += talentPoint;
				}
			}
		}

		public static IList<int> PlayerTalentPoints
		{
			get { return _playerTalentPoints; }
			set
			{
				_playerTalentPoints = value;
				TotalPlayerTalentPoints = 0;

				foreach (var talentPoint in value)
				{
					TotalPlayerTalentPoints += talentPoint;
				}
			}
		}

		public static int TotalExperienceRequired { get; private set; }

		public static int TotalHenchTalentPoints { get; private set; }

		public static int TotalPlayerTalentPoints { get; private set; }

		public static int GetExperienceRequired(int rank)
		{
			var xp = 0;

			if (rank > MaxPlayerLevel)
			{
				rank = MaxPlayerLevel;
			}

			for (var i = 0; i < rank; i++)
			{
				xp += ExperienceRequired[i];
			}

			return xp;
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
	}
}
