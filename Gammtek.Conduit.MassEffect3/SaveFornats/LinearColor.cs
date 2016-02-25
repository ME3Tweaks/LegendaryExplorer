using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class LinearColor : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		private float _A;
		private float _B;
		private float _G;
		private float _R;

		#endregion

		public LinearColor()
			: this(0.0f, 0.0f, 0.0f, 0.0f)
		{}

		public LinearColor(float r, float g, float b, float a)
		{
			_R = r;
			_G = g;
			_B = b;
			_A = a;
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _R);
			stream.Serialize(ref _G);
			stream.Serialize(ref _B);
			stream.Serialize(ref _A);
		}

		public override string ToString()
		{
			return string.Format("{0}, {1}, {2}, {3}",
				_R,
				_G,
				_B,
				_A);
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("R", typeof (Localization.LinearColor))]
		public float R
		{
			get { return _R; }
			set
			{
				if (Equals(value, _R) == false)
				{
					_R = value;
					NotifyPropertyChanged("R");
				}
			}
		}

		[LocalizedDisplayName("G", typeof (Localization.LinearColor))]
		public float G
		{
			get { return _G; }
			set
			{
				if (Equals(value, _G) == false)
				{
					_G = value;
					NotifyPropertyChanged("G");
				}
			}
		}

		[LocalizedDisplayName("B", typeof (Localization.LinearColor))]
		public float B
		{
			get { return _B; }
			set
			{
				if (Equals(value, _B) == false)
				{
					_B = value;
					NotifyPropertyChanged("B");
				}
			}
		}

		[LocalizedDisplayName("A", typeof (Localization.LinearColor))]
		public float A
		{
			get { return _A; }
			set
			{
				if (Equals(value, _A) == false)
				{
					_A = value;
					NotifyPropertyChanged("A");
				}
			}
		}

		#endregion
	}
}