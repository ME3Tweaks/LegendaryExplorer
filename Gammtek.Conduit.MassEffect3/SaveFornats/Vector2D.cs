using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class Vector2D : ISerializable, INotifyPropertyChanged
	{
		private float _X;
		private float _Y;
		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _X);
			stream.Serialize(ref _Y);
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}",
				_X,
				_Y);
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("X", typeof (Localization.Vector2D))]
		public float X
		{
			get { return _X; }
			set
			{
				if (Equals(value, _X) == false)
				{
					_X = value;
					NotifyPropertyChanged("X");
				}
			}
		}

		[LocalizedDisplayName("Y", typeof (Localization.Vector2D))]
		public float Y
		{
			get { return _Y; }
			set
			{
				if (Equals(value, _Y) == false)
				{
					_Y = value;
					NotifyPropertyChanged("Y");
				}
			}
		}

		#endregion
	}
}