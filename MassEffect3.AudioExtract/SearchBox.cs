using System;
using System.Windows.Forms;

namespace MassEffect3.AudioExtract
{
	public partial class SearchBox : Form
	{
		public SearchBox()
		{
			InitializeComponent();
		}

		public string InputText
		{
			get { return textBox.Text; }
			set { textBox.Text = value; }
		}

		private void OnKeyPress(object sender, KeyPressEventArgs e)
		{
			if (e.KeyChar == '\r' || e.KeyChar == '\n')
			{
				DialogResult = DialogResult.OK;
			}
			else if (e.KeyChar == '\x1B')
			{
				DialogResult = DialogResult.Cancel;
			}
		}

		private void OnShown(object sender, EventArgs e)
		{
			textBox.Focus();
		}
	}
}