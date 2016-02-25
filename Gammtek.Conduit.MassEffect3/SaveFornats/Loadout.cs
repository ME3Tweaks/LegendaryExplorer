using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class Loadout : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		private string _AssaultRifle;
		private string _HeavyWeapon;
		private string _Pistol;
		private string _Shotgun;
		private string _SniperRifle;
		private string _SubmachineGun;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _AssaultRifle);
			stream.Serialize(ref _Shotgun);
			stream.Serialize(ref _SniperRifle);
			stream.Serialize(ref _SubmachineGun);
			stream.Serialize(ref _Pistol);
			stream.Serialize(ref _HeavyWeapon);
		}

		#region Properties

		[LocalizedDisplayName("AssaultRifle", typeof (Localization.Loadout))]
		public string AssaultRifle
		{
			get { return _AssaultRifle; }
			set
			{
				if (value != _AssaultRifle)
				{
					_AssaultRifle = value;
					NotifyPropertyChanged("AssaultRifle");
				}
			}
		}

		[LocalizedDisplayName("Shotgun", typeof (Localization.Loadout))]
		public string Shotgun
		{
			get { return _Shotgun; }
			set
			{
				if (value != _Shotgun)
				{
					_Shotgun = value;
					NotifyPropertyChanged("Shotgun");
				}
			}
		}

		[LocalizedDisplayName("SniperRifle", typeof (Localization.Loadout))]
		public string SniperRifle
		{
			get { return _SniperRifle; }
			set
			{
				if (value != _SniperRifle)
				{
					_SniperRifle = value;
					NotifyPropertyChanged("SniperRifle");
				}
			}
		}

		[LocalizedDisplayName("SubmachineGun", typeof (Localization.Loadout))]
		public string SubmachineGun
		{
			get { return _SubmachineGun; }
			set
			{
				if (value != _SubmachineGun)
				{
					_SubmachineGun = value;
					NotifyPropertyChanged("SubmachineGun");
				}
			}
		}

		[LocalizedDisplayName("Pistol", typeof (Localization.Loadout))]
		public string Pistol
		{
			get { return _Pistol; }
			set
			{
				if (value != _Pistol)
				{
					_Pistol = value;
					NotifyPropertyChanged("Pistol");
				}
			}
		}

		[LocalizedDisplayName("HeavyWeapon", typeof (Localization.Loadout))]
		public string HeavyWeapon
		{
			get { return _HeavyWeapon; }
			set
			{
				if (value != _HeavyWeapon)
				{
					_HeavyWeapon = value;
					NotifyPropertyChanged("HeavyWeapon");
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