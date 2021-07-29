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
        public DPIAwareSplashScreen() : base("DPIAwareSplashScreen", false)
        {
            InitializeComponent();
        }
    }
}
