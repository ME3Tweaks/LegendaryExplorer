using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("LevelTreasureSaveRecord")]
	public class LevelTreasure : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("nCredits")]
		private int _Credits;

		[OriginalName("Items")]
		private List<string> _Items = new List<string>();

		[OriginalName("LevelName")]
		private string _LevelName;

		[OriginalName("nXP")]
		private int _XP;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _LevelName);
			stream.Serialize(ref _Credits);
			stream.Serialize(ref _XP);
			stream.Serialize(ref _Items);
		}

		#region Properties

		[LocalizedDisplayName("LevelName", typeof (Localization.LevelTreasure))]
		public string LevelName
		{
			get { return _LevelName; }
			set
			{
				if (value != _LevelName)
				{
					_LevelName = value;
					NotifyPropertyChanged("LevelName");
				}
			}
		}

		[LocalizedDisplayName("Credits", typeof (Localization.LevelTreasure))]
		public int Credits
		{
			get { return _Credits; }
			set
			{
				if (value != _Credits)
				{
					_Credits = value;
					NotifyPropertyChanged("Credits");
				}
			}
		}

		[LocalizedDisplayName("XP", typeof (Localization.LevelTreasure))]
		public int XP
		{
			get { return _XP; }
			set
			{
				if (value != _XP)
				{
					_XP = value;
					NotifyPropertyChanged("XP");
				}
			}
		}

		[Editor(
			"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
			, typeof (UITypeEditor))]
		[LocalizedDisplayName("Items", typeof (Localization.LevelTreasure))]
		public List<string> Items
		{
			get { return _Items; }
			set
			{
				if (value != _Items)
				{
					_Items = value;
					NotifyPropertyChanged("Items");
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