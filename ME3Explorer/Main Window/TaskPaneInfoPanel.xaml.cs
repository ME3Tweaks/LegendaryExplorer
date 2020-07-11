using System;
using System.Collections.Generic;
using System.IO;
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

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for TaskPaneInfoPanel.xaml
    /// </summary>
    public partial class TaskPaneInfoPanel : NotifyPropertyChangedControlBase
    {
        public TaskPaneInfoPanel()
        {
            InitializeComponent();
            DataContext = this;
        }

        public WPFBase current;

        public event EventHandler Close;

        private Tool _tool;

        public Tool tool
        {
            get => _tool;
            set => SetProperty(ref _tool, value);
        }

        private string _filename;

        public string Filename
        {
            get => _filename;
            set => SetProperty(ref _filename, value);
        }

        public void setTool(WPFBase gen)
        {
            current = gen;
            Filename = Path.GetFileName(gen.Pcc?.FilePath);
            var type = gen.GetType();
            tool = Tools.Items.FirstOrDefault(t => t.type == type);
            current.Closed += Current_Disposing;
            BitmapSource bitmap = gen.DrawToBitmapSource();
            double scale = screenShot.Width / (bitmap.Width > bitmap.Height ? bitmap.Width : bitmap.Height);
            screenShot.Source = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        }

        private void Current_Disposing(object sender, EventArgs e)
        {
            current.Closed -= Current_Disposing;
            Close?.Invoke(this, EventArgs.Empty);
        }

        private void viewButton_Click(object sender, RoutedEventArgs e)
        {
            current?.RestoreAndBringToFront();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            current?.Close();
        }
    }
}
