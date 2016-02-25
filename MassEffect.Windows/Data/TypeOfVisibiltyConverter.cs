using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MassEffect.Windows.Data
{
	[ValueConversion(typeof(Type), typeof(Visibility))]
	public class TypeOfVisibiltyConverter : IValueConverter
	{
		public TypeOfVisibiltyConverter()
		{ }

		public TypeOfVisibiltyConverter(Type targetType)
		{
			TargetType = targetType;
		}

		[ConstructorArgument("targetType")]
		public Type TargetType { get; set; }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return null;
			}

			return value.GetType() == TargetType ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return false;
		}
	}
}
