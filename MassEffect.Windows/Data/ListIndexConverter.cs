using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using MassEffect.Windows.Extensions;

namespace MassEffect.Windows.Data
{
	public class ListIndexConverter : IValueConverter
	{
		#region Implementation of IValueConverter

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var item = value as ListBoxItem;

			if (item != null)
			{
				var lb = item.FindVisualAncestor<ListBox>();

				if (lb != null)
				{
					return lb.Items.IndexOf(item.Content);
				}

				/*var lb = FindVisualAncestor<ListBox>(item);
				if (lb != null)
				{
					var index = lb.Items.IndexOf(item.Content);
					return index;
				}*/
			}

			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
