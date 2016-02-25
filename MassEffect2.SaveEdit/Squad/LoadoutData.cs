using System.Collections.Generic;

namespace MassEffect2.SaveEdit.Squad
{
	public static class LoadoutData
	{
		public static class Weapons
		{
			// ReSharper disable InconsistentNaming
			public static List<string> AssaultRifles { get; private set; }
			public static List<string> HeavyWepaons { get; private set; }
			public static List<string> Pistols { get; private set; }
			public static List<string> Shotguns { get; private set; }
			public static List<string> SMGs { get; private set; }
			public static List<string> SniperRifles { get; private set; }

			public enum AssaultRifleNames
			{}

			public enum PistolNames
			{}

			public enum ShotgunNames
			{}

			public enum SMGNames
			{}

			public enum SniperRifleNames
			{}
			// ReSharper restore InconsistentNaming

			static Weapons()
			{
				AssaultRifles = new List<string>
				{
					"SFXGameContent.SFXWeapon_AssaultRifle_Argus",
					"SFXGameContent.SFXWeapon_AssaultRifle_Avenger",
					"SFXGameContent.SFXWeapon_AssaultRifle_Cobra",
					"SFXGameContent.SFXWeapon_AssaultRifle_Collector",
					"SFXGameContent.SFXWeapon_AssaultRifle_Falcon",
					"SFXGameContent.SFXWeapon_AssaultRifle_Geth",
					"SFXGameContent.SFXWeapon_AssaultRifle_Mattock",
					"SFXGameContent.SFXWeapon_AssaultRifle_Reckoning",
					"SFXGameContent.SFXWeapon_AssaultRifle_Revenant",
					"SFXGameContent.SFXWeapon_AssaultRifle_Saber",
					"SFXGameContent.SFXWeapon_AssaultRifle_Valkyrie",
					"SFXGameContent.SFXWeapon_AssaultRifle_Vindicator",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_AssaultRifle_Cerb_GUN01",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_AssaultRifle_Quarian",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_AssaultRifle_LMG_GUN02",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_AssaultRifle_Krogan_GUN02",
					"SFXGameContentDLC_CON_MP1.SFXWeapon_AssaultRifle_Krogan",
					"SFXGameContentDLC_CON_MP2.SFXWeapon_AssaultRifle_Prothean_MP",
					"SFXGameContentDLC_CON_MP2.SFXWeapon_AssaultRifle_Cerberus",
					"SFXGameContentDLC_CON_MP3.SFXWeapon_AssaultRifle_LMG",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Lancer_MP",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Adas_MP",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Spitfire",
					"SFXGameContentDLC_EXP_Pack003.SFXWeapon_AssaultRifle_Lancer",
					"SFXGameContentDLC_HEN_PR.SFXWeapon_AssaultRifle_Prothean"
				};

				HeavyWepaons = new List<string>
				{
					"SFXGameContent.SFXWeapon_Heavy_ArcProjector",
					"SFXGameContent.SFXWeapon_Heavy_Avalanche",
					"SFXGameContent.SFXWeapon_Heavy_Blackstorm",
					"SFXGameContent.SFXWeapon_Heavy_Cain",
					"SFXGameContent.SFXWeapon_Heavy_FlameThrower_Player",
					"SFXGameContent.SFXWeapon_Heavy_Geth02LaserTarget",
					"SFXGameContent.SFXWeapon_Heavy_GrenadeLauncher",
					"SFXGameContent.SFXWeapon_Heavy_LegionDisinfectionWeapon",
					"SFXGameContent.SFXWeapon_Heavy_LegionInfectionLauncher",
					"SFXGameContent.SFXWeapon_Heavy_LegionInfectionLauncher_Inaccurate",
					"SFXGameContent.SFXWeapon_Heavy_MiniGun",
					"SFXGameContent.SFXWeapon_Heavy_MissileLauncher",
					"SFXGameContent.SFXWeapon_Heavy_ParticleBeam",
					"SFXGameContent.SFXWeapon_Heavy_RocketLauncher",
					"SFXGameContent.SFXWeapon_Heavy_TitanMissileLauncher",
					"SFXGameContentDLC_EXP_Pack003.SFXWeapon_Heavy_Spitfire_Cit001"
				};

				Pistols = new List<string>
				{
					"SFXGameContent.SFXWeapon_Pistol_Carnifex",
					"SFXGameContent.SFXWeapon_Pistol_Eagle",
					"SFXGameContent.SFXWeapon_Pistol_Ivory",
					"SFXGameContent.SFXWeapon_Pistol_Phalanx",
					"SFXGameContent.SFXWeapon_Pistol_Scorpion",
					"SFXGameContent.SFXWeapon_Pistol_Predator",
					"SFXGameContent.SFXWeapon_Pistol_Talon",
					"SFXGameContent.SFXWeapon_Pistol_Thor",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_Pistol_Bloodpack",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_Pistol_Asari_GUN02",
					"SFXGameContentDLC_CON_MP3.SFXWeapon_Pistol_Asari",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_Pistol_Silencer_MP",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_Pistol_Bloodpack_MP",
					"SFXGameContentDLC_EXP_Pack003.SFXWeapon_Pistol_Silencer",
					"SFXGameContentDLC_EXP_Pack003.SFXWeapon_Pistol_Silencer_Cit001",
				};

				Shotguns = new List<string>
				{
					"SFXGameContent.SFXWeapon_Shotgun_Claymore",
					"SFXGameContent.SFXWeapon_Shotgun_Crusader",
					"SFXGameContent.SFXWeapon_Shotgun_Disciple",
					"SFXGameContent.SFXWeapon_Shotgun_Eviscerator",
					"SFXGameContent.SFXWeapon_Shotgun_Geth",
					"SFXGameContent.SFXWeapon_Shotgun_Graal",
					"SFXGameContent.SFXWeapon_Shotgun_Katana",
					"SFXGameContent.SFXWeapon_Shotgun_Raider",
					"SFXGameContent.SFXWeapon_Shotgun_Scimitar",
					"SFXGameContent.SFXWeapon_Shotgun_Striker",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_Shotgun_Quarian_GUN01",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_Shotgun_Salarian",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_Shotgun_Assault_GUN02",
					"SFXGameContentDLC_CON_MP2.SFXWeapon_Shotgun_Quarian",
					"SFXGameContentDLC_CON_MP3.SFXWeapon_Shotgun_Assault",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_Shotgun_Salarian_MP"
				};

				SMGs = new List<string>
				{
					"SFXGameContent.SFXWeapon_SMG_Hornet",
					"SFXGameContent.SFXWeapon_SMG_Hurricane",
					"SFXGameContent.SFXWeapon_SMG_Locust",
					"SFXGameContent.SFXWeapon_SMG_Shuriken",
					"SFXGameContent.SFXWeapon_SMG_Tempest",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_SMG_Geth_GUN01",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_SMG_Bloodpack",
					"SFXGameContentDLC_CON_MP1.SFXWeapon_SMG_Geth",
					"SFXGameContentDLC_CON_MP4.SFXWeapon_SMG_Collector",
					"SFXGameContentDLC_CON_MP5.SFXWeapon_SMG_Bloodpack_MP"
				};

				SniperRifles = new List<string>
				{
					"SFXGameContent.SFXWeapon_SniperRifle_BlackWidow",
					"SFXGameContent.SFXWeapon_SniperRifle_Incisor",
					"SFXGameContent.SFXWeapon_SniperRifle_Indra",
					"SFXGameContent.SFXWeapon_SniperRifle_Javelin",
					"SFXGameContent.SFXWeapon_SniperRifle_Mantis",
					"SFXGameContent.SFXWeapon_SniperRifle_Raptor",
					"SFXGameContent.SFXWeapon_SniperRifle_Valiant",
					"SFXGameContent.SFXWeapon_SniperRifle_Viper",
					"SFXGameContent.SFXWeapon_SniperRifle_Widow",
					"SFXGameContentDLC_CON_GUN01.SFXWeapon_SniperRifle_Turian_GUN01",
					"SFXGameContentDLC_CON_GUN02.SFXWeapon_Sniperrifle_Batarian_GUN02",
					"SFXGameContentDLC_CON_MP1.SFXWeapon_SniperRifle_Batarian",
					"SFXGameContentDLC_CON_MP2.SFXWeapon_SniperRifle_Turian",
					"SFXGameContentDLC_CON_MP4.SFXWeapon_SniperRifle_Collector"
				};
			}
		}

