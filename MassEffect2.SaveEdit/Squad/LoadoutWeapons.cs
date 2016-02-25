using System;

namespace MassEffect2.SaveEdit.Squad
{
	[Flags]
	public enum LoadoutWeapons
	{
		// ReSharper disable InconsistentNaming
		None = 0,
		AssaultRifles = 1 << 0,
		HeavyWeapons = 1 << 1,
		Pistols = 1 << 2,
		Shotguns = 1 << 3,
		SMGs = 1 << 4,
		SniperRifles = 1 << 5
		// ReSharper restore InconsistentNaming
	}
}