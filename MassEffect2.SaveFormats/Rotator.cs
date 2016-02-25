using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class Rotator : ISerializable, INotifyPropertyChanged
	{
		private int _Pitch;
		private int _Roll;
		private int _Yaw;
		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Pitch);
			stream.Serialize(ref _Yaw);
			stream.Serialize(ref _Roll);
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}",
				_Pitch,
				_Yaw,
				_Roll);
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("Pitch", typeof (Localization.Rotator))]
		public int Pitch
		{
			get { return _Pitch; }
			set
			{
				if (value != _Pitch)
				{
					_Pitch = value;
					NotifyPropertyChanged("Pitch");
				}
			}
		}

		[LocalizedDisplayName("Yaw", typeof (Localization.Rotator))]
		public int Yaw
		{
			get { return _Yaw; }
			set
			{
				if (value != _Yaw)
				{
					_Yaw = value;
					NotifyPropertyChanged("Yaw");
				}
			}
		}

		[LocalizedDisplayName("Roll", typeof (Localization.Rotator))]
		public int Roll
		{
			get { return _Roll; }
			set
			{
				if (value != _Roll)
				{
					_Roll = value;
					NotifyPropertyChanged("Roll");
				}
			}
		}

		#endregion
	}
}