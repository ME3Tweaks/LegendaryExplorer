using System;
using System.Windows;

namespace ME3Explorer
{
    public partial class App
    {
        /// <summary>
            /// Application Entry Point.
            /// </summary>
        [STAThread]
        public static void Main()
        {
            SplashScreen splashScreen = new SplashScreen("resources/toolset_splash.png");
            splashScreen.Show(false);
            App app = new App();
            app.InitializeComponent();
            splashScreen.Close(TimeSpan.FromMilliseconds(1));
            app.Run();
        }
    }
}