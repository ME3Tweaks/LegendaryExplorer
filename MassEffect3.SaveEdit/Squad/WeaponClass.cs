using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public class WeaponClass
	{
		public WeaponClass(string className, string name, WeaponClassType weaponType = WeaponClassType.None)
		{
			ClassName = className;
			Name = name;
			WeaponType = weaponType;
		}

		public string ClassName { get; set; }

		public string Name { get; set; }

		public WeaponClassType WeaponType { get; set; }
	}
}