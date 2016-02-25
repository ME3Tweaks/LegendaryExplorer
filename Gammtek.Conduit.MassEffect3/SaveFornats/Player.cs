using System;
using System.Collections.Generic;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("PlayerSaveRecord")]
	public class Player : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("Appearance")]
		private Appearance _appearance = new Appearance();

		[OriginalName("srClassFriendlyName")]
		private int _classFriendlyName;

		[OriginalName("PlayerClassName")]
		private string _className;

		[OriginalName("Credits")]
		private int _credits;

		[OriginalName("CurrentFuel")]
		private float _currentFuel;

		[OriginalName("CurrentHealth")]
		private float _currentHealth;

		[OriginalName("CurrentXP")]
		private float _currentXP;

		[OriginalName("Eezo")]
		private int _eezo;

		[OriginalName("FaceCode")]
		private string _faceCode;

		[OriginalName("FirstName")]
		private string _firstName;

		[OriginalName("GAWAssets")]
		private List<GAWAsset> _gawAssets = new List<GAWAsset>();

		[OriginalName("Grenades")]
		private int _grenades;

		[OriginalName("CharacterGUID")]
		private Guid _guid;

		[OriginalName("HotKeys")]
		private List<HotKey> _hotKeys = new List<HotKey>();

		[OriginalName("Iridium")]
		private int _iridium;

		[OriginalName("bCombatPawn")]
		private bool _isCombatPawn;

		[OriginalName("bIsFemale")]
		private bool _isFemale;

		[OriginalName("bInjuredPawn")]
		private bool _isInjuredPawn;

		[OriginalName("LastName")]
		private int _lastName;

		[OriginalName("Level")]
		private int _level;

		[OriginalName("LoadoutWeaponGroups")]
		private List<int> _loadoutWeaponGroups = new List<int>();

		[OriginalName("LoadoutWeapons")]
		private Loadout _loadoutWeapons = new Loadout();

		[OriginalName("MappedPower1")]
		private string _mappedPower1;

		[OriginalName("MappedPower2")]
		private string _mappedPower2;

		[OriginalName("MappedPower3")]
		private string _mappedPower3;

		[OriginalName("Medigel")]
		private int _medigel;

		[OriginalName("Notoriety")]
		private NotorietyType _notoriety;

		[OriginalName("Origin")]
		private OriginType _origin;

		[OriginalName("Palladium")]
		private int _palladium;

		[OriginalName("Platinum")]
		private int _platinum;

		[OriginalName("Powers")]
		private List<Power> _powers = new List<Power>();

		[OriginalName("PrimaryWeapon")]
		private string _primaryWeapon;

		[OriginalName("Probes")]
		private int _probes;

		[OriginalName("SecondaryWeapon")]
		private string _secondaryWeapon;

		[OriginalName("TalentPoints")]
		private int _talentPoints;

		[OriginalName("bUseCasualAppearance")]
		private bool _useCasualAppearance;

		[OriginalName("WeaponMods")]
		private List<WeaponMod> _weaponMods = new List<WeaponMod>();

		[OriginalName("Weapons")]
		private List<Weapon> _weapons = new List<Weapon>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _isFemale);
			stream.Serialize(ref _className);
			stream.Serialize(ref _isCombatPawn, s => s.Version < 37, () => true);
			stream.Serialize(ref _isInjuredPawn, s => s.Version < 48, () => false);
			stream.Serialize(ref _useCasualAppearance, s => s.Version < 48, () => false);
			stream.Serialize(ref _level);
			stream.Serialize(ref _currentXP);
			stream.Serialize(ref _firstName);
			stream.Serialize(ref _lastName);
			stream.SerializeEnum(ref _origin);
			stream.SerializeEnum(ref _notoriety);
			stream.Serialize(ref _talentPoints);
			stream.Serialize(ref _mappedPower1);
			stream.Serialize(ref _mappedPower2);
			stream.Serialize(ref _mappedPower3);
			stream.Serialize(ref _appearance);
			stream.Serialize(ref _powers);
			stream.Serialize(ref _gawAssets, s => s.Version < 38, () => new List<GAWAsset>());
			stream.Serialize(ref _weapons);
			stream.Serialize(ref _weaponMods, s => s.Version < 32, () => new List<WeaponMod>());
			stream.Serialize(ref _loadoutWeapons, s => s.Version < 18, () => new Loadout());
			stream.Serialize(ref _primaryWeapon, s => s.Version < 41, () => null);
			stream.Serialize(ref _secondaryWeapon, s => s.Version < 41, () => null);
			stream.Serialize(ref _loadoutWeaponGroups, s => s.Version < 33, () => new List<int>());
			stream.Serialize(ref _hotKeys, s => s.Version < 19, () => new List<HotKey>());
			stream.Serialize(ref _currentHealth, s => s.Version < 44, () => 0.0f);
			stream.Serialize(ref _credits);
			stream.Serialize(ref _medigel);
			stream.Serialize(ref _eezo);
			stream.Serialize(ref _iridium);
			stream.Serialize(ref _palladium);
			stream.Serialize(ref _platinum);
			stream.Serialize(ref _probes);
			stream.Serialize(ref _currentFuel);
			stream.Serialize(ref _grenades, s => s.Version < 54, () => 0);

			if (stream.Version >= 25)
			{
				stream.Serialize(ref _faceCode);
			}
			else
			{
				throw new NotSupportedException();
			}

			stream.Serialize(ref _classFriendlyName, s => s.Version < 26, () => 0);
			stream.Serialize(ref _guid, s => s.Version < 42, () => Guid.Empty);
		}

		#region Properties

		[LocalizedDisplayName("IsFemale", typeof (Localization.Player))]
		public bool IsFemale
		{
			get { return _isFemale; }
			set
			{
				if (value != _isFemale)
				{
					_isFemale = value;
					NotifyPropertyChanged("IsFemale");
				}
			}
		}

		[LocalizedDisplayName("ClassName", typeof (Localization.Player))]
		public string ClassName
		{
			get { return _className; }
			set
			{
				if (value != _className)
				{
					_className = value;
					ClassFriendlyName = PlayerClasses.Classes.Find(player => player.ClassName == value).DisplayName;
					NotifyPropertyChanged("ClassName");
				}
			}
		}

		[LocalizedDisplayName("ClassFriendlyName", typeof (Localization.Player))]
		public int ClassFriendlyName
		{
			get { return _classFriendlyName; }
			set
			{
				if (value != _classFriendlyName)
				{
					_classFriendlyName = value;
					NotifyPropertyChanged("ClassFriendlyName");
				}
			}
		}

		[LocalizedDisplayName("IsCombatPawn", typeof (Localization.Player))]
		public bool IsCombatPawn
		{
			get { return _isCombatPawn; }
			set
			{
				if (value != _isCombatPawn)
				{
					_isCombatPawn = value;
					NotifyPropertyChanged("IsCombatPawn");
				}
			}
		}

		[LocalizedDisplayName("IsInjuredPawn", typeof (Localization.Player))]
		public bool IsInjuredPawn
		{
			get { return _isInjuredPawn; }
			set
			{
				if (value != _isInjuredPawn)
				{
					_isInjuredPawn = value;
					NotifyPropertyChanged("IsInjuredPawn");
				}
			}
		}

		[LocalizedDisplayName("UseCasualAppearance", typeof (Localization.Player))]
		public bool UseCasualAppearance
		{
			get { return _useCasualAppearance; }
			set
			{
				if (value != _useCasualAppearance)
				{
					_useCasualAppearance = value;
					NotifyPropertyChanged("UseCasualAppearance");
				}
			}
		}

		[LocalizedDisplayName("Level", typeof (Localization.Player))]
		public int Level
		{
			get { return _level; }
			set
			{
				if (value != _level)
				{
					_level = value;
					NotifyPropertyChanged("Level");
				}
			}
		}

		[LocalizedDisplayName("CurrentXP", typeof (Localization.Player))]
		public float CurrentXP
		{
			get { return _currentXP; }
			set
			{
				if (Equals(value, _currentXP) == false)
				{
					_currentXP = value;
					NotifyPropertyChanged("CurrentXP");
				}
			}
		}

		[LocalizedDisplayName("FirstName", typeof (Localization.Player))]
		public string FirstName
		{
			get { return _firstName; }
			set
			{
				if (value != _firstName)
				{
					_firstName = value;
					NotifyPropertyChanged("FirstName");
				}
			}
		}

		[LocalizedDisplayName("LastName", typeof (Localization.Player))]
		public int LastName
		{
			get { return _lastName; }
			set
			{
				if (value != _lastName)
				{
					_lastName = value;
					NotifyPropertyChanged("LastName");
				}
			}
		}

		[LocalizedDisplayName("Origin", typeof (Localization.Player))]
		public OriginType Origin
		{
			get { return _origin; }
			set
			{
				if (value != _origin)
				{
					_origin = value;
					NotifyPropertyChanged("Origin");
				}
			}
		}

		[LocalizedDisplayName("Notoriety", typeof (Localization.Player))]
		public NotorietyType Notoriety
		{
			get { return _notoriety; }
			set
			{
				if (value != _notoriety)
				{
					_notoriety = value;
					NotifyPropertyChanged("Notoriety");
				}
			}
		}

		[LocalizedDisplayName("TalentPoints", typeof (Localization.Player))]
		public int TalentPoints
		{
			get { return _talentPoints; }
			set
			{
				if (value != _talentPoints)
				{
					_talentPoints = value;
					NotifyPropertyChanged("TalentPoints");
				}
			}
		}

		[LocalizedDisplayName("MappedPower1", typeof (Localization.Player))]
		public string MappedPower1
		{
			get { return _mappedPower1; }
			set
			{
				if (value != _mappedPower1)
				{
					_mappedPower1 = value;
					NotifyPropertyChanged("MappedPower1");
				}
			}
		}

		[LocalizedDisplayName("MappedPower2", typeof (Localization.Player))]
		public string MappedPower2
		{
			get { return _mappedPower2; }
			set
			{
				if (value != _mappedPower2)
				{
					_mappedPower2 = value;
					NotifyPropertyChanged("MappedPower2");
				}
			}
		}

		[LocalizedDisplayName("MappedPower3", typeof (Localization.Player))]
		public string MappedPower3
		{
			get { return _mappedPower3; }
			set
			{
				if (value != _mappedPower3)
				{
					_mappedPower3 = value;
					NotifyPropertyChanged("MappedPower3");
				}
			}
		}

		[LocalizedDisplayName("Appearance", typeof (Localization.Player))]
		public Appearance Appearance
		{
			get { return _appearance; }
			set
			{
				if (value != _appearance)
				{
					_appearance = value;
					NotifyPropertyChanged("Appearance");
				}
			}
		}

		[LocalizedDisplayName("Powers", typeof (Localization.Player))]
		public List<Power> Powers
		{
			get { return _powers; }
			set
			{
				if (value != _powers)
				{
					_powers = value;
					NotifyPropertyChanged("Powers");
				}
			}
		}

		[LocalizedDisplayName("GAWAssets", typeof (Localization.Player))]
		public List<GAWAsset> GAWAssets
		{
			get { return _gawAssets; }
			set
			{
				if (value != _gawAssets)
				{
					_gawAssets = value;
					NotifyPropertyChanged("GAWAssets");
				}
			}
		}

		[LocalizedDisplayName("Weapons", typeof (Localization.Player))]
		public List<Weapon> Weapons
		{
			get { return _weapons; }
			set
			{
				if (value != _weapons)
				{
					_weapons = value;
					NotifyPropertyChanged("Weapons");
				}
			}
		}

		[LocalizedDisplayName("WeaponMods", typeof (Localization.Player))]
		public List<WeaponMod> WeaponMods
		{
			get { return _weaponMods; }
			set
			{
				if (value != _weaponMods)
				{
					_weaponMods = value;
					NotifyPropertyChanged("WeaponMods");
				}
			}
		}

		[LocalizedDisplayName("LoadoutWeapons", typeof (Localization.Player))]
		public Loadout LoadoutWeapons
		{
			get { return _loadoutWeapons; }
			set
			{
				if (value != _loadoutWeapons)
				{
					_loadoutWeapons = value;
					NotifyPropertyChanged("LoadoutWeapons");
				}
			}
		}

		[LocalizedDisplayName("PrimaryWeapon", typeof (Localization.Player))]
		public string PrimaryWeapon
		{
			get { return _primaryWeapon; }
			set
			{
				if (value != _primaryWeapon)
				{
					_primaryWeapon = value;
					NotifyPropertyChanged("PrimaryWeapon");
				}
			}
		}

		[LocalizedDisplayName("SecondaryWeapon", typeof (Localization.Player))]
		public string SecondaryWeapon
		{
			get { return _secondaryWeapon; }
			set
			{
				if (value != _secondaryWeapon)
				{
					_secondaryWeapon = value;
					NotifyPropertyChanged("SecondaryWeapon");
				}
			}
		}

		[LocalizedDisplayName("LoadoutWeaponGroups", typeof (Localization.Player))]
		public List<int> LoadoutWeaponGroups
		{
			get { return _loadoutWeaponGroups; }
			set
			{
				if (value != _loadoutWeaponGroups)
				{
					_loadoutWeaponGroups = value;
					NotifyPropertyChanged("LoadoutWeaponGroups");
				}
			}
		}

		[LocalizedDisplayName("HotKeys", typeof (Localization.Player))]
		public List<HotKey> HotKeys
		{
			get { return _hotKeys; }
			set
			{
				if (value != _hotKeys)
				{
					_hotKeys = value;
					NotifyPropertyChanged("HotKeys");
				}
			}
		}

		[LocalizedDisplayName("CurrentHealth", typeof (Localization.Player))]
		public float CurrentHealth
		{
			get { return _currentHealth; }
			set
			{
				if (Equals(value, _currentHealth) == false)
				{
					_currentHealth = value;
					NotifyPropertyChanged("CurrentHealth");
				}
			}
		}

		[LocalizedDisplayName("Credits", typeof (Localization.Player))]
		public int Credits
		{
			get { return _credits; }
			set
			{
				if (value != _credits)
				{
					_credits = value;
					NotifyPropertyChanged("Credits");
				}
			}
		}

		[LocalizedDisplayName("Medigel", typeof (Localization.Player))]
		public int Medigel
		{
			get { return _medigel; }
			set
			{
				if (value != _medigel)
				{
					_medigel = value;
					NotifyPropertyChanged("Medigel");
				}
			}
		}

		[LocalizedDisplayName("Eezo", typeof (Localization.Player))]
		public int Eezo
		{
			get { return _eezo; }
			set
			{
				if (value != _eezo)
				{
					_eezo = value;
					NotifyPropertyChanged("Eezo");
				}
			}
		}

		[LocalizedDisplayName("Iridium", typeof (Localization.Player))]
		public int Iridium
		{
			get { return _iridium; }
			set
			{
				if (value != _iridium)
				{
					_iridium = value;
					NotifyPropertyChanged("Iridium");
				}
			}
		}

		[LocalizedDisplayName("Palladium", typeof (Localization.Player))]
		public int Palladium
		{
			get { return _palladium; }
			set
			{
				if (value != _palladium)
				{
					_palladium = value;
					NotifyPropertyChanged("Palladium");
				}
			}
		}

		[LocalizedDisplayName("Platinum", typeof (Localization.Player))]
		public int Platinum
		{
			get { return _platinum; }
			set
			{
				if (value != _platinum)
				{
					_platinum = value;
					NotifyPropertyChanged("Platinum");
				}
			}
		}

		[LocalizedDisplayName("Probes", typeof (Localization.Player))]
		public int Probes
		{
			get { return _probes; }
			set
			{
				if (value != _probes)
				{
					_probes = value;
					NotifyPropertyChanged("Probes");
				}
			}
		}

		[LocalizedDisplayName("CurrentFuel", typeof (Localization.Player))]
		public float CurrentFuel
		{
			get { return _currentFuel; }
			set
			{
				if (Equals(value, _currentFuel) == false)
				{
					_currentFuel = value;
					NotifyPropertyChanged("CurrentFuel");
				}
			}
		}

		[LocalizedDisplayName("Grenades", typeof (Localization.Player))]
		public int Grenades
		{
			get { return _grenades; }
			set
			{
				if (Equals(value, _grenades) == false)
				{
					_grenades = value;
					NotifyPropertyChanged("Grenades");
				}
			}
		}

		[LocalizedDisplayName("FaceCode", typeof (Localization.Player))]
		public string FaceCode
		{
			get { return _faceCode; }
			set
			{
				if (value != _faceCode)
				{
					_faceCode = value;
					NotifyPropertyChanged("FaceCode");
				}
			}
		}

		[LocalizedDisplayName("Guid", typeof (Localization.Player))]
		public Guid Guid
		{
			get { return _guid; }
			set
			{
				if (value != _guid)
				{
					_guid = value;
					NotifyPropertyChanged("Guid");
				}
			}
		}

		#endregion

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}