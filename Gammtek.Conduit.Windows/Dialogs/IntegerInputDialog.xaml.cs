using System;
using System.Windows;

namespace Gammtek.Conduit.Windows.Dialogs
{
	/// <summary>
	///     Interaction logic for IntegerInputDialog.xaml
	/// </summary>
	public partial class IntegerInputDialog
	{
		public static readonly DependencyProperty MessageTextProperty = DependencyProperty.Register("MessageText", typeof (string),
			typeof(IntegerInputDialog));


		public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof (int),
			typeof (IntegerInputDialog));
		
		public IntegerInputDialog(string messageText = "Input Integer:", string title = "Input", int value = 0)
		{
			InitializeComponent();
			DataContext = this;

			MessageText = messageText;
			Value = value;
			Title = title;
		}

		public string MessageText
		{
			get { return (string) GetValue(MessageTextProperty); }
			set { SetValue(MessageTextProperty, value); }
		}

		public int Value
		{
			get { return (int) GetValue(ValueProperty); }
			set { SetValue(ValueProperty, value); }
		}

		private void CancelButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		private void IntegerInputDialog_OnContentRendered(object sender, EventArgs e)
		{
			InputBox.Focus();
		}

		private void OkButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
