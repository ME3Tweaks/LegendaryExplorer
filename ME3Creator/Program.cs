using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace ME3Creator
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Process[] pname = Process.GetProcessesByName("ME3Creator");
            if (pname.Length > 1)
            {
                MessageBox.Show("Only one instance of ME3Creator can be run at a time, please close the other first!");
                return;
            }
            Application.Run(new Form1());
        }
    }
}
