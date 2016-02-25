using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public class HenchProperty
	{
		public string ClassName { get; set; }
		public List<PowerId> DefaultPowers { get; set; }
		public LoadoutWeapons DefaultWeapons { get; set; }
		public int Level { get; set; }
		public List<PowerId> Powers { get; set; }
		public string Tag { get; set; }
		public int TalentPoints { get; set; }
		public LoadoutWeapons Weapons { get; set; }
	}
}