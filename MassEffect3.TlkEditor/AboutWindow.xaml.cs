using System.Windows;

namespace MassEffect3.TlkEditor
{
	/// <summary>
	///     Interaction logic for AboutWindow.xaml
	/// </summary>
	public partial class AboutWindow : Window
	{
		public AboutWindow()
		{
			InitializeComponent();
		}

		public string ApplicationVersion
		{
			get { return "v. " + App.GetVersion(); }
		}

		private void button1_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}