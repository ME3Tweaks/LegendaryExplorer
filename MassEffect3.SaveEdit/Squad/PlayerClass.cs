using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public class PlayerClass
	{
		public PlayerClass(PlayerCharacterClass characterClass, string combatName, string nonCombatName, int displayName, IList<string> powers = null, IList<WeaponClassType> weapons = null, IList<string> defaultPowers = null, IList<WeaponClassType> defaultWeapons = null)
		{
			CharacterClass = characterClass;
			CombatName = combatName;
			NonCombatName = nonCombatName;
			DisplayName = displayName;
			Powers = powers ?? new List<string>();
			Weapons = weapons ?? new List<WeaponClassType>();
			DefaultPowers = defaultPowers ?? new List<string>();
			DefaultWeapons = defaultWeapons ?? new List<WeaponClassType>();
		}

		public PlayerCharacterClass CharacterClass { get; set; }

		public string CombatName { get; set; }

		public IList<string> DefaultPowers { get; set; }

		public IList<WeaponClassType> DefaultWeapons { get; set; }

		public int DisplayName { get; set; }

		public int Level { get; set; }

		public string NonCombatName { get; set; }

		public IList<string> Powers { get; set; }

		public int TalentPoints { get; set; }

		public IList<WeaponClassType> Weapons { get; set; }
	}
}
