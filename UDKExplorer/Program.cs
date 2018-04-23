using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace UDKExplorer
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }

        /// <summary>
        /// Resolves assemblies in lib.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var probingPath = AppDomain.CurrentDomain.BaseDirectory + @"lib";
            var assyName = new AssemblyName(args.Name);

            var newPath = Path.Combine(probingPath, assyName.Name);
            if (!newPath.EndsWith(".dll"))
            {
                newPath = newPath + ".dll";
            }
            if (File.Exists(newPath))
            {
                var assy = Assembly.LoadFile(newPath);
                return assy;
            }
            return null;
        }
    }
}
