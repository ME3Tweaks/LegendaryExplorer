using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace MassEffect.Windows.Data
{
	[ValueConversion(typeof (Type), typeof (bool))]
	public class TypeOfConverter : IValueConverter
	{
		public TypeOfConverter() {}

		public TypeOfConverter(Type targetType)
		{
			TargetType = targetType;
		}

		[ConstructorArgument("targetType")]
		public Type TargetType { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return false;
			}

			return value.GetType() == TargetType;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return false;
		}
	}
}
