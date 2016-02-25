using System.Collections.Generic;

namespace MassEffect3.SaveEdit.Squad
{
	public static class LoadoutData
	{
		static LoadoutData()
		{
			InitializeDefaultHenchmenClasses();
			InitializeDefaultPlayerClasses();
			InitializeDefaultPowerClasses();
			InitializeDefaultWeaponClasses();
			InitializeDefaultWeaponModClasses();
			InitializeDefaultWeaponMods();
		}

		public static IList<LoadoutDataWeaponMod> DefaultWeaponMods { get; set; }

		public static IList<HenchmanClass> HenchmenClasses { get; set; }

		public static List<PlayerClass> PlayerClasses { get; set; }

		public static IList<PowerClass> PowerClasses { get; set; }

		public static IList<WeaponClass> WeaponClasses { get; set; }

		public static IList<WeaponModClass> WeaponModClasses { get; set; }

		private static void InitializeDefaultHenchmenClasses()
		{
			HenchmenClasses =
				new List<HenchmanClass>
				{
					new HenchmanClass("SFXPawn_Anderson", "hench_anderson")
					{
						DefaultPowers =
							new[]
							{
								"ConcussiveShot"
							},
						Powers =
							new[]
							{
								"ConcussiveShot",
								"FragGrenade",
								"Fortification",
								"IncendiaryAmmo",
								"AndersonPassive"
							},
						Weapons =
							new[] { WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Aria", "hench_aria")
					{
						DefaultPowers =
							new[]
							{
								"Flare"
							},
						Powers =
							new[]
							{
								"Flare",
								"Reave",
								"Carnage",
								"AriaLash",
								"AriaPassive"
							},
						Weapons =
							new[] { WeaponClassType.Shotgun, WeaponClassType.SMG }
					},
					new HenchmanClass("SFXPawn_Ashley", "hench_ashley")
					{
						DefaultPowers =
							new[]
							{
								"InfernoGrenade"
							},
						Powers =
							new[]
							{
								"InfernoGrenade",
								"DisruptorAmmo",
								"ConcussiveShot",
								"Marksman",
								"AshleyPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.SniperRifle }
					},
					new HenchmanClass("SFXPawn_EDI", "hench_edi")
					{
						DefaultPowers =
							new[]
							{
								"Incinerate"
							},
						Powers =
							new[]
							{
								"Incinerate",
								"Overload",
								"DefenseMatrix",
								"Decoy",
								"EDIPassive"
							},
						Weapons =
							new[] { WeaponClassType.SMG, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Garrus", "hench_garrus")
					{
						DefaultPowers =
							new[]
							{
								"ConcussiveShot"
							},
						Powers =
							new[]
							{
								"ConcussiveShot",
								"Overload",
								"ArmorPiercingAmmo",
								"ProximityMine",
								"GarrusPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.SniperRifle }
					},
					new HenchmanClass("SFXPawn_Grunt", "hench_grunt")
					{
						DefaultPowers =
							new[]
							{
								"ConcussiveShot"
							},
						Powers =
							new[]
							{
								"ConcussiveShot",
								"IncendiaryAmmo",
								"Fortification",
								"FragGrenade",
								"GruntPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.Shotgun }
					},
					new HenchmanClass("SFXPawn_Jack", "hench_jack")
					{
						DefaultPowers =
							new[]
							{
								"Shockwave"
							},
						Powers =
							new[]
							{
								"Shockwave",
								"Pull",
								"Warp",
								"WarpAmmo",
								"JackPassive"
							},
						Weapons =
							new[] { WeaponClassType.Shotgun, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Jacob", "hench_jacob")
					{
						DefaultPowers =
							new[]
							{
								"IncendiaryAmmo"
							},
						Powers =
							new[]
							{
								"IncendiaryAmmo",
								"LiftGrenade",
								"Barrier",
								"Pull",
								"JacobPassive"
							},
						Weapons =
							new[] { WeaponClassType.Shotgun, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Kaidan", "hench_kaidan")
					{
						DefaultPowers =
							new[]
							{
								"Barrier"
							},
						Powers =
							new[]
							{
								"Barrier",
								"Reave",
								"Overload",
								"CryoBlast",
								"KaidanPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Kasumi", "hench_kasumi")
					{
						DefaultPowers =
							new[]
							{
								"KasumiTacticalCloak"
							},
						Powers =
							new[]
							{
								"KasumiTacticalCloak",
								"Overload",
								"ArmorPiercingAmmo",
								"Decoy",
								"KasumiPassive"
							},
						Weapons =
							new[] { WeaponClassType.SMG, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Liara", "hench_liara")
					{
						DefaultPowers =
							new[]
							{
								"Singularity"
							},
						Powers =
							new[]
							{
								"Singularity",
								"Stasis",
								"WarpAmmo",
								"Warp",
								"LiaraPassive"
							},
						Weapons =
							new[] { WeaponClassType.SMG, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Marine", "hench_marine")
					{
						DefaultPowers =
							new[]
							{
								"Fortification"
							},
						Powers =
							new[]
							{
								"Fortification",
								"FragGrenade",
								"IncendiaryAmmo",
								"Carnage",
								"JamesPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.Shotgun }
					},
					new HenchmanClass("SFXPawn_Miranda", "hench_miranda")
					{
						DefaultPowers =
							new[]
							{
								"Overload"
							},
						Powers =
							new[]
							{
								"Overload",
								"Warp",
								"Incinerate",
								"Reave",
								"MirandaPassive"
							},
						Weapons =
							new[] { WeaponClassType.SMG, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Nyreen", "hench_nyreen")
					{
						DefaultPowers =
							new[]
							{
								"Incinerate"
							},
						Powers =
							new[]
							{
								"Incinerate",
								"Overload",
								"BioticProtector",
								"LiftGrenade",
								"NyreenPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Prothean", "hench_prothean")
					{
						DefaultPowers =
							new[]
							{
								"DarkChannel"
							},
						Powers =
							new[]
							{
								"DarkChannel",
								"LiftGrenade",
								"Pull",
								"Slam",
								"JavikPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Samara", "hench_samara")
					{
						DefaultPowers =
							new[]
							{
								"Reave"
							},
						Powers =
							new[]
							{
								"Reave",
								"Warp",
								"Pull",
								"Throw",
								"SamaraPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.SMG }
					},
					new HenchmanClass("SFXPawn_Tali", "hench_tali")
					{
						DefaultPowers =
							new[]
							{
								"EnergyDrain"
							},
						Powers =
							new[]
							{
								"EnergyDrain",
								"Sabotage",
								"CombatDrone",
								"DefenseDrone",
								"TaliPassive"
							},
						Weapons =
							new[] { WeaponClassType.Shotgun, WeaponClassType.Pistol }
					},
					new HenchmanClass("SFXPawn_Wrex", "hench_wrex")
					{
						DefaultPowers =
							new[]
							{
								"Carnage"
							},
						Powers =
							new[]
							{
								"Carnage",
								"LiftGrenade",
								"Barrier",
								"StimulantPack",
								"WrexPassive"
							},
						Weapons =
							new[] { WeaponClassType.Shotgun, WeaponClassType.AssaultRifle }
					},
					new HenchmanClass("SFXPawn_Zaeed", "hench_zaeed")
					{
						DefaultPowers =
							new[]
							{
								"Carnage"
							},
						Powers =
							new[]
							{
								"Carnage",
								"ConcussiveShot",
								"DisruptorAmmo",
								"InfernoGrenade",
								"ZaeedPassive"
							},
						Weapons =
							new[] { WeaponClassType.AssaultRifle, WeaponClassType.SniperRifle }
					}
				};
		}

		private static void InitializeDefaultPlayerClasses()
		{
			PlayerClasses =
				new List<PlayerClass>
				{
					new PlayerClass(PlayerCharacterClass.Adept, "SFXGame.SFXPawn_PlayerAdept", "SFXGame.SFXPawn_PlayerAdeptNonCombat", 93954)
					{
						DefaultPowers =
							new[]
							{
								"Singularity",
								"Warp",
								"Unity"
							},
						Powers =
							new[]
							{
								"Warp",
								"Throw",
								"Shockwave",
								"Singularity",
								"Pull",
								"ClusterGrenade",
								"AdpetPassive",
								"AdeptMeleePassive",
								"Unity"
							}
					},
					new PlayerClass(PlayerCharacterClass.Engineer, "SFXGame.SFXPawn_PlayerEngineer", "SFXGame.SFXPawn_PlayerEngineerNonCombat", 93953)
					{
						DefaultPowers =
							new[]
							{
								"Incinerate",
								"CombatDrone",
								"Unity"
							},
						Powers =
							new[]
							{
								"Incinerate",
								"Overload",
								"CryoBlast",
								"CombatDrone",
								"Sabotage",
								"SentryTurret",
								"EngineerPassive",
								"EngineerMeleePassive",
								"Unity"
							}
					},
					new PlayerClass(PlayerCharacterClass.Infiltrator, "SFXGame.SFXPawn_PlayerInfiltrator", "SFXGame.SFXPawn_PlayerInfiltratorNonCombat", 93955)
					{
						DefaultPowers =
							new[]
							{
								"DisruptorAmmo",
								"TacticalCloak",
								"Unity"
							},
						Powers =
							new[]
							{
								"DisruptorAmmo",
								"CryoAmmo",
								"Incinerate",
								"TacticalCloak",
								"StickyGrenade",
								"Sabotage",
								"InfiltratorPassive",
								"InfiltratorMeleePassive",
								"Unity"
							}
					},
					new PlayerClass(PlayerCharacterClass.Sentinel, "SFXGame.SFXPawn_PlayerSentinel", "SFXGame.SFXPawn_PlayerSentinelNonCombat", 93957)
					{
						DefaultPowers =
							new[]
							{
								"Throw",
								"TechArmor",
								"Unity"
							},
						Powers =
							new[]
							{
								"Throw",
								"Warp",
								"LiftGrenade",
								"TechArmor",
								"Overload",
								"CryoBlast",
								"SentinelPassive",
								"SentinelMeleePassive",
								"Unity"
							}
					},
					new PlayerClass(PlayerCharacterClass.Soldier, "SFXGame.SFXPawn_PlayerSoldier", "SFXGame.SFXPawn_PlayerSoldierNonCombat", 93952)
					{
						DefaultPowers =
							new[]
							{
								"AdrenalineRush",
								"IncendiaryAmmo",
								"Unity"
							},
						Powers =
							new[]
							{
								"AdrenalineRush",
								"ConcussiveShot",
								"FragGrenade",
								"IncendiaryAmmo",
								"DisruptorAmmo",
								"CryoAmmo",
								"SoldierPassive",
								"SoldierMeleePassive",
								"Unity"
							}
					},
					new PlayerClass(PlayerCharacterClass.Vanguard, "SFXGame.SFXPawn_PlayerVanguard", "SFXGame.SFXPawn_PlayerVanguardNonCombat", 93956)
					{
						DefaultPowers =
							new[]
							{
								"IncendiaryAmmo",
								"BioticCharge",
								"Unity"
							},
						Powers =
							new[]
							{
								"IncendiaryAmmo",
								"CryoAmmo",
								"Pull",
								"BioticCharge",
								"Shockwave",
								"Nova",
								"VanguardPassive",
								"VanguardMeleePassive",
								"Unity"
							}
					}
				};
		}

		private static void InitializeDefaultPowerClasses()
		{
			PowerClasses = new List<PowerClass>
			{
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AdeptMeleePassive", "AdeptMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AdeptPassive", "AdeptPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_EngineerMeleePassive", "EngineerMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_EngineerPassive", "EngineerPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_InfiltratorMeleePassive", "InfiltratorMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_InfiltratorPassive", "InfiltratorPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_SentinelMeleePassive", "SentinelMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_SentinelPassive", "SentinelPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_SoldierMeleePassive", "SoldierMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_SoldierPassive", "SoldierPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_VanguardMeleePassive", "VanguardMeleePassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_VanguardPassive", "VanguardPassive", PowerClassType.Player),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AndersonPassive", "AndersonPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AshleyPassive", "AshleyPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_EDIPassive", "EDIPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_GarrusPassive", "GarrusPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_JimmyPassive", "JimmyPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_KaidenPassive", "KaidenPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_LiaraPassive", "LiaraPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_ProtheanPassive", "ProtheanPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_TaliPassive", "TaliPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_AriaPassive", "AriaPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_NyreenBubbleShield", "BubbleShield", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_NyreenPassive", "NyreenPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_Cloak_Kasumi", "Cloak", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_GruntPassive", "GruntPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_JackPassive", "JackPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_JacobPassive", "JacobPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_KasumiPassive", "KasumiPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_MirandaPassive", "MirandaPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_SamaraPassive", "SamaraPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_WrexPassive", "WrexPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_ZaeedPassive", "ZaeedPassive", PowerClassType.SinglePlayer),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_ArmorPiercingAmmo", "ArmorPiercingAmmo", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Barrier", "Barrier", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Carnage", "Carnage", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_DarkChannel", "DarkChannel", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Decoy", "Decoy", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_EnergyDrain", "EnergyDrain", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Fortification", "Fortification", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_GethShieldBoost", "GethShieldBoost", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_ProtectorDrone", "ProtectorDrone", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_InfernoGrenade", "InfernoGrenade", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Marksman", "Marksman", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_ProximityMine", "ProximityMine", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Reave", "Reave", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Slam", "Slam", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Stasis", "Stasis", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_WarpAmmo", "WarpAmmo", PowerClassType.Bonus),
				new PowerClass("SFXGameContentDLC_EXP_Pack001.SFXPowerCustomAction_Dominate", "Dominate", PowerClassType.Bonus),
				new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_AriaLash", "AriaLash", PowerClassType.Bonus),
				new PowerClass("SFXGameContentDLC_EXP_Pack002.SFXPowerCustomAction_BioticFlare", "Flare", PowerClassType.Bonus),
				new PowerClass("SFXGameContentDLC_EXP_Pack003.SFXPowerCustomAction_StimPack", "StimPack", PowerClassType.Bonus),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AdrenalineRush", "AdrenalineRush"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_BioticCharge", "BioticCharge"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_BioticGrenade", "BioticGrenade"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_CombatDrone", "CombatDrone"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_ConcussiveShot", "ConcussiveShot"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_CryoAmmo", "CryoAmmo"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_CryoBlast", "CryoBlast"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_DisruptorAmmo", "DisruptorAmmo"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_FragGrenade", "FragGrenade"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_IncendiaryAmmo", "IncendiaryAmmo"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Incinerate", "Incinerate"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_LiftGrenade", "LiftGrenade"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Discharge", "Discharge"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Overload", "Overload"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Pull", "Pull"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_AIHacking", "Hacking"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_SentryTurret", "SentryTurret"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Shockwave", "Shockwave"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Singularity", "Singularity"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_StickyGrenade", "StickyGrenade"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Cloak", "Cloak"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_TechArmor", "TechArmor"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Throw", "Throw"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Warp", "Warp"),
				new PowerClass("SFXGameContent.SFXPowerCustomAction_Unity", "Unity", PowerClassType.None)
			};
		}

		private static void InitializeDefaultWeaponClasses()
		{
			WeaponClasses = new List<WeaponClass>
			{
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Argus", "SFXWeapon_AssaultRifle_Argus", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Avenger", "SFXWeapon_AssaultRifle_Avenger", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Cobra", "SFXWeapon_AssaultRifle_Cobra", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Collector", "SFXWeapon_AssaultRifle_Collector", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Falcon", "SFXWeapon_AssaultRifle_Falcon", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Geth", "SFXWeapon_AssaultRifle_Geth", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Mattock", "SFXWeapon_AssaultRifle_Mattock", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Reckoning", "SFXWeapon_AssaultRifle_Reckoning", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Revenant", "SFXWeapon_AssaultRifle_Revenant", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Saber", "SFXWeapon_AssaultRifle_Saber", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Valkyrie", "SFXWeapon_AssaultRifle_Valkyrie", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_AssaultRifle_Vindicator", "SFXWeapon_AssaultRifle_Vindicator", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_AssaultRifle_Cerb_GUN01", "SFXWeapon_AssaultRifle_Cerb_GUN01", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_AssaultRifle_Quarian", "SFXWeapon_AssaultRifle_Quarian", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_AssaultRifle_LMG_GUN02", "SFXWeapon_AssaultRifle_LMG_GUN02", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_AssaultRifle_Krogan_GUN02", "SFXWeapon_AssaultRifle_Krogan_GUN02",
					WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP1.SFXWeapon_AssaultRifle_Krogan", "SFXWeapon_AssaultRifle_Krogan", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP2.SFXWeapon_AssaultRifle_Prothean_MP", "SFXWeapon_AssaultRifle_Prothean_MP", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP2.SFXWeapon_AssaultRifle_Cerberus", "SFXWeapon_AssaultRifle_Cerberus", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP3.SFXWeapon_AssaultRifle_LMG", "SFXWeapon_AssaultRifle_LMG", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Lancer_MP", "SFXWeapon_AssaultRifle_Lancer_MP", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Adas_MP", "SFXWeapon_AssaultRifle_Adas_MP", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_AssaultRifle_Spitfire", "SFXWeapon_AssaultRifle_Spitfire", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_EXP_Pack003.SFXWeapon_AssaultRifle_Lancer", "SFXWeapon_AssaultRifle_Lancer", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContentDLC_HEN_PR.SFXWeapon_AssaultRifle_Prothean", "SFXWeapon_AssaultRifle_Prothean", WeaponClassType.AssaultRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_ArcProjector", "SFXWeapon_Heavy_ArcProjector", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_Avalanche", "SFXWeapon_Heavy_Avalanche", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_Blackstorm", "SFXWeapon_Heavy_Blackstorm", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_Cain", "SFXWeapon_Heavy_Cain", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_FlameThrower_Player", "SFXWeapon_Heavy_FlameThrower_Player", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_Geth02LaserTarget", "SFXWeapon_Heavy_Geth02LaserTarget", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_GrenadeLauncher", "SFXWeapon_Heavy_GrenadeLauncher", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_LegionDisinfectionWeapon", "SFXWeapon_Heavy_LegionDisinfectionWeapon", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_LegionInfectionLauncher", "SFXWeapon_Heavy_LegionInfectionLauncher", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_LegionInfectionLauncher_Inaccurate", "SFXWeapon_Heavy_LegionInfectionLauncher_Inaccurate",
					WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_MiniGun", "SFXWeapon_Heavy_MiniGun", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_MissileLauncher", "SFXWeapon_Heavy_MissileLauncher", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_ParticleBeam", "SFXWeapon_Heavy_ParticleBeam", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_RocketLauncher", "SFXWeapon_Heavy_RocketLauncher", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Heavy_TitanMissileLauncher", "SFXWeapon_Heavy_TitanMissileLauncher", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContentDLC_EXP_Pack003.SFXWeapon_Heavy_Spitfire_Cit001", "SFXWeapon_Heavy_Spitfire_Cit001", WeaponClassType.HeavyWeapon),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Carnifex", "SFXWeapon_Pistol_Carnifex", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Eagle", "SFXWeapon_Pistol_Eagle", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Ivory", "SFXWeapon_Pistol_Ivory", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Phalanx", "SFXWeapon_Pistol_Phalanx", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Scorpion", "SFXWeapon_Pistol_Scorpion", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Predator", "SFXWeapon_Pistol_Predator", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Talon", "SFXWeapon_Pistol_Talon", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Pistol_Thor", "SFXWeapon_Pistol_Thor", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_Pistol_Bloodpack", "SFXWeapon_Pistol_Bloodpack", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_Pistol_Asari_GUN02", "SFXWeapon_Pistol_Asari_GUN02", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_CON_MP3.SFXWeapon_Pistol_Asari", "SFXWeapon_Pistol_Asari", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_Pistol_Silencer_MP", "SFXWeapon_Pistol_Silencer_MP", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_Pistol_Bloodpack_MP", "SFXWeapon_Pistol_Bloodpack_MP", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_EXP_Pack003.SFXWeapon_Pistol_Silencer", "SFXWeapon_Pistol_Silencer", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContentDLC_EXP_Pack003.SFXWeapon_Pistol_Silencer_Cit001", "SFXWeapon_Pistol_Silencer_Cit001", WeaponClassType.Pistol),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Claymore", "SFXWeapon_Shotgun_Claymore", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Crusader", "SFXWeapon_Shotgun_Crusader", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Disciple", "SFXWeapon_Shotgun_Disciple", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Eviscerator", "SFXWeapon_Shotgun_Eviscerator", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Geth", "SFXWeapon_Shotgun_Geth", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Graal", "SFXWeapon_Shotgun_Graal", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Katana", "SFXWeapon_Shotgun_Katana", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Raider", "SFXWeapon_Shotgun_Raider", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Scimitar", "SFXWeapon_Shotgun_Scimitar", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_Shotgun_Striker", "SFXWeapon_Shotgun_Striker", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_Shotgun_Quarian_GUN01", "SFXWeapon_Shotgun_Quarian_GUN01", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_Shotgun_Salarian", "SFXWeapon_Shotgun_Salarian", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_Shotgun_Assault_GUN02", "SFXWeapon_Shotgun_Assault_GUN02", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_MP2.SFXWeapon_Shotgun_Quarian", "SFXWeapon_Shotgun_Quarian", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_MP3.SFXWeapon_Shotgun_Assault", "SFXWeapon_Shotgun_Assault", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_Shotgun_Salarian_MP", "SFXWeapon_Shotgun_Salarian_MP", WeaponClassType.Shotgun),
				new WeaponClass("SFXGameContent.SFXWeapon_SMG_Hornet", "SFXWeapon_SMG_Hornet", WeaponClassType.SMG),
				new WeaponClass("SFXGameContent.SFXWeapon_SMG_Hurricane", "SFXWeapon_SMG_Hurricane", WeaponClassType.SMG),
				new WeaponClass("SFXGameContent.SFXWeapon_SMG_Locust", "SFXWeapon_SMG_Locust", WeaponClassType.SMG),
				new WeaponClass("SFXGameContent.SFXWeapon_SMG_Shuriken", "SFXWeapon_SMG_Shuriken", WeaponClassType.SMG),
				new WeaponClass("SFXGameContent.SFXWeapon_SMG_Tempest", "SFXWeapon_SMG_Tempest", WeaponClassType.SMG),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_SMG_Geth_GUN01", "SFXWeapon_SMG_Geth_GUN01", WeaponClassType.SMG),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_SMG_Bloodpack", "SFXWeapon_SMG_Bloodpack", WeaponClassType.SMG),
				new WeaponClass("SFXGameContentDLC_CON_MP1.SFXWeapon_SMG_Geth", "SFXWeapon_SMG_Geth", WeaponClassType.SMG),
				new WeaponClass("SFXGameContentDLC_CON_MP4.SFXWeapon_SMG_Collector", "SFXWeapon_SMG_Collector", WeaponClassType.SMG),
				new WeaponClass("SFXGameContentDLC_CON_MP5.SFXWeapon_SMG_Bloodpack_MP", "SFXWeapon_SMG_Bloodpack_MP", WeaponClassType.SMG),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_BlackWidow", "SFXWeapon_SniperRifle_BlackWidow", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Incisor", "SFXWeapon_SniperRifle_Incisor", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Indra", "SFXWeapon_SniperRifle_Indra", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Javelin", "SFXWeapon_SniperRifle_Javelin", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Mantis", "SFXWeapon_SniperRifle_Mantis", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Raptor", "SFXWeapon_SniperRifle_Raptor", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Valiant", "SFXWeapon_SniperRifle_Valiant", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Viper", "SFXWeapon_SniperRifle_Viper", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContent.SFXWeapon_SniperRifle_Widow", "SFXWeapon_SniperRifle_Widow", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN01.SFXWeapon_SniperRifle_Turian_GUN01", "SFXWeapon_SniperRifle_Turian_GUN01",
					WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContentDLC_CON_GUN02.SFXWeapon_Sniperrifle_Batarian_GUN02", "SFXWeapon_Sniperrifle_Batarian_GUN02",
					WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP1.SFXWeapon_SniperRifle_Batarian", "SFXWeapon_SniperRifle_Batarian", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP2.SFXWeapon_SniperRifle_Turian", "SFXWeapon_SniperRifle_Turian", WeaponClassType.SniperRifle),
				new WeaponClass("SFXGameContentDLC_CON_MP4.SFXWeapon_SniperRifle_Collector", "SFXWeapon_SniperRifle_Collector", WeaponClassType.SniperRifle)
			};
		}

		private static void InitializeDefaultWeaponModClasses()
		{
			WeaponModClasses = new List<WeaponModClass>
			{
				new WeaponModClass("SFXGameContent.SFXWeaponMod_AssaultRifleDamage", "Damage", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_AssaultRifleMagSize", "MagSize", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleMelee", "Melee", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_AssaultRifleForce", "Penetration", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_AssaultRifleAccuracy", "Scope", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_AssaultRifleStability", "Stability", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleSuperPen", "SuperPenetration", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_AssaultRifleSuperScope", "SuperScope", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_AssaultRifleUltraLight", "UltraLight", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContentDLC_CON_MP5.SFXWeaponMod_AssaultRifleUltraLight_MP5", "UltraLightMP5", WeaponClassType.AssaultRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_PistolDamage", "Damage", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_PistolHeadShot", "HeadShot", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_PistolMagSize", "MagSize", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_PistolStability", "MeleeDamage", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_PistolReloadSpeed", "Penetration", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_PistolPowerDamage", "PowerDamage", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContentDLC_CON_MP5.SFXWeaponMod_PistolPowerDamage_MP5", "PowerDamageMP5", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_PistolAccuracy", "Scope", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_PistolSuperDamage", "SuperDamage", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_PistolUltraLight", "UltraLight", WeaponClassType.Pistol),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_ShotgunAccuracy", "Accuracy", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_ShotgunDamage", "Damage", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_ShotgunDamageAndPen", "DamageAndPenetration", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_ShotgunMeleeDamage", "MeleeDamage", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_ShotgunReloadSpeed", "Penetration", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_ShotgunStability", "SpareAmmo", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_ShotgunSuperMelee", "SuperMelee", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_ShotgunUltraLight", "UltraLight", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContentDLC_CON_MP5.SFXWeaponMod_ShotgunUltraLight_MP5", "UltraLightMP5", WeaponClassType.Shotgun),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SMGDamage", "Damage", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SMGMagSize", "MagSize", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SMGConstraintDamage", "NoAmmoUsedChance", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_SMGPenetration", "Penetration", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_SMGPowerDamage", "PowerDamage", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContentDLC_CON_MP5.SFXWeaponMod_SMGPowerDamage_MP5", "PowerDamageMP5", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SMGAccuracy", "Scope", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_SMGStabilization", "Stability", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SMGStability", "UltraLight", WeaponClassType.SMG),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SniperRifleDamage", "Damage", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_SniperRifleDamageAndPen", "DamageAndPenetration", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SniperRifleConstraintDamage", "Penetration", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SniperRifleAccuracy", "Scope", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SniperRifleReloadSpeed", "SpareAmmo", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContentDLC_Shared.SFXWeaponMod_SniperRifleSuperScope", "SuperScope", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContent.SFXWeaponMod_SniperRifleTimeDilation", "TimeDilation", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContentDLC_EXP_Pack003.SFXWeaponMod_SniperRifleUltraLight", "UltraLight", WeaponClassType.SniperRifle),
				new WeaponModClass("SFXGameContentDLC_CON_MP5.SFXWeaponMod_SniperRifleUltraLight_MP5", "UltraLightMP5", WeaponClassType.SniperRifle)
			};
		}

		private static void InitializeDefaultWeaponMods()
		{
			DefaultWeaponMods = new List<LoadoutDataWeaponMod>
			{
				new LoadoutDataWeaponMod(WeaponClassType.AssaultRifle, new List<string> { "SuperScope", "SuperPenetration" },
					new List<string> { "SuperPenetration", "Melee" }),
				new LoadoutDataWeaponMod(WeaponClassType.Pistol, new List<string> { "UltraLight", "SuperDamage" },
					new List<string> { "PowerDamage", "SuperDamage" }),
				new LoadoutDataWeaponMod(WeaponClassType.Shotgun, new List<string> { "Accuracy", "DamageAndPenetration" },
					new List<string> { "SuperMelee", "DamageAndPenetration" }),
				new LoadoutDataWeaponMod(WeaponClassType.SMG, new List<string> { "Stability", "Penetration" }, new List<string> { "PowerDamage", "Penetration" }),
				new LoadoutDataWeaponMod(WeaponClassType.SniperRifle, new List<string> { "TimeDilation", "DamageAndPenetration" },
					new List<string> { "TimeDilation", "DamageAndPenetration" })
			};
		}
	}
}
