using System.Collections.Generic;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("HenchmanSaveRecord")]
	public class Henchman : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("CharacterLevel")]
		private int _CharacterLevel;

		[OriginalName("Grenades")]
		private int _Grenades;

		[OriginalName("LoadoutWeapons")]
		private Loadout _LoadoutWeapons = new Loadout();

		[OriginalName("MappedPower")]
		private string _MappedPower;

		[OriginalName("Powers")]
		private List<Power> _Powers = new List<Power>();

		[OriginalName("Tag")]
		private string _Tag;

		[OriginalName("TalentPoints")]
		private int _TalentPoints;

		[OriginalName("WeaponMods")]
		private List<WeaponMod> _WeaponMods = new List<WeaponMod>();

		[OriginalName("Weapons")]
		private List<Weapon> _Weapons = new List<Weapon>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Tag);
			stream.Serialize(ref _Powers);
			stream.Serialize(ref _CharacterLevel);
			stream.Serialize(ref _TalentPoints);
			stream.Serialize(ref _LoadoutWeapons, s => s.Version < 23, () => new Loadout());
			stream.Serialize(ref _MappedPower, s => s.Version < 29, () => null);
			stream.Serialize(ref _WeaponMods, s => s.Version < 45, () => new List<WeaponMod>());
			stream.Serialize(ref _Grenades, s => s.Version < 59, () => 0);
			stream.Serialize(ref _Weapons, s => s.Version < 59, () => new List<Weapon>());
		}

		#region Properties

		[LocalizedDisplayName("Tag", typeof (Localization.Henchman))]
		public string Tag
		{
			get { return _Tag; }
			set
			{
				if (value != _Tag)
				{
					_Tag = value;
					NotifyPropertyChanged("Tag");
				}
			}
		}

		[LocalizedDisplayName("Powers", typeof (Localization.Henchman))]
		public List<Power> Powers
		{
			get { return _Powers; }
			set
			{
				if (value != _Powers)
				{
					_Powers = value;
					NotifyPropertyChanged("Powers");
				}
			}
		}

		[LocalizedDisplayName("CharacterLevel", typeof (Localization.Henchman))]
		public int CharacterLevel
		{
			get { return _CharacterLevel; }
			set
			{
				if (value != _CharacterLevel)
				{
					_CharacterLevel = value;
					NotifyPropertyChanged("CharacterLevel");
				}
			}
		}

		[LocalizedDisplayName("TalentPoints", typeof (Localization.Henchman))]
		public int TalentPoints
		{
			get { return _TalentPoints; }
			set
			{
				if (value != _TalentPoints)
				{
					_TalentPoints = value;
					NotifyPropertyChanged("TalentPoints");
				}
			}
		}

		[LocalizedDisplayName("LoadoutWeapons", typeof (Localization.Henchman))]
		public Loadout LoadoutWeapons
		{
			get { return _LoadoutWeapons; }
			set
			{
				if (value != _LoadoutWeapons)
				{
					_LoadoutWeapons = value;
					NotifyPropertyChanged("LoadoutWeapons");
				}
			}
		}

		[LocalizedDisplayName("MappedPower", typeof (Localization.Henchman))]
		public string MappedPower
		{
			get { return _MappedPower; }
			set
			{
				if (value != _MappedPower)
				{
					_MappedPower = value;
					NotifyPropertyChanged("MappedPower");
				}
			}
		}

		[LocalizedDisplayName("WeaponMods", typeof (Localization.Henchman))]
		public List<WeaponMod> WeaponMods
		{
			get { return _WeaponMods; }
			set
			{
				if (value != _WeaponMods)
				{
					_WeaponMods = value;
					NotifyPropertyChanged("WeaponMods");
				}
			}
		}

		[LocalizedDisplayName("Grenades", typeof (Localization.Henchman))]
		public int Grenades
		{
			get { return _Grenades; }
			set
			{
				if (value != _Grenades)
				{
					_Grenades = value;
					NotifyPropertyChanged("Grenades");
				}
			}
		}

		[LocalizedDisplayName("Weapons", typeof (Localization.Henchman))]
		public List<Weapon> Weapons
		{
			get { return _Weapons; }
			set
			{
				if (value != _Weapons)
				{
					_Weapons = value;
					NotifyPropertyChanged("Weapons");
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