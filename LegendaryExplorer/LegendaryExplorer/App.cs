using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Startup;

namespace LegendaryExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [STAThread]
        public static void Main()
        {
            DPIAwareSplashScreen.Show();

            var app = new App();
            app.Startup += app.Application_Startup;
            app.Run();
        }

        #region Application-wide variables
        public static Visibility IsDebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif

        public static int CoreCount;

        public static readonly DateTime BuildDateTime = new(CompileTime, DateTimeKind.Utc);

        public static App Instance;

        #endregion

        // PLEASE KEEP GENERAL CODE NOT IN APP CLASS
        // (Only required overrides should be here)
        // BOOT CODE SHOULD BE IN APPBOOT.CS
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Handle command line arguments that may be used for updater (if implemented) here, which must not be limited by the single instance
            // TODO: UPDATER (using JPATCH?)

            // Boot single instance
            var singleInstance = new SingleInstance.SingleInstance("LegendaryExplorer");
            Current.Exit += (_, _) => singleInstance.Dispose();

            if(singleInstance.IsFirstInstance)
            {
                Instance = this;
                // Application bootup is handled in AppBoot class
                singleInstance.ArgumentsReceived.Subscribe(OnInstanceInvoked);
                singleInstance.ListenForArgumentsFromSuccessiveInstances();
                AppBoot.Startup(this);
            }
            else
            {
                // Arguments will be passed through on OnInstanceInvoked().
                singleInstance.PassArgumentsToFirstInstance(e.Args);
                Shutdown(0);
            }
        }

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        internal static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var eh = new ExceptionHandlerDialog(e.Exception);
            Window wpfActiveWindow = Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            eh.Owner = wpfActiveWindow;
            try
            {
                eh.ShowDialog();
            }
            catch
            {
                // Retry without owner - if owner window is in error state it will crash this dialog and the 
                // whole app will die instead
                eh = new ExceptionHandlerDialog(e.Exception);
                eh.ShowDialog();
            }
            e.Handled = eh.Handled;
        }

        private static void OnInstanceInvoked(string[] args)
        {
            AppBoot.HandleDuplicateInstanceArgs(args);
        }
    }
}
