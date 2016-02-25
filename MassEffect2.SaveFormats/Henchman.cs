using System.Collections.Generic;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("HenchmanSaveRecord")]
	public class Henchman : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("CharacterLevel")]
		private int _CharacterLevel;

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

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Tag);
			stream.Serialize(ref _Powers);
			stream.Serialize(ref _CharacterLevel);
			stream.Serialize(ref _TalentPoints);

			if (stream.Version >= 23)
			{
				stream.Serialize(ref _LoadoutWeapons);
			}

			if (stream.Version >= 29)
			{
				stream.Serialize(ref _MappedPower);
			}
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