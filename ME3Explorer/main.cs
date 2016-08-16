using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Shell;

namespace ME3Explorer
{
    public partial class App : ISingleInstanceApp
    {
        const string Unique = "{3BF98E29-9166-43E7-B24C-AA5C57B73BA6}";

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            int exitCode = 0;
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique, out exitCode))
            {
                SplashScreen splashScreen = new SplashScreen("resources/toolset_splash.png");
                if (Environment.GetCommandLineArgs().Length == 1)
                {
                    splashScreen.Show(false); 
                }
                App app = new App();
                app.InitializeComponent();
                splashScreen.Close(TimeSpan.FromMilliseconds(1));
                app.Run();

                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
            else
            {
                Environment.Exit(exitCode);
            }
        }

        //
        public int SignalExternalCommandLineArgs(IList<string> args)
        {
            int exitCode = 0;
            if (!HandleCommandLineArgs(args, out exitCode))
            {
                //if not called with command line arguments, bring window to the fore
                MainWindow.RestoreAndBringToFront();
            }
            return exitCode;
        }
    }
}