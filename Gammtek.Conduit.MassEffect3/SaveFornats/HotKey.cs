using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("HotKeySaveRecord")]
	public class HotKey : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("PawnName")]
		private string _PawnName;

		[OriginalName("PowerID")]
		private int _PowerId;

		[OriginalName("PowerName")]
		private string _PowerName;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _PawnName);
			stream.Serialize(ref _PowerName, s => s.Version < 30, () => null);
			stream.Serialize(ref _PowerId, s => s.Version >= 30, () => 0);
		}

		#region Properties

		[LocalizedDisplayName("PawnName", typeof (Localization.HotKey))]
		public string PawnName
		{
			get { return _PawnName; }
			set
			{
				if (value != _PawnName)
				{
					_PawnName = value;
					NotifyPropertyChanged("PawnName");
				}
			}
		}

		[Browsable(false)]
		[LocalizedDisplayName("PowerId", typeof (Localization.HotKey))]
		public int PowerId
		{
			get { return _PowerId; }
			set
			{
				if (value != _PowerId)
				{
					_PowerId = value;
					NotifyPropertyChanged("PowerId");
				}
			}
		}

		[LocalizedDisplayName("PowerName", typeof (Localization.HotKey))]
		public string PowerName
		{
			get { return _PowerName; }
			set
			{
				if (value != _PowerName)
				{
					_PowerName = value;
					NotifyPropertyChanged("PowerName");
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