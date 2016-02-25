using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("StreamingStateSaveRecord")]
	public class StreamingState : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("bActive")]
		private bool _IsActive;

		[OriginalName("Name")]
		private string _Name;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Name);
			stream.Serialize(ref _IsActive);
		}

		#region Properties

		[LocalizedDisplayName("Name", typeof (Localization.StreamingState))]
		public string Name
		{
			get { return _Name; }
			set
			{
				if (value != _Name)
				{
					_Name = value;
					NotifyPropertyChanged("Name");
				}
			}
		}

		[LocalizedDisplayName("IsActive", typeof (Localization.StreamingState))]
		public bool IsActive
		{
			get { return _IsActive; }
			set
			{
				if (value != _IsActive)
				{
					_IsActive = value;
					NotifyPropertyChanged("IsActive");
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