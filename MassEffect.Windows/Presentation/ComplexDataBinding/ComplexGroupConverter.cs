using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	public class ComplexGroupConverter : IMultiValueConverter
	{
		#region IMultiValueConverter Members

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			var results = new List<object>();
			string[] parameters;

			var s = parameter as string;

			if (s != null)
			{
				parameters = s.Split(',');

				for (var i = 0; i < parameters.Length; i++)
				{
					parameters[i] = parameters[i].Trim();
				}
			}
			else
			{
				parameters = new string[0];
			}

			var index = 0;

			foreach (var value in values)
			{
				if (value is IEnumerable)
				{
					results.Add(index < parameters.Length ? new BindingGroup(value as IEnumerable, parameters[index]) : value);
				}
				else
				{
					results.Add(value);
				}
				index++;
			}

			return results;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			if (!(value is List<object>))
			{
				throw new NotSupportedException();
			}

			var objects = value as List<object>;

			return objects.ToArray();
		}

		#endregion
	}
}
