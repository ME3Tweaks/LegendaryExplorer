using System.Windows;
using LegendaryExplorer.SharedUI.Bases;

namespace LegendaryExplorer.Dialogs.Splash
{
    /// <summary>
    /// Interaction logic for DPIAwareSplashScreen.xaml
    /// </summary>
    public partial class DPIAwareSplashScreen : TrackingNotifyPropertyChangedWindowBase
    {
        private string _splashScreenText;

        public string SplashScreenText
        {
            get => _splashScreenText;
            set => SetProperty(ref _splashScreenText, value);
        }

#if NIGHTLY
        public string SplashImagePath => "/Resources/Images/LEX_Splash_Nightly.png";
#else
        public string SplashImagePath => "/Resources/Images/LEX_Splash.png";
#endif
        public DPIAwareSplashScreen() : base("DPIAwareSplashScreen", false)
        {
            InitializeComponent();
        }
    }
}
