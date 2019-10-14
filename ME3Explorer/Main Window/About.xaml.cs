using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.AppCenter.Analytics;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();
            //azure testing. Will remove once testing is done.
            //force rebuild.
            MessageBox.Show("Analytics enabled: " + Analytics.IsEnabledAsync().ToString());
            MessageBox.Show("App Center Key: " + (APIKeys.HasAppCenterKey ? APIKeys.AppCenterKey : "No app center key"));

        }
    }
}
