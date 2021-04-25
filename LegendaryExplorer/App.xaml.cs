using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Dialogs.Splash;
using LegendaryExplorer.Startup;
using SingleInstanceCore;

namespace LegendaryExplorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstance
    {
        #region Application-wide variables
        public static Visibility IsDebugVisibility => IsDebug ? Visibility.Visible : Visibility.Collapsed;

#if DEBUG
        public static bool IsDebug => true;
#else
        public static bool IsDebug => false;
#endif
        #endregion


        // PLEASE KEEP GENERAL CODE NOT IN APP CLASS
        // (Only required overrides should be here)
        // BOOT CODE SHOULD BE IN APPBOOT.CS

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Handle command line arguments that may be used for updater (if implemented) here, which must not be limited by the single instance
            // TODO: UPDATER (using JPATCH?)

            // Boot single instance
            if (SingleInstance<App>.InitializeAsFirstInstance("LegendaryExplorer"))
            {
                // Application bootup is handled in AppBoot class
                AppBoot.Startup(this);
            }
            else
            {
                // Arguments will be passed through on OnInstanceInvoked().
                Shutdown(0);
            }

        }

        /// <summary>
        /// Called when an unhandled exception occurs. This method can only be invoked after startup has completed. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">Exception to process</param>
        internal void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var eh = new ExceptionHandlerDialog(e.Exception);
            Window wpfActiveWindow = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
            eh.Owner = wpfActiveWindow;
            eh.ShowDialog();
            e.Handled = eh.Handled;
        }

        public void OnInstanceInvoked(string[] args)
        {
            AppBoot.HandleDuplicateInstanceArgs(args);
        }
    }
}
