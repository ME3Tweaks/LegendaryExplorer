using System.Collections.Generic;
using MassEffect3.SaveEdit.Properties;

namespace MassEffect3.SaveEdit
{
	internal class PlayerGender
	{
		public PlayerGender(bool type, string name)
		{
			Type = type;
			Name = name;
		}

		public bool Type { get; set; }
		public string Name { get; set; }

		public static List<PlayerGender> GetGenders()
		{
			var genders = new List<PlayerGender>
			{
				new PlayerGender(false, Localization.PlayerGender_Male),
				new PlayerGender(true, Localization.PlayerGender_Female),
			};
			return genders;
		}
	}
}