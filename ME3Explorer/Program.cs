using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Helper3DS.loc = Path.GetDirectoryName(Application.ExecutablePath);
            Application.Run(new Form1());
        }
    }
}
