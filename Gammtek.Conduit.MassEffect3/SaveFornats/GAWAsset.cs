using System.ComponentModel;
using System.Globalization;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("GAWAssetSaveInfo")]
	public class GAWAsset : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("Id")]
		private int _Id;

		[OriginalName("Strength")]
		private int _Strength;

		#endregion

		// for CollectionEditor
		[Browsable(false)]
		public string Name
		{
			get { return _Id.ToString(CultureInfo.InvariantCulture); }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Id);
			stream.Serialize(ref _Strength);
		}

		public override string ToString()
		{
			return Name ?? "(null)";
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("Id", typeof (Localization.GAWAsset))]
		public int Id
		{
			get { return _Id; }
			set
			{
				if (value != _Id)
				{
					_Id = value;
					NotifyPropertyChanged("Id");
				}
			}
		}

		[LocalizedDisplayName("Strength", typeof (Localization.GAWAsset))]
		public int Strength
		{
			get { return _Strength; }
			set
			{
				if (value != _Strength)
				{
					_Strength = value;
					NotifyPropertyChanged("Strength");
				}
			}
		}

		#endregion
	}
}