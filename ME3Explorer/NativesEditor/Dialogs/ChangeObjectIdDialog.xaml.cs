using System;
using System.Windows;

namespace MassEffect.NativesEditor.Dialogs
{
	/// <summary>
	///     Interaction logic for ChangeObjectIdDialog.xaml
	/// </summary>
	public partial class ChangeObjectIdDialog
	{
		private const string DefaultContentText = "Change id of <object: type> #<object: id>";
		private const string DefaultHeaderText = "Specify the new id.";
		private const string DefaultTitle = "Change Object Id";

		public static readonly DependencyProperty ContentTextProperty = DependencyProperty.Register("ContentText", typeof (string),
			typeof (ChangeObjectIdDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register("HeaderText", typeof (string),
			typeof (ChangeObjectIdDialog), new PropertyMetadata(default(string)));

		public static readonly DependencyProperty ObjectIdProperty = DependencyProperty.Register("ObjectId", typeof (int),
			typeof (ChangeObjectIdDialog), new PropertyMetadata(default(int)));

		public ChangeObjectIdDialog()
			: this(DefaultContentText, DefaultHeaderText, DefaultTitle) {}

		public ChangeObjectIdDialog(string contentText = null, string headerText = null, string dialogTitle = null)
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

		private void ChangeObjectIdDialog_OnContentRendered(object sender, EventArgs e)
		{
			IdSpinner.Focus();
		}

		private void OkButton_OnClick(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}
