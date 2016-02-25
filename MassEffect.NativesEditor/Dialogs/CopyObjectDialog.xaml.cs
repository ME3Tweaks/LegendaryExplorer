using System;
using System.Windows;

namespace MassEffect.NativesEditor.Dialogs
{
	/// <summary>
	///     Interaction logic for CopyObjectDialog.xaml
	/// </summary>
	public partial class CopyObjectDialog
	{
		private const string DefaultContentText = "Copy <object: type> #<object: id>";
		private const string DefaultHeaderText = "Specify id of the copy.";
		private const string DefaultTitle = "Copy Object";

		public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register("ContentText", typeof (string),
			typeof (CopyObjectDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register("HeaderText", typeof (string),
			typeof (CopyObjectDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty ObjectIdProperty = DependencyProperty.Register("ObjectId", typeof (int),
			typeof (CopyObjectDialog), new PropertyMetadata(default(int)));

		public CopyObjectDialog()
			: this(DefaultContentText, DefaultHeaderText, DefaultTitle) {}

		public CopyObjectDialog(string contentText = null, string headerText = null, string dialogTitle = null)
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

		private void CopyObjectDialog_OnContentRendered(object sender, EventArgs e)
		{
			IdSpinner.Focus();
		}

		private void OkButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
