using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("WeaponSaveRecord")]
	public class Weapon : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("AmmoPowerName")]
		private string _AmmoPowerName;

		//[OriginalName("AmmoPowerSourceTag")]
		//private string _AmmoPowerSourceTag;

		[OriginalName("TotalAmmo")]
		private int _AmmoTotal;

		[OriginalName("AmmoUsedCount")]
		private int _AmmoUsedCount;

		[OriginalName("WeaponClassName")]
		private string _ClassName;

		[OriginalName("bCurrentWeapon")]
		private bool _CurrentWeapon;

		[OriginalName("bLastWeapon")]
		private bool _WasLastWeapon;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _ClassName);
			stream.Serialize(ref _AmmoUsedCount);
			stream.Serialize(ref _AmmoTotal);
			stream.Serialize(ref _CurrentWeapon);
			stream.Serialize(ref _WasLastWeapon);

			if (stream.Version >= 17)
			{
				stream.Serialize(ref _AmmoPowerName);
			}
		}

		public override string ToString()
		{
			return _ClassName;
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("ClassName", typeof (Localization.Weapon))]
		public string ClassName
		{
			get { return _ClassName; }
			set
			{
				if (value != _ClassName)
				{
					_ClassName = value;
					NotifyPropertyChanged("ClassName");
				}
			}
		}

		[LocalizedDisplayName("AmmoUsedCount", typeof (Localization.Weapon))]
		public int AmmoUsedCount
		{
			get { return _AmmoUsedCount; }
			set
			{
				if (value != _AmmoUsedCount)
				{
					_AmmoUsedCount = value;
					NotifyPropertyChanged("AmmoUsedCount");
				}
			}
		}

		[LocalizedDisplayName("AmmoTotal", typeof (Localization.Weapon))]
		public int AmmoTotal
		{
			get { return _AmmoTotal; }
			set
			{
				if (value != _AmmoTotal)
				{
					_AmmoTotal = value;
					NotifyPropertyChanged("AmmoTotal");
				}
			}
		}

		[LocalizedDisplayName("IsCurrentWeapon", typeof (Localization.Weapon))]
		public bool IsCurrentWeapon
		{
			get { return _CurrentWeapon; }
			set
			{
				if (value != _CurrentWeapon)
				{
					_CurrentWeapon = value;
					NotifyPropertyChanged("IsCurrentWeapon");
				}
			}
		}

		[LocalizedDisplayName("WasLastWeapon", typeof (Localization.Weapon))]
		public bool WasLastWeapon
		{
			get { return _WasLastWeapon; }
			set
			{
				if (value != _WasLastWeapon)
				{
					_WasLastWeapon = value;
					NotifyPropertyChanged("WasLastWeapon");
				}
			}
		}

		[LocalizedDisplayName("AmmoPowerName", typeof (Localization.Weapon))]
		public string AmmoPowerName
		{
			get { return _AmmoPowerName; }
			set
			{
				if (value != _AmmoPowerName)
				{
					_AmmoPowerName = value;
					NotifyPropertyChanged("AmmoPowerName");
				}
			}
		}

		/*[LocalizedDisplayName("AmmoPowerSourceTag", typeof (Localization.Weapon))]
		public string AmmoPowerSourceTag
		{
			get { return _AmmoPowerSourceTag; }
			set
			{
				if (value != _AmmoPowerSourceTag)
				{
					_AmmoPowerSourceTag = value;
					NotifyPropertyChanged("AmmoPowerSourceTag");
				}
			}
		}*/

		#endregion
	}
}