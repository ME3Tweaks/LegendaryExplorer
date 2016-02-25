using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("LevelSaveRecord")]
	public class Level : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("LevelName")]
		private string _Name;

		[OriginalName("bShouldBeLoaded")]
		private bool _ShouldBeLoaded;

		[OriginalName("bShouldBeVisible")]
		private bool _ShouldBeVisible;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Name);
			stream.Serialize(ref _ShouldBeLoaded);
			stream.Serialize(ref _ShouldBeVisible);
		}

		#region Properties

		[LocalizedDisplayName("Name", typeof (Localization.Level))]
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

		[LocalizedDisplayName("ShouldBeLoaded", typeof (Localization.Level))]
		public bool ShouldBeLoaded
		{
			get { return _ShouldBeLoaded; }
			set
			{
				if (value != _ShouldBeLoaded)
				{
					_ShouldBeLoaded = value;
					NotifyPropertyChanged("ShouldBeLoaded");
				}
			}
		}

		[LocalizedDisplayName("ShouldBeVisible", typeof (Localization.Level))]
		public bool ShouldBeVisible
		{
			get { return _ShouldBeVisible; }
			set
			{
				if (value != _ShouldBeVisible)
				{
					_ShouldBeVisible = value;
					NotifyPropertyChanged("ShouldBeVisible");
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