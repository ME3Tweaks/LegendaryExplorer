using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public class HenchmanClass
	{
		public HenchmanClass(string className, string tag = null, IList<string> powers = null, IList<WeaponClassType> weapons = null, IList<string> defaultPowers = null, IList<WeaponClassType> defaultWeapons = null)
		{
			ClassName = className;
			Tag = tag;
			Powers = powers ?? new List<string>();
			Weapons = weapons ?? new List<WeaponClassType>();
			DefaultPowers = defaultPowers ?? new List<string>();
			DefaultWeapons = defaultWeapons ?? new List<WeaponClassType>();
		}

		public string ClassName { get; set; }

		public IList<string> DefaultPowers { get; set; }

		public IList<WeaponClassType> DefaultWeapons { get; set; }

		public int Level { get; set; }

		public IList<string> Powers { get; set; }

		public string Tag { get; set; }

		public int TalentPoints { get; set; }

		public IList<WeaponClassType> Weapons { get; set; }
	}
}
