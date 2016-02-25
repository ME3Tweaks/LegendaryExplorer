using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MassEffect3.SaveEdit.Squad;
using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit.SquadBasicTable
{
	internal static class ResetCharacters
	{
		public static List<Button> Build(Editor editor)
		{
			var buttons = new List<Button>();

			// Player Powers
			var resetPlayerPowersButton = new Button
			{
				AutoSize = true,
				Name = "resetPlayerPowersButton",
				Text = @"Reset Player Powers"
			};

			resetPlayerPowersButton.Click += (sender, args) => ResetPlayerPowers(editor.SaveFile);

			buttons.Add(resetPlayerPowersButton);

			// Player Weapon Mods
			var resetPlayerWeaponModsButton = new Button
			{
				AutoSize = true,
				Name = "resetPlayerWeaponModsButton",
				Text = @"Reset Player Weapon Mods"
			};

			resetPlayerWeaponModsButton.Click += (sender, args) => ResetPlayerWeaponMods(editor.SaveFile);

			buttons.Add(resetPlayerWeaponModsButton);

			// Henchmen Powers
			var resetHenchmenPowersButton = new Button
			{
				AutoSize = true,
				Name = "resetHenchmenPowersButton",
				Text = @"Reset Henchmen Powers"
			};

			resetHenchmenPowersButton.Click += (sender, args) => ResetHenchmenPowers(editor.SaveFile);

			buttons.Add(resetHenchmenPowersButton);

			// Henchmen Weapon Mods
			var resetHenchmenWeaponModsButton = new Button
			{
				AutoSize = true,
				Name = "resetHenchmenWeaponModsButton",
				Text = @"Reset Henchmen Weapon Mods"
			};

			resetHenchmenWeaponModsButton.Click += (sender, args) => ResetHenchmenWeaponMods(editor.SaveFile);

			buttons.Add(resetHenchmenWeaponModsButton);

			// Squad Powers
			var resetAllButton = new Button
			{
				AutoSize = true,
				Name = "resetPlayerPowersButton",
				Text = @"Reset All"
			};

			resetAllButton.Click += (sender, args) =>
			{
				ResetPlayerPowers(editor.SaveFile);
				ResetHenchmenPowers(editor.SaveFile);

				ResetPlayerWeaponMods(editor.SaveFile);
				ResetHenchmenWeaponMods(editor.SaveFile);
			};

			buttons.Add(resetAllButton);

			// Default Player Powers
			var resetDefaultPlayerPowersButton = new Button
			{
				AutoSize = true,
				Name = "resetDefaultPlayerPowersButton",
				Text = @"Default Player Powers"
			};

			resetDefaultPlayerPowersButton.Click += (sender, args) => ResetPlayerPowers(editor.SaveFile, true);

			buttons.Add(resetDefaultPlayerPowersButton);

			// Default Henchmen Powers
			var resetDefaultHenchmenPowersButton = new Button
			{
				AutoSize = true,
				Name = "resetDefaultHenchmenPowersButton",
				Text = @"Default Henchmen Powers"
			};

			resetDefaultHenchmenPowersButton.Click += (sender, args) => ResetHenchmenPowers(editor.SaveFile, true);

			buttons.Add(resetDefaultHenchmenPowersButton);

			return buttons;
		}

		internal static LoadoutDataWeaponMod GetDefaultWeaponMod(WeaponClassType weaponClassType)
		{
			return LoadoutData.DefaultWeaponMods.FirstOrDefault(mod => mod.WeaponType == weaponClassType);
		}

		internal static IEnumerable<WeaponMod> GetHenchmanWeaponMods(WeaponClassType weaponClassType)
		{
			var defaultWeaponMod = GetDefaultWeaponMod(weaponClassType);

			if (defaultWeaponMod == null)
			{
				return null;
			}

			var wc = LoadoutData.WeaponClasses.Where(w => w.WeaponType == weaponClassType);
			var wmc = LoadoutData.WeaponModClasses.Where(wm => wm.WeaponType == weaponClassType && defaultWeaponMod.Henchman.Contains(wm.Name));

			var result = wc.Select(weapon => new WeaponMod
			{
				WeaponClassName = weapon.ClassName,
				WeaponModClassNames = wmc.Select(modClass => modClass.ClassName).ToList()
			});

			return result;
			//var weaponMods = (from weaponModName in defaultWeaponMod.Henchman select SquadVariables.WeaponModClasses.FirstOrDefault(weaponMod => (weaponMod.WeaponType == WeaponClassType.AssaultRifle) && weaponMod.Name.Equals(weaponModName)) into firstOrDefault where firstOrDefault != null select firstOrDefault.ClassName).ToList();
		}

		internal static Squad.PlayerClass GetPlayerClassInfo(SFXSaveGameFile saveGame)
		{
			var friendlyName = saveGame.Player.ClassFriendlyName;
			//var playerClass = (PlayerCharacterClass) friendlyName;

			return LoadoutData.PlayerClasses.Find(p => p.DisplayName == friendlyName);
		}

		internal static IEnumerable<WeaponMod> GetPlayerWeaponMods(WeaponClassType weaponClassType)
		{
			var defaultWeaponMod = GetDefaultWeaponMod(weaponClassType);

			if (defaultWeaponMod == null)
			{
				return null;
			}

			var wc = LoadoutData.WeaponClasses.Where(w => w.WeaponType == weaponClassType);
			var wmc = LoadoutData.WeaponModClasses.Where(wm => wm.WeaponType == weaponClassType && defaultWeaponMod.Player.Contains(wm.Name));

			var result = wc.Select(weapon => new WeaponMod
			{
				WeaponClassName = weapon.ClassName,
				WeaponModClassNames = wmc.Select(modClass => modClass.ClassName).ToList()
			});

			return result;
			//var weaponMods = (from weaponModName in defaultWeaponMod.Henchman select SquadVariables.WeaponModClasses.FirstOrDefault(weaponMod => (weaponMod.WeaponType == WeaponClassType.AssaultRifle) && weaponMod.Name.Equals(weaponModName)) into firstOrDefault where firstOrDefault != null select firstOrDefault.ClassName).ToList();
		}

		internal static Henchman ResetHenchman(SFXSaveGameFile saveGame, HenchmanClass henchmanClass, bool resetDefaults = false)
		{
			var hench = new Henchman
			{
				CharacterLevel = saveGame.Player.Level,
				Grenades = 0,
				LoadoutWeapons = new Loadout
				{
					AssaultRifle = "None",
					HeavyWeapon = "None",
					Pistol = "None",
					Shotgun = "None",
					SubmachineGun = "None",
					SniperRifle = "None"
				},
				MappedPower = "None",
				Tag = henchmanClass.Tag
			};

			ResetHenchmanPowers(saveGame, hench, resetDefaults);
			ResetHenchmanWeaponMods(saveGame, hench);

			return hench;
		}

		internal static void ResetHenchmanPowers(SFXSaveGameFile saveGame, Henchman henchman, bool resetDefaults = false)
		{
			var henchProperty = LoadoutData.HenchmenClasses.FirstOrDefault(pair => pair.Tag.Equals(henchman.Tag, StringComparison.InvariantCultureIgnoreCase));

			if (henchProperty == null)
			{
				return;
			}

			henchman.Powers = new List<Power>();
			henchman.CharacterLevel = saveGame.Player.Level;
			henchman.TalentPoints = SquadVariables.GetHenchTalentPoints(henchman.CharacterLevel);

			foreach (var powerId in henchProperty.Powers)
			{
				PowerClass power = null;
				var pId = powerId;

				foreach (var powerClass in LoadoutData.PowerClasses.Where(powerClass => powerClass != null && powerClass.Name != null)
					.Where(powerClass => powerClass.Name.Equals(pId, StringComparison.InvariantCultureIgnoreCase)
										 || (powerClass.CustomName != null
											 && powerClass.CustomName.Equals(pId, StringComparison.InvariantCultureIgnoreCase))))
				{
					power = powerClass;
				}

				if (power == null)
				{
					continue;
				}

				henchman.Powers.Add(new Power(power.ClassName, power.Name));
			}

			if (!resetDefaults)
			{
				return;
			}

			foreach (var power in henchman.Powers)
			{
				var powerClass =
					LoadoutData.PowerClasses.FirstOrDefault(
						p => (p.ClassName != null && p.ClassName.Equals(power.ClassName, StringComparison.InvariantCultureIgnoreCase)));

				if (powerClass == null)
				{
					continue;
				}

				if (!henchProperty.DefaultPowers.Any(s => s.Equals(powerClass.Name, StringComparison.InvariantCultureIgnoreCase)
														|| s.Equals(powerClass.CustomName, StringComparison.InvariantCultureIgnoreCase)))
				{
					continue;
				}

				power.CurrentRank = 1;

				if (powerClass.PowerType != PowerClassType.None)
				{
					henchman.TalentPoints--;
				}
			}
		}

		internal static void ResetHenchmanWeaponMods(SFXSaveGameFile saveGame, Henchman henchman)
		{
			var henchProperty = LoadoutData.HenchmenClasses.Single(pair => pair.Tag == henchman.Tag);
			henchman.WeaponMods = new List<WeaponMod>();

			// Assault Rifles
			if (henchProperty.Weapons.Contains(WeaponClassType.AssaultRifle))
			{
				henchman.WeaponMods.AddRange(GetHenchmanWeaponMods(WeaponClassType.AssaultRifle));
			}

			// Pistols
			if (henchProperty.Weapons.Contains(WeaponClassType.Pistol))
			{
				henchman.WeaponMods.AddRange(GetHenchmanWeaponMods(WeaponClassType.Pistol));
			}

			// Shotguns
			if (henchProperty.Weapons.Contains(WeaponClassType.Shotgun))
			{
				henchman.WeaponMods.AddRange(GetHenchmanWeaponMods(WeaponClassType.Shotgun));
			}

			// SMG's
			if (henchProperty.Weapons.Contains(WeaponClassType.SMG))
			{
				henchman.WeaponMods.AddRange(GetHenchmanWeaponMods(WeaponClassType.SMG));
			}

			// Sniper Rifles
			if (henchProperty.Weapons.Contains(WeaponClassType.SniperRifle))
			{
				henchman.WeaponMods.AddRange(GetHenchmanWeaponMods(WeaponClassType.SniperRifle));
			}
		}

		internal static void ResetHenchmenPowers(SFXSaveGameFile saveGame, bool resetDefaults = false)
		{
			var previous = saveGame.Henchmen;
			saveGame.Henchmen = new List<Henchman>();

			foreach (var henchman in LoadoutData.HenchmenClasses)
			{
				var hench = previous.Find(h => h.Tag == henchman.Tag);

				if (hench != null)
				{
					ResetHenchmanPowers(saveGame, hench, resetDefaults);
				}
				else
				{
					hench = ResetHenchman(saveGame, henchman, resetDefaults);
					hench.WeaponMods.Clear();
				}

				saveGame.Henchmen.Add(hench);
			}
		}

		internal static void ResetHenchmenWeaponMods(SFXSaveGameFile saveGame)
		{
			var previous = saveGame.Henchmen;
			saveGame.Henchmen = new List<Henchman>();

			foreach (var henchman in LoadoutData.HenchmenClasses)
			{
				var hench = previous.Find(h => h.Tag == henchman.Tag);

				if (hench != null)
				{
					ResetHenchmanWeaponMods(saveGame, hench);
				}
				else
				{
					hench = ResetHenchman(saveGame, henchman);
					hench.Powers.Clear();
				}

				saveGame.Henchmen.Add(hench);
			}
		}

		internal static void ResetPlayerPowers(SFXSaveGameFile saveGame, bool resetDefaults = false)
		{
			var playerClass = GetPlayerClassInfo(saveGame);
			var previous = saveGame.Player.Powers;
			saveGame.Player.Powers = new List<Power>();
			saveGame.Player.TalentPoints = SquadVariables.GetPlayerTalentPoints(saveGame.Player.Level);

			foreach (var powerId in playerClass.Powers)
			{
				PowerClass power = null;
				var pId = powerId;

				foreach (var powerClass in LoadoutData.PowerClasses.Where(powerClass => powerClass != null && powerClass.Name != null)
					.Where(powerClass => powerClass.Name.Equals(pId, StringComparison.InvariantCultureIgnoreCase)
										 || (powerClass.CustomName != null
											 && powerClass.CustomName.Equals(pId, StringComparison.InvariantCultureIgnoreCase))))
				{
					power = powerClass;
				}

				if (power == null)
				{
					continue;
				}

				saveGame.Player.Powers.Add(new Power(power.ClassName, power.Name));
			}

			foreach (var power in previous)
			{
				if (saveGame.Player.Powers.FindIndex(pwr => pwr.Name.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)) >= 0)
				{
					continue;
				}

				var firstOrDefault =
					LoadoutData.PowerClasses.FirstOrDefault(
						powerClass => (powerClass.Name.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)) 
									  || (powerClass.CustomName != null && powerClass.CustomName.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)));

				if (firstOrDefault != null && firstOrDefault.PowerType == PowerClassType.Bonus)
				{
					saveGame.Player.Powers.Add(new Power(power.ClassName, power.Name));
				}

				//LoadoutData.PowerClasses.First(powerClass => powerClass.Name.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)).PowerType == PowerClassType.Bonus
				//LoadoutData.PowerClasses.First(powerClass => powerClass.CustomName != null && powerClass.CustomName.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)).PowerType == PowerClassType.Bonus
					
				/*if (LoadoutData.PowerClasses.First(powerClass => powerClass.Name.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)).PowerType == PowerClassType.Bonus)
				{
					saveGame.Player.Powers.Add(new Power(power.ClassName, power.Name));						
				}
				else if (LoadoutData.PowerClasses.First(powerClass => powerClass.CustomName != null && powerClass.CustomName.Equals(power.Name, StringComparison.InvariantCultureIgnoreCase)).PowerType == PowerClassType.Bonus)
				{
					saveGame.Player.Powers.Add(new Power(power.ClassName, power.Name));						
				}*/
			}

			if (!resetDefaults)
			{
				return;
			}

			foreach (var power in saveGame.Player.Powers)
			{
				var powerClass =
					LoadoutData.PowerClasses.FirstOrDefault(
						p => (p.ClassName != null && p.ClassName.Equals(power.ClassName, StringComparison.InvariantCultureIgnoreCase)));

				if (powerClass == null)
				{
					continue;
				}

				if (!playerClass.DefaultPowers.Any(s => s.Equals(powerClass.Name, StringComparison.InvariantCultureIgnoreCase)
														|| s.Equals(powerClass.CustomName, StringComparison.InvariantCultureIgnoreCase))
					&& powerClass.PowerType != PowerClassType.Bonus)
				{
					continue;
				}

				power.CurrentRank = 1;

				if (powerClass.PowerType != PowerClassType.None)
				{
					saveGame.Player.TalentPoints--;
				}
			}
		}

		internal static void ResetPlayerWeaponMods(SFXSaveGameFile saveGame)
		{
			// Assault Rifles
			saveGame.Player.WeaponMods = GetPlayerWeaponMods(WeaponClassType.AssaultRifle).ToList();

			// Pistols
			saveGame.Player.WeaponMods.AddRange(GetPlayerWeaponMods(WeaponClassType.Pistol));

			// Shotguns
			saveGame.Player.WeaponMods.AddRange(GetPlayerWeaponMods(WeaponClassType.Shotgun));

			// SMG's
			saveGame.Player.WeaponMods.AddRange(GetPlayerWeaponMods(WeaponClassType.SMG));

			// Sniper Rifles
			saveGame.Player.WeaponMods.AddRange(GetPlayerWeaponMods(WeaponClassType.SniperRifle));
		}
	}
}
