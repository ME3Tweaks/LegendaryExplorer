using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("WeaponModSaveRecord")]
	public class WeaponMod : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("WeaponModClassNames")]
		private string _WeaponClassName;

		[OriginalName("WeaponClassName")]
		private List<string> _WeaponModClassNames = new List<string>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _WeaponClassName);
			stream.Serialize(ref _WeaponModClassNames);
		}

		#region Properties

		[LocalizedDisplayName("WeaponClassName", typeof (Localization.WeaponMod))]
		public string WeaponClassName
		{
			get { return _WeaponClassName; }
			set
			{
				if (value != _WeaponClassName)
				{
					_WeaponClassName = value;
					NotifyPropertyChanged("WeaponClassName");
				}
			}
		}

		[Editor(
			"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
			, typeof (UITypeEditor))]
		[LocalizedDisplayName("WeaponModClassNames", typeof (Localization.WeaponMod))]
		public List<string> WeaponModClassNames
		{
			get { return _WeaponModClassNames; }
			set
			{
				if (value != _WeaponModClassNames)
				{
					_WeaponModClassNames = value;
					NotifyPropertyChanged("WeaponModClassNames");
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