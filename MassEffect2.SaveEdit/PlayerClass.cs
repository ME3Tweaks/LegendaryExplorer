using System.Collections.Generic;
using MassEffect2.SaveEdit.Properties;
using MassEffect2.SaveFormats;

namespace MassEffect2.SaveEdit
{
	public sealed class PlayerClass
	{
		public static readonly List<PlayerClass> Classes;

		static PlayerClass()
		{
			Classes = new List<PlayerClass>
			{
				new PlayerClass(PlayerClasses.AdeptClassName, Localization.PlayerClass_Adept, PlayerClasses.AdeptDisplayName),
				new PlayerClass(PlayerClasses.EngineerClassName, Localization.PlayerClass_Engineer, PlayerClasses.EngineerDisplayName),
				new PlayerClass(PlayerClasses.InfiltratorClassName, Localization.PlayerClass_Infiltrator, PlayerClasses.InfiltratorDisplayName),
				new PlayerClass(PlayerClasses.SentinelClassName, Localization.PlayerClass_Sentinel, PlayerClasses.SentinelDisplayName),
				new PlayerClass(PlayerClasses.SoldierClassName, Localization.PlayerClass_Soldier, PlayerClasses.SoldierDisplayName),
				new PlayerClass(PlayerClasses.VanguardClassName, Localization.PlayerClass_Vanguard, PlayerClasses.VanguardDisplayName)
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