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
    /// Interaction logic for ToolPanel.xaml
    /// </summary>
    public partial class ToolPanel : ToolListControl
    {
        public string Category
        {
            get { return (string)GetValue(CategoryProperty); }
            set { SetValue(CategoryProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Category.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register("Category", typeof(string), typeof(ToolPanel), new PropertyMetadata(""));

        public Thickness ItemMargin
        {
            get { return (Thickness)GetValue(ItemMarginProperty); }
            set { SetValue(ItemMarginProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemMargin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemMarginProperty =
            DependencyProperty.Register("ItemMargin", typeof(Thickness), typeof(ToolPanel), new PropertyMetadata(new Thickness(0,15,15,0)));

        public Thickness ItemControlMargin
        {
            get { return (Thickness)GetValue(ItemControlMarginProperty); }
            set { SetValue(ItemControlMarginProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemControlMargin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemControlMarginProperty =
            DependencyProperty.Register("ItemControlMargin", typeof(Thickness), typeof(ToolPanel), new PropertyMetadata(new Thickness(0)));
        
        public int Rows
        {
            get { return (int)GetValue(RowsProperty); }
            set { SetValue(RowsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Rows.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register("Rows", typeof(int), typeof(ToolPanel), new PropertyMetadata(1));

        public int Columns
        {
            get { return (int)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Columns.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register("Columns", typeof(int), typeof(ToolPanel), new PropertyMetadata(1));

        public int viewCapacity { get { return Rows * Columns; } }

        private int index;

        public ToolPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            index = 0;
            updateContents();
        }
        
        private void Button_GotFocus(object sender, RoutedEventArgs e)
        {

        }
        
        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            index -= viewCapacity;
            updateContents();
        }

        private void forwardButton_Click(object sender, RoutedEventArgs e)
        {
            index += viewCapacity;
            updateContents();
        }

        private void updateContents()
        {
            ToolList.ItemsSource = tools.Skip(index).Take(viewCapacity);
            if (tools.Count() > index + viewCapacity)
            {
                forwardButton.Visibility = Visibility.Visible;
            }
            else
            {
                forwardButton.Visibility = Visibility.Collapsed;
            }
            if (index > 0)
            {
                backButton.Visibility = Visibility.Visible;
            }
            else
            {
                backButton.Visibility = Visibility.Collapsed;
            }
        }
    }
}
