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
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string name);

        const string Unique = "{3BF98E29-9166-43E7-B24C-AA5C57B73BA6}";

        static SplashScreen splashScreen;

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            UnblockLibFiles();
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique, out int exitCode))
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                splashScreen = new SplashScreen("resources/toolset_splash.png");
                if (Environment.GetCommandLineArgs().Length == 1)
                {
                    splashScreen.Show(false);
                }
                SetDllDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lib"));
                CleanupTempFiles();
                App app = new App();
                app.InitializeComponent();
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

        private static void CleanupTempFiles()
        {
            //Cleanup orphaned temp sounds from soundplorer
            DirectoryInfo tempDirectoryInfo = new DirectoryInfo(System.IO.Path.GetTempPath());
            FileInfo[] Files = tempDirectoryInfo.GetFiles("ME3EXP_SOUND_*");
            foreach (FileInfo file in Files)
            {
                try
                {
                    File.Delete(file.FullName);
                }
                catch (Exception)
                {
                    //ignore error
                }
            }
        }


        /// <summary>
        /// Removes ADS streams from files in the lib folder. This prevents startup crash caused by inability for dlls to load from "the internet" if extracted via windows explorer.
        /// </summary>
        private static void UnblockLibFiles()
        {
            var probingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib");
            var files = Directory.GetFiles(probingPath);
            foreach (string file in files)
            {
                DeleteFile(file + ":Zone.Identifier");
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