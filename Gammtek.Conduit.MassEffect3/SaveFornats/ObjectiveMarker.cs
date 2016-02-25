using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("ObjectiveMarkerSaveRecord")]
	public class ObjectiveMarker : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("BoneToAttachTo")]
		private string _BoneToAttachTo;

		[OriginalName("MarkerIconType")]
		private ObjectiveMarkerIconType _MarkerIconType;

		[OriginalName("MarkerLabel")]
		private int _MarkerLabel;

		[OriginalName("MarkerOffset")]
		private Vector _MarkerOffset;

		[OriginalName("MarkerOwnerPath")]
		private string _MarkerOwnerPath;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _MarkerOwnerPath);
			stream.Serialize(ref _MarkerOffset);
			stream.Serialize(ref _MarkerLabel);
			stream.Serialize(ref _BoneToAttachTo);
			stream.SerializeEnum(ref _MarkerIconType);
		}

		#region Properties

		[LocalizedDisplayName("MarkerOwnerPath", typeof (Localization.ObjectiveMarker))]
		public string MarkerOwnerPath
		{
			get { return _MarkerOwnerPath; }
			set
			{
				if (value != _MarkerOwnerPath)
				{
					_MarkerOwnerPath = value;
					NotifyPropertyChanged("MarkerOwnerPath");
				}
			}
		}

		[LocalizedDisplayName("MarkerOffset", typeof (Localization.ObjectiveMarker))]
		public Vector MarkerOffset
		{
			get { return _MarkerOffset; }
			set
			{
				if (value != _MarkerOffset)
				{
					_MarkerOffset = value;
					NotifyPropertyChanged("MarkerOffset");
				}
			}
		}

		[LocalizedDisplayName("MarkerLabel", typeof (Localization.ObjectiveMarker))]
		public int MarkerLabel
		{
			get { return _MarkerLabel; }
			set
			{
				if (value != _MarkerLabel)
				{
					_MarkerLabel = value;
					NotifyPropertyChanged("MarkerLabel");
				}
			}
		}

		[LocalizedDisplayName("BoneToAttachTo", typeof (Localization.ObjectiveMarker))]
		public string BoneToAttachTo
		{
			get { return _BoneToAttachTo; }
			set
			{
				if (value != _BoneToAttachTo)
				{
					_BoneToAttachTo = value;
					NotifyPropertyChanged("BoneToAttachTo");
				}
			}
		}

		[LocalizedDisplayName("MarkerIconType", typeof (Localization.ObjectiveMarker))]
		public ObjectiveMarkerIconType MarkerIconType
		{
			get { return _MarkerIconType; }
			set
			{
				if (value != _MarkerIconType)
				{
					_MarkerIconType = value;
					NotifyPropertyChanged("MarkerIconType");
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