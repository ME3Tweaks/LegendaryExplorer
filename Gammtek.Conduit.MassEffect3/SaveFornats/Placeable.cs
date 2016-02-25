using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("PlaceableSaveRecord")]
	public class Placeable : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("PlaceableGUID")]
		private Guid _Guid;

		[OriginalName("IsDeactivated")]
		private byte _IsDeactivated;

		[OriginalName("IsDestroyed")]
		private byte _IsDestroyed;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Guid);
			stream.Serialize(ref _IsDestroyed);
			stream.Serialize(ref _IsDeactivated);
		}

		#region Properties

		[LocalizedDisplayName("Guid", typeof (Localization.Placeable))]
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

		[LocalizedDisplayName("IsDestroyed", typeof (Localization.Placeable))]
		public bool IsDestroyed
		{
			get { return _IsDestroyed != 0; }
			set
			{
				if (value != (_IsDestroyed != 0))
				{
					_IsDestroyed = value ? (byte) 1 : (byte) 0;
					NotifyPropertyChanged("IsDestroyed");
				}
			}
		}

		[LocalizedDisplayName("IsDeactivated", typeof (Localization.Placeable))]
		public bool IsDeactivated
		{
			get { return _IsDeactivated != 0; }
			set
			{
				if (value != (_IsDeactivated != 0))
				{
					_IsDeactivated = value ? (byte) 1 : (byte) 0;
					NotifyPropertyChanged("IsDeactivated");
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