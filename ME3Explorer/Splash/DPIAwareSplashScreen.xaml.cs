namespace ME3Explorer.Splash
{
    /// <summary>
    /// Interaction logic for DPIAwareSplashScreen.xaml
    /// </summary>
    public partial class DPIAwareSplashScreen : NotifyPropertyChangedWindowBase
    {
        private string _splashScreenText;

        public string SplashScreenText
        {
            get => _splashScreenText;
            set => SetProperty(ref _splashScreenText, value);
        }
        public DPIAwareSplashScreen()
        {
            InitializeComponent();
        }
    }
}
