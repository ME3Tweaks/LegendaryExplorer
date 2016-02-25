using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("PowerSaveRecord")]
	public class Power : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("PowerClassName")]
		private string _ClassName;

		[OriginalName("CurrentRank")]
		private float _CurrentRank;

		//[OriginalName("EvolvedChoices[0]")]
		//private int _EvolvedChoice0;

		//[OriginalName("EvolvedChoices[1]")]
		//private int _EvolvedChoice1;

		//[OriginalName("EvolvedChoices[2]")]
		//private int _EvolvedChoice2;

		//[OriginalName("EvolvedChoices[3]")]
		//private int _EvolvedChoice3;

		//[OriginalName("EvolvedChoices[4]")]
		//private int _EvolvedChoice4;

		//[OriginalName("EvolvedChoices[5]")]
		//private int _EvolvedChoice5;

		[OriginalName("PowerName")]
		private string _Name;

		[OriginalName("WheelDisplayIndex")]
		private int _WheelDisplayIndex;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public Power()
		{}

		public Power(string className, string name = "")
		{
			_ClassName = className;
			_Name = name;
		}

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Name);
			stream.Serialize(ref _CurrentRank);
			//stream.Serialize(ref _EvolvedChoice0, s => s.Version < 30, () => 0);
			//stream.Serialize(ref _EvolvedChoice1, s => s.Version < 30, () => 0);
			//stream.Serialize(ref _EvolvedChoice2, s => s.Version < 31, () => 0);
			//stream.Serialize(ref _EvolvedChoice3, s => s.Version < 31, () => 0);
			//stream.Serialize(ref _EvolvedChoice4, s => s.Version < 31, () => 0);
			//stream.Serialize(ref _EvolvedChoice5, s => s.Version < 31, () => 0);
			stream.Serialize(ref _ClassName);
			stream.Serialize(ref _WheelDisplayIndex);
		}

		public override string ToString()
		{
			return Name;
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("Name", typeof (Localization.Power))]
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

		[LocalizedDisplayName("CurrentRank", typeof (Localization.Power))]
		public float CurrentRank
		{
			get { return _CurrentRank; }
			set
			{
				if (Equals(value, _CurrentRank) == false)
				{
					_CurrentRank = value;
					NotifyPropertyChanged("CurrentRank");
				}
			}
		}

		/*[LocalizedDisplayName("EvolvedChoice0", typeof (Localization.Power))]
		public int EvolvedChoice0
		{
			get { return _EvolvedChoice0; }
			set
			{
				if (value != _EvolvedChoice0)
				{
					_EvolvedChoice0 = value;
					NotifyPropertyChanged("EvolvedChoice0");
				}
			}
		}*/

		/*[LocalizedDisplayName("EvolvedChoice1", typeof (Localization.Power))]
		public int EvolvedChoice1
		{
			get { return _EvolvedChoice1; }
			set
			{
				if (value != _EvolvedChoice1)
				{
					_EvolvedChoice1 = value;
					NotifyPropertyChanged("EvolvedChoice1");
				}
			}
		}*/

		/*[LocalizedDisplayName("EvolvedChoice2", typeof (Localization.Power))]
		public int EvolvedChoice2
		{
			get { return _EvolvedChoice2; }
			set
			{
				if (value != _EvolvedChoice2)
				{
					_EvolvedChoice2 = value;
					NotifyPropertyChanged("EvolvedChoice2");
				}
			}
		}*/

		/*[LocalizedDisplayName("EvolvedChoice3", typeof (Localization.Power))]
		public int EvolvedChoice3
		{
			get { return _EvolvedChoice3; }
			set
			{
				if (value != _EvolvedChoice3)
				{
					_EvolvedChoice3 = value;
					NotifyPropertyChanged("EvolvedChoice3");
				}
			}
		}*/

		/*[LocalizedDisplayName("EvolvedChoice4", typeof (Localization.Power))]
		public int EvolvedChoice4
		{
			get { return _EvolvedChoice4; }
			set
			{
				if (value != _EvolvedChoice4)
				{
					_EvolvedChoice4 = value;
					NotifyPropertyChanged("EvolvedChoice4");
				}
			}
		}*/

		/*[LocalizedDisplayName("EvolvedChoice5", typeof (Localization.Power))]
		public int EvolvedChoice5
		{
			get { return _EvolvedChoice5; }
			set
			{
				if (value != _EvolvedChoice5)
				{
					_EvolvedChoice5 = value;
					NotifyPropertyChanged("EvolvedChoice5");
				}
			}
		}*/

		[LocalizedDisplayName("ClassName", typeof (Localization.Power))]
		public string ClassName
		{
			get { return _ClassName; }
			set
			{
				if (value != _ClassName)
				{
					_ClassName = value;
					NotifyPropertyChanged("ClassName");
				}
			}
		}

		[LocalizedDisplayName("WheelDisplayIndex", typeof (Localization.Power))]
		public int WheelDisplayIndex
		{
			get { return _WheelDisplayIndex; }
			set
			{
				if (value != _WheelDisplayIndex)
				{
					_WheelDisplayIndex = value;
					NotifyPropertyChanged("WheelDisplayIndex");
				}
			}
		}

		#endregion
	}
}