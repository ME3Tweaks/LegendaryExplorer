using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LegendaryExplorer
{
    /// <summary>
    /// Interaction logic for ToolPanel.xaml
    /// </summary>
    public partial class ToolPanel : ToolListControl
    {
        public string Category
        {
            get => (string)GetValue(CategoryProperty);
            set => SetValue(CategoryProperty, value);
        }

        // Using a DependencyProperty as the backing store for Category.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CategoryProperty =
            DependencyProperty.Register(nameof(Category), typeof(string), typeof(ToolPanel), new PropertyMetadata(""));

        public string Subtext
        {
            get => (string)GetValue(SubtextProperty);
            set => SetValue(SubtextProperty, value);
        }

        public static readonly DependencyProperty SubtextProperty = DependencyProperty.Register(
            nameof(Subtext), typeof(string), typeof(ToolPanel), new PropertyMetadata(default(string)));

        public Thickness ItemMargin
        {
            get => (Thickness)GetValue(ItemMarginProperty);
            set => SetValue(ItemMarginProperty, value);
        }

        // Using a DependencyProperty as the backing store for ItemMargin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemMarginProperty =
            DependencyProperty.Register(nameof(ItemMargin), typeof(Thickness), typeof(ToolPanel), new PropertyMetadata(new Thickness(0,15,15,0)));

        public Thickness ItemControlMargin
        {
            get => (Thickness)GetValue(ItemControlMarginProperty);
            set => SetValue(ItemControlMarginProperty, value);
        }

        // Using a DependencyProperty as the backing store for ItemControlMargin.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemControlMarginProperty =
            DependencyProperty.Register(nameof(ItemControlMargin), typeof(Thickness), typeof(ToolPanel), new PropertyMetadata(new Thickness(0)));
        
        public int Rows
        {
            get => (int)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        // Using a DependencyProperty as the backing store for Rows.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register(nameof(Rows), typeof(int), typeof(ToolPanel), new PropertyMetadata(1));

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        // Using a DependencyProperty as the backing store for Columns.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(ToolPanel), new PropertyMetadata(1));

        public int viewCapacity => Rows * Columns;

        public event EventHandler<Tool> ToolMouseOver;

        private int index;

        public ToolPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            while (index > tools.Count)
            {
                index -= viewCapacity;
            }
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
            if (tools.Count > index + viewCapacity)
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

        protected override void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            base.Button_MouseEnter(sender, e);
            ToolMouseOver?.Invoke(sender, (sender as Button)?.DataContext as Tool);
        }
    }
}
