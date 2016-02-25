using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("KismetBoolSaveRecord")]
	public class KismetBool : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("BoolGUID")]
		private Guid _Guid;

		[OriginalName("Value")]
		private bool _Value;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Guid);
			stream.Serialize(ref _Value);
		}

		#region Properties

		[LocalizedDisplayName("Guid", typeof (Localization.KismetBool))]
		public Guid Guid
		{
			get { return _Guid; }
			set
			{
				if (value != _Guid)
				{
					_Guid = value;
					NotifyPropertyChanged("Guid");
				}
			}
		}

		[LocalizedDisplayName("Value", typeof (Localization.KismetBool))]
		public bool Value
		{
			get { return _Value; }
			set
			{
				if (value != _Value)
				{
					_Value = value;
					NotifyPropertyChanged("Value");
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