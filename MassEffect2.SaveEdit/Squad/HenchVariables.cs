using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public static class HenchVariables
	{
		static HenchVariables()
		{
			// Anderson
			Anderson = new HenchProperty
			{
				ClassName = "SFXPawn_Anderson",
				DefaultPowers = new List<PowerId>
				{
					PowerId.ConcussiveShot
				},
				Powers = new List<PowerId>
				{
					PowerId.ConcussiveShot,
					PowerId.FragGrenade,
					PowerId.Fortification,
					PowerId.IncendiaryAmmo,
					PowerId.AndersonPassive
				},
				Tag = "hench_anderson",
				Weapons = LoadoutWeapons.Pistols
			};

			// Aria
			Aria = new HenchProperty
			{
				ClassName = "SFXPawn_Aria",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Flare
				},
				Powers = new List<PowerId>
				{
					PowerId.Flare,
					PowerId.Reave,
					PowerId.Carnage,
					PowerId.Lash,
					PowerId.AriaPassive
				},
				Tag = "hench_aria",
				Weapons = LoadoutWeapons.Shotguns | LoadoutWeapons.SMGs
			};

			// Ashley
			Ashley = new HenchProperty
			{
				ClassName = "SFXPawn_Ashley",
				DefaultPowers = new List<PowerId>
				{
					PowerId.InfernoGrenade
				},
				Powers = new List<PowerId>
				{
					PowerId.InfernoGrenade,
					PowerId.DisruptorAmmo,
					PowerId.ConcussiveShot,
					PowerId.Marksman,
					PowerId.AshleyPassive
				},
				Tag = "hench_ashley",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.SniperRifles
			};

			// EDI
			Edi = new HenchProperty
			{
				ClassName = "SFXPawn_EDI",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Incinerate
				},
				Powers = new List<PowerId>
				{
					PowerId.Incinerate,
					PowerId.Overload,
					PowerId.DefenseMatrix,
					PowerId.Decoy,
					PowerId.EDIPassive
				},
				Tag = "hench_edi",
				Weapons = LoadoutWeapons.SMGs | LoadoutWeapons.Pistols
			};

			// Garrus
			Garrus = new HenchProperty
			{
				ClassName = "SFXPawn_Garrus",
				DefaultPowers = new List<PowerId>
				{
					PowerId.ConcussiveShot
				},
				Powers = new List<PowerId>
				{
					PowerId.ConcussiveShot,
					PowerId.Overload,
					PowerId.ArmorPiercingAmmo,
					PowerId.ProximityMine,
					PowerId.GarrusPassive
				},
				Tag = "hench_garrus",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.SniperRifles
			};

			// Grunt
			Grunt = new HenchProperty
			{
				ClassName = "SFXPawn_Grunt",
				DefaultPowers = new List<PowerId>
				{
					PowerId.ConcussiveShot
				},
				Powers = new List<PowerId>
				{
					PowerId.ConcussiveShot,
					PowerId.IncendiaryAmmo,
					PowerId.Fortification,
					PowerId.FragGrenade,
					PowerId.GruntPassive
				},
				Tag = "hench_grunt",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Shotguns
			};

			// Jack
			Jack = new HenchProperty
			{
				ClassName = "SFXPawn_Jack",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Shockwave
				},
				Powers = new List<PowerId>
				{
					PowerId.Shockwave,
					PowerId.Pull,
					PowerId.Warp,
					PowerId.WarpAmmo,
					PowerId.JackPassive
				},
				Tag = "hench_jack",
				Weapons = LoadoutWeapons.Shotguns | LoadoutWeapons.Pistols
			};

			// Jacob
			Jacob = new HenchProperty
			{
				ClassName = "SFXPawn_Jacob",
				DefaultPowers = new List<PowerId>
				{
					PowerId.IncendiaryAmmo
				},
				Powers = new List<PowerId>
				{
					PowerId.IncendiaryAmmo,
					PowerId.LiftGrenade,
					PowerId.Barrier,
					PowerId.Pull,
					PowerId.JacobPassive
				},
				Tag = "hench_jacob",
				Weapons = LoadoutWeapons.Shotguns | LoadoutWeapons.Pistols
			};

			// James
			James = new HenchProperty
			{
				ClassName = "SFXPawn_Marine",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Fortification
				},
				Powers = new List<PowerId>
				{
					PowerId.Fortification,
					PowerId.FragGrenade,
					PowerId.IncendiaryAmmo,
					PowerId.Carnage,
					PowerId.JamesPassive
				},
				Tag = "hench_marine",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Shotguns
			};

			// Javik
			Javik = new HenchProperty
			{
				ClassName = "SFXPawn_Prothean",
				DefaultPowers = new List<PowerId>
				{
					PowerId.DarkChannel
				},
				Powers = new List<PowerId>
				{
					PowerId.DarkChannel,
					PowerId.LiftGrenade,
					PowerId.Pull,
					PowerId.Slam,
					PowerId.JavikPassive
				},
				Tag = "hench_prothean",
				//Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Pistols
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Shotguns
			};

			// Kaidan
			Kaidan = new HenchProperty
			{
				ClassName = "SFXPawn_Kaidan",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Barrier
				},
				Powers = new List<PowerId>
				{
					PowerId.Barrier,
					PowerId.Reave,
					PowerId.Overload,
					PowerId.CryoBlast,
					PowerId.KaidanPassive
				},
				Tag = "hench_kaidan",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Pistols
			};

			// Kasumi
			Kasumi = new HenchProperty
			{
				ClassName = "SFXPawn_Kasumi",
				DefaultPowers = new List<PowerId>
				{
					PowerId.TacticalCloak
				},
				Powers = new List<PowerId>
				{
					PowerId.TacticalCloak,
					PowerId.Overload,
					PowerId.ArmorPiercingAmmo,
					PowerId.Decoy,
					PowerId.KasumiPassive
				},
				Tag = "hench_kasumi",
				Weapons = LoadoutWeapons.SMGs | LoadoutWeapons.Pistols
			};

			// Liara
			Liara = new HenchProperty
			{
				ClassName = "SFXPawn_Liara",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Singularity
				},
				Powers = new List<PowerId>
				{
					PowerId.Singularity,
					PowerId.Stasis,
					PowerId.WarpAmmo,
					PowerId.Warp,
					PowerId.LiaraPassive
				},
				Tag = "hench_liara",
				Weapons = LoadoutWeapons.SMGs | LoadoutWeapons.Pistols
			};

			// Miranda
			Miranda = new HenchProperty
			{
				ClassName = "SFXPawn_Miranda",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Overload
				},
				Powers = new List<PowerId>
				{
					PowerId.Overload,
					PowerId.Warp,
					PowerId.Incinerate,
					PowerId.Reave,
					PowerId.MirandaPassive
				},
				Tag = "hench_miranda",
				Weapons = LoadoutWeapons.SMGs | LoadoutWeapons.Pistols
			};

			// Nyreen
			Nyreen = new HenchProperty
			{
				ClassName = "SFXPawn_Nyreen",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Incinerate
				},
				Powers = new List<PowerId>
				{
					PowerId.Incinerate,
					PowerId.Overload,
					PowerId.BioticProtector,
					PowerId.LiftGrenade,
					PowerId.NyreenPassive
				},
				Tag = "hench_nyreen",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.Pistols
			};

			// Samara
			Samara = new HenchProperty
			{
				ClassName = "SFXPawn_Samara",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Reave
				},
				Powers = new List<PowerId>
				{
					PowerId.Reave,
					PowerId.Warp,
					PowerId.Pull,
					PowerId.Throw,
					PowerId.SamaraPassive
				},
				Tag = "hench_samara",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.SMGs
			};

			// Tali
			Tali = new HenchProperty
			{
				ClassName = "SFXPawn_Tali",
				DefaultPowers = new List<PowerId>
				{
					PowerId.EnergyDrain
				},
				Powers = new List<PowerId>
				{
					PowerId.EnergyDrain,
					PowerId.Sabotage,
					PowerId.CombatDrone,
					PowerId.DefenseDrone,
					PowerId.TaliPassive
				},
				Tag = "hench_tali",
				Weapons = LoadoutWeapons.Shotguns | LoadoutWeapons.Pistols
			};

			// Wrex
			Wrex = new HenchProperty
			{
				ClassName = "SFXPawn_Wrex",
				DefaultPowers = new List<PowerId>
				{
					PowerId.Carnage
				},
				Powers = new List<PowerId>
				{
					PowerId.Carnage,
					PowerId.LiftGrenade,
					PowerId.Barrier,
					PowerId.StimulantPack,
					PowerId.WrexPassive
				},
				Tag = "hench_wrex",
				Weapons = LoadoutWeapons.Shotguns | LoadoutWeapons.Pistols
			};

			// Zaeed
			Zaeed = new HenchProperty
			{
				ClassName = "SFXPawn_Zaeed",
				DefaultPowers = new List<PowerId>
				{
					PowerId.ConcussiveShot
				},
				Powers = new List<PowerId>
				{
					PowerId.ConcussiveShot,
					PowerId.FragGrenade,
					PowerId.Fortification,
					PowerId.IncendiaryAmmo,
					PowerId.AndersonPassive
				},
				Tag = "hench_zaeed",
				Weapons = LoadoutWeapons.AssaultRifles | LoadoutWeapons.SniperRifles
			};

			// Henchmen
			Henchmen = new Dictionary<string, HenchProperty>
			{
				{
					Anderson.Tag, Anderson
				},
				{
					Aria.Tag, Aria
				},
				{
					Ashley.Tag, Ashley
				},
				{
					Edi.Tag, Edi
				},
				{
					Garrus.Tag, Garrus
				},
				{
					Grunt.Tag, Grunt
				},
				{
					Jack.Tag, Jack
				},
				{
					Jacob.Tag, Jacob
				},
				{
					James.Tag, James
				},
				{
					Javik.Tag, Javik
				},
				{
					Kaidan.Tag, Kaidan
				},
				{
					Kasumi.Tag, Kasumi
				},
				{
					Liara.Tag, Liara
				},
				{
					Miranda.Tag, Miranda
				},
				{
					Nyreen.Tag, Nyreen
				},
				{
					Samara.Tag, Samara
				},
				{
					Tali.Tag, Tali
				},
				{
					Wrex.Tag, Wrex
				},
				{
					Zaeed.Tag, Zaeed
				}
			};
		}

		public static Dictionary<string, HenchProperty> Henchmen { get; private set; }

		public static HenchProperty Anderson { get; private set; }
		public static HenchProperty Aria { get; private set; }
		public static HenchProperty Ashley { get; private set; }
		public static HenchProperty Edi { get; private set; }
		public static HenchProperty Garrus { get; private set; }
		public static HenchProperty Grunt { get; private set; }
		public static HenchProperty Jack { get; private set; }
		public static HenchProperty Jacob { get; private set; }
		public static HenchProperty James { get; private set; }
		public static HenchProperty Javik { get; private set; }
		public static HenchProperty Kaidan { get; private set; }
		public static HenchProperty Kasumi { get; private set; }
		public static HenchProperty Liara { get; private set; }
		public static HenchProperty Miranda { get; private set; }
		public static HenchProperty Nyreen { get; private set; }
		public static HenchProperty Samara { get; private set; }
		public static HenchProperty Tali { get; private set; }
		public static HenchProperty Wrex { get; private set; }
		public static HenchProperty Zaeed { get; private set; }
	}
}