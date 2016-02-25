using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public class PlayerProperty
	{
		public List<PowerId> DefaultPowers { get; set; }
		public LoadoutWeapons DefaultWeapons { get; set; }
		public int Level { get; set; }
		public CharacterClass PlayerClass { get; set; }
		public List<PowerId> Powers { get; set; }
		public int TalentPoints { get; set; }
	}
}