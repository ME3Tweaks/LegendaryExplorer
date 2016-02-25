using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("SaveTimeStamp")]
	public class SaveTimeStamp : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("Day")]
		private int _Day;

		[OriginalName("Month")]
		private int _Month;

		[OriginalName("SecondsSinceMidnight")]
		private int _SecondsSinceMidnight;

		[OriginalName("Year")]
		private int _Year;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _SecondsSinceMidnight);
			stream.Serialize(ref _Day);
			stream.Serialize(ref _Month);
			stream.Serialize(ref _Year);
		}

		public override string ToString()
		{
			return string.Format("{0}/{1}/{2} {3}:{4:D2}",
				Day,
				Month,
				Year,
				(int) Math.Round((SecondsSinceMidnight / 60.0) / 60.0),
				(int) Math.Round(SecondsSinceMidnight / 60.0) % 60);
		}

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#region Properties

		[LocalizedDisplayName("SecondsSinceMidnight", typeof (Localization.SaveTimeStamp))]
		public int SecondsSinceMidnight
		{
			get { return _SecondsSinceMidnight; }
			set
			{
				if (value != _SecondsSinceMidnight)
				{
					_SecondsSinceMidnight = value;
					NotifyPropertyChanged("SecondsSinceMidnight");
				}
			}
		}

		[LocalizedDisplayName("Day", typeof (Localization.SaveTimeStamp))]
		public int Day
		{
			get { return _Day; }
			set
			{
				if (value != _Day)
				{
					_Day = value;
					NotifyPropertyChanged("Day");
				}
			}
		}

		[LocalizedDisplayName("Month", typeof (Localization.SaveTimeStamp))]
		public int Month
		{
			get { return _Month; }
			set
			{
				if (value != _Month)
				{
					_Month = value;
					NotifyPropertyChanged("Month");
				}
			}
		}

		[LocalizedDisplayName("Year", typeof (Localization.SaveTimeStamp))]
		public int Year
		{
			get { return _Year; }
			set
			{
				if (value != _Year)
				{
					_Year = value;
					NotifyPropertyChanged("Year");
				}
			}
		}

		#endregion
	}
}