using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public static class PlayerVariables
	{
		static PlayerVariables()
		{
			// Adept
			Adept = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.ClusterGrenade,
					PowerId.Pull,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Adept,
				Powers = new List<PowerId>
				{
					PowerId.ClusterGrenade,
					PowerId.Pull,
					PowerId.Shockwave,
					PowerId.Singularity,
					PowerId.Throw,
					PowerId.Warp,
					PowerId.AdpetPassive,
					PowerId.AdeptFitness,
					PowerId.Unity
				}
			};

			// Engineer
			Engineer = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.Incinerate,
					PowerId.Overload,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Engineer,
				Powers = new List<PowerId>
				{
					PowerId.Incinerate,
					PowerId.Overload,
					PowerId.CryoBlast,
					PowerId.CombatDrone,
					PowerId.Sabotage,
					PowerId.SentryTurret,
					PowerId.EngineerPassive,
					PowerId.EngineerFitness,
					PowerId.Unity
				}
			};

			// Infiltrator
			Infiltrator = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.TacticalCloak,
					PowerId.Sabotage,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Infiltrator,
				Powers = new List<PowerId>
				{
					PowerId.TacticalCloak,
					PowerId.Sabotage,
					PowerId.CryoAmmo,
					PowerId.DisruptorAmmo,
					PowerId.Incinerate,
					PowerId.StickyGrenade,
					PowerId.InfiltratorPassive,
					PowerId.InfiltratorFitness,
					PowerId.Unity
				}
			};

			// Sentinel
			Sentinel = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.Throw,
					PowerId.TechArmor,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Sentinel,
				Powers = new List<PowerId>
				{
					PowerId.CryoBlast,
					PowerId.LiftGrenade,
					PowerId.Overload,
					PowerId.TechArmor,
					PowerId.Throw,
					PowerId.Warp,
					PowerId.SentinelPassive,
					PowerId.SentinelFitness,
					PowerId.Unity
				}
			};

			// Soldier
			Soldier = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.AdrenalineRush,
					PowerId.ConcussiveShot,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Soldier,
				Powers = new List<PowerId>
				{
					PowerId.AdrenalineRush,
					PowerId.ConcussiveShot,
					PowerId.CryoAmmo,
					PowerId.DisruptorAmmo,
					PowerId.FragGrenade,
					PowerId.IncendiaryAmmo,
					PowerId.SoldierPassive,
					PowerId.SoldierFitness,
					PowerId.Unity
				}
			};

			// Vanguard
			Vanguard = new PlayerProperty
			{
				DefaultPowers = new List<PowerId>
				{
					PowerId.BioticCharge,
					PowerId.CryoAmmo,
					PowerId.Unity
				},
				PlayerClass = CharacterClass.Vanguard,
				Powers = new List<PowerId>
				{
					PowerId.BioticCharge,
					PowerId.CryoAmmo,
					PowerId.IncendiaryAmmo,
					PowerId.Nova,
					PowerId.Pull,
					PowerId.Shockwave,
					PowerId.VanguardPassive,
					PowerId.VanguardFitness,
					PowerId.Unity
				}
			};

			// Classes
			Classes = new Dictionary<CharacterClass, PlayerProperty>
			{
				{
					CharacterClass.Adept, Adept
				},
				{
					CharacterClass.Engineer, Engineer
				},
				{
					CharacterClass.Infiltrator, Infiltrator
				},
				{
					CharacterClass.Sentinel, Sentinel
				},
				{
					CharacterClass.Soldier, Soldier
				},
				{
					CharacterClass.Vanguard, Vanguard
				},
			};
		}

		public static Dictionary<CharacterClass, PlayerProperty> Classes { get; private set; }

		public static PlayerProperty Adept { get; private set; }
		public static PlayerProperty Engineer { get; private set; }
		public static PlayerProperty Infiltrator { get; private set; }
		public static PlayerProperty Sentinel { get; private set; }
		public static PlayerProperty Soldier { get; private set; }
		public static PlayerProperty Vanguard { get; private set; }
	}
}