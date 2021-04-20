using System;
using System.Windows;

namespace ME3Explorer.PlotEditor.Dialogs
{
	/// <summary>
	///     Interaction logic for NewObjectDialog.xaml
	/// </summary>
	public partial class NewObjectDialog
	{
		private const string DefaultContentText = "New <object: type>";
		private const string DefaultHeaderText = "Specify the id.";
		private const string DefaultTitle = "New Object";

		public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register("ContentText", typeof (string),
			typeof(NewObjectDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register("HeaderText", typeof (string),
			typeof(NewObjectDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty ObjectIdProperty = DependencyProperty.Register("ObjectId", typeof (int),
			typeof(NewObjectDialog), new PropertyMetadata(default(int)));

		public NewObjectDialog()
			: this(DefaultContentText, DefaultHeaderText, DefaultTitle) {}

		public NewObjectDialog(string contentText = null, string headerText = null, string dialogTitle = null)
		{
			InitializeComponent();

			ContentText = contentText ?? DefaultContentText;
			HeaderText = headerText ?? DefaultHeaderText;
			Title = dialogTitle ?? DefaultTitle;
		}

		public string ContentText
		{
			get { return (string) GetValue(ContentTextProperty); }
			set { SetValue(ContentTextProperty, value); }
		}

		public string HeaderText
		{
			get { return (string) GetValue(HeaderTextProperty); }
			set { SetValue(HeaderTextProperty, value); }
		}

		public int ObjectId
		{
			get { return (int) GetValue(ObjectIdProperty); }
			set { SetValue(ObjectIdProperty, value); }
		}

		private void CancelButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		private void NewObjectDialog_OnContentRendered(object sender, EventArgs e)
		{
			IdSpinner.Focus();
		}

		private void OkButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
