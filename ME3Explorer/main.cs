using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Shell;

namespace ME3Explorer
{
    public partial class App : ISingleInstanceApp
    {
        const string Unique = "{3BF98E29-9166-43E7-B24C-AA5C57B73BA6}";
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique, out int exitCode))
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                SetDllDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lib")); //required for lzo library dllimports
                SplashScreen splashScreen = new SplashScreen("resources/toolset_splash.png");
                if (Environment.GetCommandLineArgs().Length == 1)
                {
                    splashScreen.Show(false);
                }
                App app = new App();
                app.InitializeComponent();
                splashScreen.Close(TimeSpan.FromMilliseconds(1));
                //will throw exception on some tools when opening over remote desktop.
                app.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
            else
            {
                Environment.Exit(exitCode);
            }
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
            var searchPath = newPath;
            if (!searchPath.EndsWith(".dll"))
            {
                searchPath = searchPath + ".dll";
            }
            if (File.Exists(searchPath))
            {
                var assy = Assembly.LoadFile(searchPath);
                return assy;
            }
            //look for exe assembly (CSharpImageLibrary)
            searchPath = newPath;
            if (!searchPath.EndsWith(".exe"))
            {
                searchPath = searchPath + ".exe";
            }
            if (File.Exists(searchPath))
            {
                var assy = Assembly.LoadFile(searchPath);
                return assy;
            }
            return null;
        }

        //
        public int SignalExternalCommandLineArgs(string[] args)
        {
            int taskListResponse = HandleCommandLineJumplistCall(args, out int exitCode);
            if (taskListResponse == 1)
            {
                //just a new instance
                MainWindow.RestoreAndBringToFront();
            }
            return 0;
        }
    }
}