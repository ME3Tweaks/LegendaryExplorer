using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class Loadout : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		private string _unknown0;
		private string _unknown1;
		private string _unknown2;
		private string _unknown3;
		private string _unknown4;
		private string _unknown5;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _unknown0);
			stream.Serialize(ref _unknown3);
			stream.Serialize(ref _unknown4);
			stream.Serialize(ref _unknown5);
			stream.Serialize(ref _unknown2);
			stream.Serialize(ref _unknown1);
		}

		#region Properties

		[LocalizedDisplayName("Unknown0", typeof(Localization.Loadout))]
		public string Unknown0
		{
			get { return _unknown0; }
			set
			{
				if (value != _unknown0)
				{
					_unknown0 = value;
					NotifyPropertyChanged("Unknown0");
				}
			}
		}

		[LocalizedDisplayName("Unknown1", typeof(Localization.Loadout))]
		public string Unknown1
		{
			get { return _unknown1; }
			set
			{
				if (value != _unknown1)
				{
					_unknown1 = value;
					NotifyPropertyChanged("Unknown1");
				}
			}
		}

		[LocalizedDisplayName("Unknown2", typeof(Localization.Loadout))]
		public string Unknown2
		{
			get { return _unknown2; }
			set
			{
				if (value != _unknown2)
				{
					_unknown2 = value;
					NotifyPropertyChanged("Unknown2");
				}
			}
		}

		[LocalizedDisplayName("Unknown3", typeof(Localization.Loadout))]
		public string Unknown3
		{
			get { return _unknown3; }
			set
			{
				if (value != _unknown3)
				{
					_unknown3 = value;
					NotifyPropertyChanged("Unknown3");
				}
			}
		}

		[LocalizedDisplayName("Unknown4", typeof(Localization.Loadout))]
		public string Unknown4
		{
			get { return _unknown4; }
			set
			{
				if (value != _unknown4)
				{
					_unknown4 = value;
					NotifyPropertyChanged("Unknown4");
				}
			}
		}

		[LocalizedDisplayName("Unknown5", typeof(Localization.Loadout))]
		public string Unknown5
		{
			get { return _unknown5; }
			set
			{
				if (value != _unknown5)
				{
					_unknown5 = value;
					NotifyPropertyChanged("Unknown5");
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