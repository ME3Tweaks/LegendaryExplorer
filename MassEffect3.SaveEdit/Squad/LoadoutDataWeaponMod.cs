using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public class LoadoutDataWeaponMod
	{
		public LoadoutDataWeaponMod(WeaponClassType weaponType, IList<string> player = null, IList<string> henchman = null)
		{
			WeaponType = weaponType;
			Player = player ?? new List<string>();
			Henchman = henchman ?? new List<string>();
		}

		public IList<string> Henchman { get; set; }

		public IList<string> Player { get; set; }

		public WeaponClassType WeaponType { get; set; }
	}
}
