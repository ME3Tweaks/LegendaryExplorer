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
    /// Interaction logic for InfoPanel.xaml
    /// </summary>
    public partial class SearchPanel : UserControl
    {
        public ImageSource Icon
        {
            get { return (ImageSource)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Icon.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(ImageSource), typeof(SearchPanel), new PropertyMetadata(null));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Title.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(SearchPanel), new PropertyMetadata(""));


        private IEnumerable<Tool> tools;

        public SearchPanel()
        {
            InitializeComponent();
        }

        public void setToolList(IEnumerable<Tool> enumerable)
        {
            List<Tool> list = enumerable.ToList();
            list.Sort((x, y) => x.name.CompareTo(y.name));
            tools = list;
            ToolList.ItemsSource = tools;
            scrollIndicator.Visibility = list.Count >= 10 ? Visibility.Visible : Visibility.Hidden;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ((sender as Button)?.DataContext as Tool)?.open();
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                Rectangle r = b.FindName("highlightUnderline") as Rectangle;
                if (r != null)
                {
                    r.Visibility = Visibility.Visible;
                }
                Image img = b.FindName("toolIcon") as Image;
                if (img != null)
                {
                    img.Opacity = 1;
                }
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                Rectangle r = b.FindName("highlightUnderline") as Rectangle;
                if (r != null)
                {
                    r.Visibility = Visibility.Hidden;
                }
                Image img = b.FindName("toolIcon") as Image;
                if (img != null)
                {
                    img.Opacity = 0.85;
                }
            }
        }
    }
}
