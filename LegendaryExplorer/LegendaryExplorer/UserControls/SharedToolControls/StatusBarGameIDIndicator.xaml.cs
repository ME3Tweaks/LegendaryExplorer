using System.Windows;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.UserControls.SharedToolControls
{
    /// <summary>
    /// Interaction logic for StatusBarGameIDIndicator.xaml
    /// </summary>
    public partial class StatusBarGameIDIndicator : NotifyPropertyChangedControlBase
    {
        public string GameType
        {
            get => (string)GetValue(GameTypeProperty);
            set
            {
                SetValue(GameTypeProperty, value);
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }

        public Visibility StatusVisibility => GameType != null && GameType.ToString() != "Unknown" ? Visibility.Visible : Visibility.Collapsed;

        // Using a DependencyProperty as the backing store for GameType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GameTypeProperty =
            DependencyProperty.Register(nameof(GameType), typeof(string), typeof(StatusBarGameIDIndicator), new PropertyMetadata(nameof(MEGame.Unknown)));

        //private static void OnGameTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    if (d is StatusBarGameIDIndicator indicator)
        //    {
        //        var game = e.NewValue?.ToString();
        //        indicator.Visibility =
        //    }
        //}

        public StatusBarGameIDIndicator()
        {
            InitializeComponent();
        }
    }
}
