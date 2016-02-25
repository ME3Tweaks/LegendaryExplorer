using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("DoorSaveRecord")]
	public class Door : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("CurrentState")]
		private byte _CurrentState;

		[OriginalName("DoorGUID")]
		private Guid _Guid;

		[OriginalName("OldState")]
		private byte _OldState;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Guid);
			stream.Serialize(ref _CurrentState);
			stream.Serialize(ref _OldState);
		}

		#region Properties

		[LocalizedDisplayName("Guid", typeof (Localization.Door))]
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

		[LocalizedDisplayName("CurrentState", typeof (Localization.Door))]
		public byte CurrentState
		{
			get { return _CurrentState; }
			set
			{
				if (value != _CurrentState)
				{
					_CurrentState = value;
					NotifyPropertyChanged("CurrentState");
				}
			}
		}

		[LocalizedDisplayName("OldState", typeof (Localization.Door))]
		public byte OldState
		{
			get { return _OldState; }
			set
			{
				if (value != _OldState)
				{
					_OldState = value;
					NotifyPropertyChanged("OldState");
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