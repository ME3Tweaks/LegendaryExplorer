using System.Collections.Generic;
using MassEffect3.SaveEdit.Properties;
using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit
{
	internal class PlayerOrigin
	{
		public PlayerOrigin(OriginType type, string name)
		{
			Type = type;
			Name = name;
		}

		public OriginType Type { get; set; }
		public string Name { get; set; }

		public static List<PlayerOrigin> GetOrigins()
		{
			var origins = new List<PlayerOrigin>
			{
				new PlayerOrigin(OriginType.None, Localization.PlayerOrigin_None),
				new PlayerOrigin(OriginType.Colony, Localization.PlayerOrigin_Colony),
				new PlayerOrigin(OriginType.Earthborn, Localization.PlayerOrigin_Earthborn),
				new PlayerOrigin(OriginType.Spacer, Localization.PlayerOrigin_Spacer),
			};
			return origins;
		}
	}
}