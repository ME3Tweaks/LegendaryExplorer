using System.Collections.Generic;
using MassEffect2.SaveEdit.Properties;
using MassEffect2.SaveFormats;

namespace MassEffect2.SaveEdit
{
	internal class PlayerNotoriety
	{
		public PlayerNotoriety(NotorietyType type, string name)
		{
			Type = type;
			Name = name;
		}

		public NotorietyType Type { get; set; }
		public string Name { get; set; }

		public static List<PlayerNotoriety> GetNotorieties()
		{
			var notorieties = new List<PlayerNotoriety>
			{
				new PlayerNotoriety(NotorietyType.None, Localization.PlayerNotoriety_None),
				new PlayerNotoriety(NotorietyType.Ruthless, Localization.PlayerNotoriety_Ruthless),
				new PlayerNotoriety(NotorietyType.Survivor, Localization.PlayerNotoriety_Survivor),
				new PlayerNotoriety(NotorietyType.Warhero, Localization.PlayerNotoriety_Warhero),
			};
			return notorieties;
		}
	}
}