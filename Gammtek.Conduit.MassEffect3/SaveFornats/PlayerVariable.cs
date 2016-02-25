using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("PlayerVariableSaveRecord")]
	public class PlayerVariable : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("VariableName")]
		private string _Name;

		[OriginalName("VariableValue")]
		private int _Value;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Name);
			stream.Serialize(ref _Value);
		}

		#region Properties

		[LocalizedDisplayName("Name", typeof (Localization.PlayerVariable))]
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

		[LocalizedDisplayName("Value", typeof (Localization.PlayerVariable))]
		public int Value
		{
			get { return _Value; }
			set
			{
				if (value != _Value)
				{
					_Value = value;
					NotifyPropertyChanged("Value");
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