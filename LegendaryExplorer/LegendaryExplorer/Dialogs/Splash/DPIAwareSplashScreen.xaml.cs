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
            MessageBox.Show(
                "WARNING: Mods released before the toolset is publicly available for download (not compiled) will be blacklisted by modding tools in the scene.\nDo NOT release package mods until the tools for modding are released for public use.", "WARNING: DO NOT RELEASE MODS", MessageBoxButton.OK, MessageBoxImage.Warning);
            InitializeComponent();
        }
    }
}
