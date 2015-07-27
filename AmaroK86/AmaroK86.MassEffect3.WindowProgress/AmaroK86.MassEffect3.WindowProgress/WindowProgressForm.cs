/*  Copyright (C) 2013 AmaroK86 (marcidm 'at' hotmail 'dot' com)
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.

 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Forms;

namespace AmaroK86.MassEffect3
{
    public partial class WindowProgressForm : Form
    {
        public delegate void DelegateFunc(WindowProgressForm dbProg, object args);

        DelegateFunc func;
        object args;

        public WindowProgressForm(DelegateFunc newFunc, object args)
        {
            InitializeComponent();

            func = newFunc;
            this.args = args;
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DelegateFunc newFunc = (DelegateFunc)e.Argument;
            newFunc(this, args);
        }

        private void WindowProgressForm_Load(object sender, EventArgs e)
        {
            backgroundWorker.RunWorkerAsync(func);
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
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
