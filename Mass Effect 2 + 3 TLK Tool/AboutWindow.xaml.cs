using System.Windows;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public string ApplicationVersion
        {
            get { return "v. " + App.GetVersion(); }
        }
        
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
