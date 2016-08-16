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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPaneInfoPanel.xaml
    /// </summary>
    public partial class TaskPaneInfoPanel : UserControl
    {
        public TaskPaneInfoPanel()
        {
            InitializeComponent();
        }

        public GenericWindow current;

        public event EventHandler Close;

        public void setTool(GenericWindow gen)
        {
            this.DataContext = current = gen;
            current.Disposing += Current_Disposing;
            BitmapSource bitmap = gen.GetImage();
            double scale = screenShot.Width / (bitmap.Width > bitmap.Height ? bitmap.Width : bitmap.Height);
            screenShot.Source = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        }

        private void Current_Disposing(object sender, EventArgs e)
        {
            current.Disposing -= Current_Disposing;
            Close?.Invoke(this, EventArgs.Empty);
        }

        private void viewButton_Click(object sender, RoutedEventArgs e)
        {
            current?.BringToFront();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            current?.Close();
        }
    }
}