		public static class WeaponMods
		{
			// ReSharper disable InconsistentNaming
			public static Dictionary<AssaultRifleMods, string> AssaultRifles { get; private set; }
			public static Dictionary<PistolMods, string> Pistols { get; private set; }
			public static Dictionary<ShotgunMods, string> Shotguns { get; private set; }
			public static Dictionary<SMGMods, string> SMGs { get; private set; }
			public static Dictionary<SniperRifleMods, string> SniperRifles { get; private set; }

			public static string[] DefaultPlayerAssaultRifleMods { get; private set; }
			public static string[] DefaultPlayerPistolMods { get; private set; }
			public static string[] DefaultPlayerShotgunMods { get; private set; }
			public static string[] DefaultPlayerSMGMods { get; private set; }
			public static string[] DefaultPlayerSniperRifleMods { get; private set; }

			public static string[] DefaultHenchAssaultRifleMods { get; private set; }
			public static string[] DefaultHenchPistolMods { get; private set; }
			public static string[] DefaultHenchShotgunMods { get; private set; }
			public static string[] DefaultHenchSMGMods { get; private set; }
			public static string[] DefaultHenchSniperRifleMods { get; private set; }

			public enum AssaultRifleMods
			{
				Damage,
				MagSize,
				Melee,
				Penetration, // Force
				Scope, // Accuracy
				Stability,
				SuperPenetration,
				SuperScope,
				UltraLight,
				UltraLightMP5
			}

