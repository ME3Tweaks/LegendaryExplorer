using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace MassEffect3.TocEditor
{
	public partial class WindowProgressForm : Form
	{
		public delegate void DelegateFunc(WindowProgressForm dbProg, object args);

		private readonly object _args;
		private readonly DelegateFunc _func;

		public WindowProgressForm(DelegateFunc newFunc, object args)
		{
			InitializeComponent();

			_func = newFunc;
			_args = args;
		}

		private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			var newFunc = (DelegateFunc) e.Argument;
			newFunc(this, _args);
		}

		private void WindowProgressForm_Load(object sender, EventArgs e)
		{
			backgroundWorker.RunWorkerAsync(_func);
		}

		private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			Close();
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			// If the background thread is running then clicking this
			// button causes a cancel, otherwise clicking this button
			// launches the background thread.
			if (backgroundWorker.IsBusy)
			{
				// Notify the worker thread that a cancel has been requested.
				// The cancel will not actually happen until the thread in the
				// DoWork checks the bwAsync.CancellationPending flag, for this
				// reason we set the label to "Cancelling...", because we haven't
				// actually cancelled yet.
				backgroundWorker.CancelAsync();
			}
		}
	}
}