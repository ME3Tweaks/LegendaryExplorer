using System.Collections.Generic;
using MassEffect3.SaveEdit.Properties;
using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit
{
	public sealed class PlayerClass
	{
		public static readonly List<PlayerClass> Classes;

		static PlayerClass()
		{
			Classes = new List<PlayerClass>
			{
				// Combat
				new PlayerClass(PlayerClasses.AdeptClassName, Localization.PlayerClass_Adept, PlayerClasses.AdeptDisplayName),
				new PlayerClass(PlayerClasses.EngineerClassName, Localization.PlayerClass_Engineer, PlayerClasses.EngineerDisplayName),
				new PlayerClass(PlayerClasses.InfiltratorClassName, Localization.PlayerClass_Infiltrator, PlayerClasses.InfiltratorDisplayName),
				new PlayerClass(PlayerClasses.SentinelClassName, Localization.PlayerClass_Sentinel, PlayerClasses.SentinelDisplayName),
				new PlayerClass(PlayerClasses.SoldierClassName, Localization.PlayerClass_Soldier, PlayerClasses.SoldierDisplayName),
				new PlayerClass(PlayerClasses.VanguardClassName, Localization.PlayerClass_Vanguard, PlayerClasses.VanguardDisplayName),

				// Non-Combat
				new PlayerClass(PlayerClasses.AdeptNonCombatClassName, Localization.PlayerClass_AdeptNonCombat, PlayerClasses.AdeptDisplayName),
				new PlayerClass(PlayerClasses.EngineerNonCombatClassName, Localization.PlayerClass_EngineerNonCombat, PlayerClasses.EngineerDisplayName),
				new PlayerClass(PlayerClasses.InfiltratorNonCombatClassName, Localization.PlayerClass_InfiltratorNonCombat, PlayerClasses.InfiltratorDisplayName),
				new PlayerClass(PlayerClasses.SentinelNonCombatClassName, Localization.PlayerClass_SentinelNonCombat, PlayerClasses.SentinelDisplayName),
				new PlayerClass(PlayerClasses.SoldierNonCombatClassName, Localization.PlayerClass_SoldierNonCombat, PlayerClasses.SoldierDisplayName),
				new PlayerClass(PlayerClasses.VanguardNonCombatClassName, Localization.PlayerClass_VanguardNonCombat, PlayerClasses.VanguardDisplayName)
			};
		}

		public PlayerClass(string type, string name, int id = 0)
		{
			Type = type;
			Name = name;
			Id = id;
		}

		public string Type { get; set; }

		public string Name { get; set; }

		public int Id { get; set; }
	}
}