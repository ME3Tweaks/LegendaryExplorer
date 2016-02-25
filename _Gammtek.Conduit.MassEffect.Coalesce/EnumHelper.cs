using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class EnumHelper : DependencyObject
	{
		public static readonly DependencyProperty EnumProperty =
			DependencyProperty.RegisterAttached("Enum", typeof (Type), typeof (EnumHelper), new PropertyMetadata(null, OnEnumChanged));

		public static readonly DependencyProperty MoreDetailsProperty =
			DependencyProperty.RegisterAttached("MoreDetails", typeof (bool), typeof (EnumHelper), new PropertyMetadata(false, OnMoreDetailsChanged));

		public static Type GetEnum(DependencyObject obj)
		{
			return (Type) obj.GetValue(EnumProperty);
		}

		public static void SetEnum(DependencyObject obj, string value)
		{
			obj.SetValue(EnumProperty, value);
		}

		// Using a DependencyProperty as the backing store for Enum.  This enables animation, styling, binding, etc...

		private static void OnEnumChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			var control = sender as ItemsControl;

			if (control == null)
			{
				return;
			}

			if (e.NewValue == null)
			{
				return;
			}

			var type = e.NewValue as Type;

			if (type == null)
			{
				return;
			}

			var _enum = Enum.GetValues(type);

			control.ItemsSource = _enum;
		}

		public static bool GetMoreDetails(DependencyObject obj)
		{
			return (bool) obj.GetValue(MoreDetailsProperty);
		}

		public static void SetMoreDetails(DependencyObject obj, bool value)
		{
			obj.SetValue(MoreDetailsProperty, value);
		}

		// Using a DependencyProperty as the backing store for MoreDetails.  This enables animation, styling, binding, etc...

		private static void OnMoreDetailsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			var control = sender as FrameworkElement;

			if (control == null)
			{
				return;
			}

			var enumobject = control.DataContext;
			var fieldInfo = enumobject.GetType().GetField(enumobject.ToString());

			var array = fieldInfo.GetCustomAttributes(false);

			if (array.Length == 0)
			{
				var block = control as TextBlock;

				if (block != null)
				{
					block.Text = enumobject.ToString();
				}
				else
				{
					var contentControl = control as ContentControl;

					if (contentControl != null)
					{
						contentControl.Content = enumobject;
					}
				}

				return;
			}

			foreach (var o in array)
			{
				var attribute = o as DescriptionAttribute;

				if (attribute != null)
				{
					control.ToolTip = attribute.Description;
				}
				else
				{
					var displayAttribute = o as DisplayAttribute;

					if (displayAttribute == null)
					{
						continue;
					}

					var block = control as TextBlock;

					if (block != null)
					{
						block.Text = displayAttribute.Name;
					}
					else
					{
						var contentControl = control as ContentControl;

						if (contentControl != null)
						{
							contentControl.Content = displayAttribute.Name;
						}
					}
				}
			}
		}
	}
}