			public enum PistolMods
			{
				Damage,
				HeadShot,
				MagSize,
				MeleeDamage, // Stability
				Penetration, //ReloadSpeed
				PowerDamage,
				PowerDamageMP5,
				Scope, // Accuracy
				SuperDamage,
				UltraLight
			}

			public enum ShotgunMods
			{
				Accuracy,
				Damage,
				DamageAndPenetration,
				MeleeDamage,
				Penetration, // ReloadSpeed
				SpareAmmo, // Stability
				SuperMelee,
				UltraLight,
				UltraLightMP5
			}

			public enum SMGMods
			{
				Damage,
				MagSize,
				NoAmmoUsedChance, // ConstraintDamage
				Penetration,
				PowerDamage,
				PowerDamageMP5,
				Scope, // Accuracy
				Stability, // Stabilization
				UltraLight // Stability
			}

			public enum SniperRifleMods
			{
				Damage,
				DamageAndPenetration,
				Penetration, // ConstraintDamage
				Scope, // Accuracy
				SpareAmmo, // ReloadSpeed
				SuperScope,
				TimeDilation,
				UltraLight,
				UltraLightMP5
			}
			// ReSharper restore InconsistentNaming

			static WeaponMods()
			{
				AssaultRifles = new Dictionary<AssaultRifleMods, string>
				{
					{
						AssaultRifleMods.Damage,
						"SFXGameContent.SFXWeaponMod_AssaultRifleDamage"
					},
					{
						AssaultRifleMods.MagSize,
						"SFXGameContent.SFXWeaponMod_AssaultRifleMagSize"
					},
					{
						AssaultRifleMods.Melee,
						"SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleMelee"
					},
					{
						AssaultRifleMods.Penetration,
						"SFXGameContent.SFXWeaponMod_AssaultRifleForce"
					},
					{
						AssaultRifleMods.Scope,
						"SFXGameContent.SFXWeaponMod_AssaultRifleAccuracy"
					},
					{
						AssaultRifleMods.Stability,
						"SFXGameContent.SFXWeaponMod_AssaultRifleStability"
					},
					{
						AssaultRifleMods.SuperPenetration,
						"SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleSuperPen"
					},
					{
						AssaultRifleMods.SuperScope,
						"SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleSuperScope"
					},
					{
						AssaultRifleMods.UltraLight,
						"SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_AssaultRifleUltraLight"
					},
					{
						AssaultRifleMods.UltraLightMP5,
						"SFXGameContentDLC_CON_MP5.SFXWeaponMod_AssaultRifleUltraLight_MP5"
					}
				};

				Pistols = new Dictionary<PistolMods, string>
				{
					{
						PistolMods.Damage,
						"SFXGameContent.SFXWeaponMod_PistolDamage"
					},
					{
						PistolMods.HeadShot,
						"SFXGameContentDLC_Shared.SFXWeaponMod_PistolHeadShot"
					},
					{
						PistolMods.MagSize,
						"SFXGameContent.SFXWeaponMod_PistolMagSize"
					},
					{
						PistolMods.MeleeDamage,
						"SFXGameContent.SFXWeaponMod_PistolStability"
					},
					{
						PistolMods.Penetration,
						"SFXGameContent.SFXWeaponMod_PistolReloadSpeed"
					},
					{
						PistolMods.PowerDamage,
						"SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_PistolPowerDamage"
					},
					{
						PistolMods.PowerDamageMP5,
						"SFXGameContentDLC_CON_MP5.SFXWeaponMod_PistolPowerDamage_MP5"
					},
					{
						PistolMods.Scope,
						"SFXGameContent.SFXWeaponMod_PistolAccuracy"
					},
					{
						PistolMods.SuperDamage,
						"SFXGameContentDLC_Shared.SFXWeaponMod_PistolSuperDamage"
					},
					{
						PistolMods.UltraLight,
						"SFXGameContentDLC_Shared.SFXWeaponMod_PistolUltraLight"
					}
				};

				Shotguns = new Dictionary<ShotgunMods, string>
				{
					{
						ShotgunMods.Accuracy,
						"SFXGameContent.SFXWeaponMod_ShotgunAccuracy"
					},
					{
						ShotgunMods.Damage,
						"SFXGameContent.SFXWeaponMod_ShotgunDamage"
					},
					{
						ShotgunMods.DamageAndPenetration,
						"SFXGameContentDLC_Shared.SFXWeaponMod_ShotgunDamageAndPen"
					},
					{
						ShotgunMods.MeleeDamage,
						"SFXGameContent.SFXWeaponMod_ShotgunMeleeDamage"
					},
					{
						ShotgunMods.Penetration,
						"SFXGameContent.SFXWeaponMod_ShotgunReloadSpeed"
					},
					{
						ShotgunMods.SpareAmmo,
						"SFXGameContent.SFXWeaponMod_ShotgunStability"
					},
					{
						ShotgunMods.SuperMelee,
						"SFXGameContentDLC_Shared.SFXWeaponMod_ShotgunSuperMelee"
					},
					{
						ShotgunMods.UltraLight,
						"SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_ShotgunUltraLight"
					},
					{
						ShotgunMods.UltraLightMP5,
						"SFXGameContentDLC_CON_MP5.SFXWeaponMod_ShotgunUltraLight_MP5"
					}
				};

				SMGs = new Dictionary<SMGMods, string>
				{
					{
						SMGMods.Damage,
						"SFXGameContent.SFXWeaponMod_SMGDamage"
					},
					{
						SMGMods.MagSize,
						"SFXGameContent.SFXWeaponMod_SMGMagSize"
					},
					{
						SMGMods.NoAmmoUsedChance,
						"SFXGameContent.SFXWeaponMod_SMGConstraintDamage"
					},
					{
						SMGMods.Penetration,
						"SFXGameContentDLC_Shared.SFXWeaponMod_SMGPenetration"
					},
					{
						SMGMods.PowerDamage,
						"SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_SMGPowerDamage"
					},
					{
						SMGMods.PowerDamageMP5,
						"SFXGameContentDLC_CON_MP5.SFXWeaponMod_SMGPowerDamage_MP5"
					},
					{
						SMGMods.Scope,
						"SFXGameContent.SFXWeaponMod_SMGAccuracy"
					},
					{
						SMGMods.Stability,
						"SFXGameContentDLC_Shared.SFXWeaponMod_SMGStabilization"
					},
					{
						SMGMods.UltraLight,
						"SFXGameContent.SFXWeaponMod_SMGStability"
					}
				};

				SniperRifles = new Dictionary<SniperRifleMods, string>
				{
					{
						SniperRifleMods.Damage,
						"SFXGameContent.SFXWeaponMod_SniperRifleDamage"
					},
					{
						SniperRifleMods.DamageAndPenetration,
						"SFXGameContentDLC_Shared.SFXWeaponMod_SniperRifleDamageAndPen"
					},
					{
						SniperRifleMods.Penetration,
						"SFXGameContent.SFXWeaponMod_SniperRifleConstraintDamage"
					},
					{
						SniperRifleMods.Scope,
						"SFXGameContent.SFXWeaponMod_SniperRifleAccuracy"
					},
					{
						SniperRifleMods.SpareAmmo,
						"SFXGameContent.SFXWeaponMod_SniperRifleReloadSpeed"
					},
					{
						SniperRifleMods.SuperScope,
						"SFXGameContentDLC_Shared.SFXWeaponMod_SniperRifleSuperScope"
					},
					{
						SniperRifleMods.TimeDilation,
						"SFXGameContent.SFXWeaponMod_SniperRifleTimeDilation"
					},
					{
						SniperRifleMods.UltraLight,
						"SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_SniperRifleUltraLight"
					},
					{
						SniperRifleMods.UltraLightMP5,
						"SFXGameContentDLC_CON_MP5.SFXWeaponMod_SniperRifleUltraLight_MP5"
					}
				};

				// Default Player Mods
				DefaultPlayerAssaultRifleMods = new[]
				{
					AssaultRifles[AssaultRifleMods.SuperScope],
					AssaultRifles[AssaultRifleMods.SuperPenetration]
				};

				DefaultPlayerPistolMods = new[]
				{
					Pistols[PistolMods.PowerDamage],
					Pistols[PistolMods.SuperDamage]
				};

				DefaultPlayerShotgunMods = new[]
				{
					Shotguns[ShotgunMods.Accuracy],
					Shotguns[ShotgunMods.DamageAndPenetration]
				};

				DefaultPlayerSMGMods = new[]
				{
					SMGs[SMGMods.PowerDamage],
					SMGs[SMGMods.Penetration]
				};

				DefaultPlayerSniperRifleMods = new[]
				{
					SniperRifles[SniperRifleMods.TimeDilation],
					SniperRifles[SniperRifleMods.DamageAndPenetration]
				};


				// Default Henchman Mods
				DefaultHenchAssaultRifleMods = new[]
				{
					AssaultRifles[AssaultRifleMods.SuperPenetration],
					AssaultRifles[AssaultRifleMods.Melee]
				};

				DefaultHenchPistolMods = new[]
				{
					Pistols[PistolMods.PowerDamage],
					Pistols[PistolMods.SuperDamage]
				};

				DefaultHenchShotgunMods = new[]
				{
					Shotguns[ShotgunMods.SuperMelee],
					Shotguns[ShotgunMods.DamageAndPenetration]
				};

				DefaultHenchSMGMods = new[]
				{
					SMGs[SMGMods.PowerDamage],
					SMGs[SMGMods.Penetration]
				};

				DefaultHenchSniperRifleMods = new[]
				{
					SniperRifles[SniperRifleMods.Penetration],
					SniperRifles[SniperRifleMods.DamageAndPenetration]
				};
			}
		}
	}
}