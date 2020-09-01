using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ME3ExplorerCore.Packages;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for StatusBarGameIDIndicator.xaml
    /// </summary>
    public partial class StatusBarGameIDIndicator : UserControl
    {
        
        public MEGame GameType
        {
            get => (MEGame)GetValue(GameTypeProperty);
            set => SetValue(GameTypeProperty, value);
        }

        // Using a DependencyProperty as the backing store for GameType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GameTypeProperty =
            DependencyProperty.Register(nameof(GameType), typeof(MEGame), typeof(StatusBarGameIDIndicator), new PropertyMetadata(MEGame.ME3, OnGameTypeChanged));

        private static void OnGameTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusBarGameIDIndicator indicator && e.NewValue is MEGame game)
            {
                switch (game)
                {
                    case MEGame.ME1:
                        indicator.StatusBar_GameID_Text.Text = "ME1";
                        indicator.StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                        break;
                    case MEGame.ME2:
                        indicator.StatusBar_GameID_Text.Text = "ME2";
                        indicator.StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                        break;
                    case MEGame.ME3:
                        indicator.StatusBar_GameID_Text.Text = "ME3";
                        indicator.StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                        break;
                    case MEGame.UDK:
                        indicator.StatusBar_GameID_Text.Text = "UDK";
                        indicator.StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.IndianRed);
                        break;
                }
            }
        }

        public StatusBarGameIDIndicator()
        {
            InitializeComponent();
        }
    }
}
