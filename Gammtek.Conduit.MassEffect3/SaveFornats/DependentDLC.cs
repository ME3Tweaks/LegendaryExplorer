using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("DependentDLCRecord")]
	public class DependentDLC : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("CanonicalName")]
		private string _CanonicalName;

		[OriginalName("ModuleID")]
		private int _ModuleId;

		[OriginalName("Name")]
		private string _Name;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _ModuleId);
			stream.Serialize(ref _Name);
			stream.Serialize(ref _CanonicalName, s => s.Version < 50, () => null);
		}

		public override string ToString()
		{
			return String.Format("{1} ({0})",
				_ModuleId,
				_Name);
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("ModuleId", typeof (Localization.DependentDLC))]
		public int ModuleId
		{
			get { return _ModuleId; }
			set
			{
				if (value != _ModuleId)
				{
					_ModuleId = value;
					NotifyPropertyChanged("ModuleId");
				}
			}
		}

		[LocalizedDisplayName("Name", typeof (Localization.DependentDLC))]
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

		[LocalizedDisplayName("CanonicalName", typeof (Localization.DependentDLC))]
		public string CanonicalName
		{
			get { return _CanonicalName; }
			set
			{
				if (value != _CanonicalName)
				{
					_CanonicalName = value;
					NotifyPropertyChanged("CanonicalName");
				}
			}
		}

		#endregion
	}
}