using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LegendaryExplorer.MainWindow
{
    /// <summary>
    /// Interaction logic for ToolPanel.xaml
    /// </summary>
    public partial class ToolPanel : ToolListControl
    {
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

        public event EventHandler<Tool> ToolMouseOver;

        public ToolPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            ToolList.ItemsSource = tools;
        }
        
        private void Button_GotFocus(object sender, RoutedEventArgs e)
        {
        }

        protected override void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            base.Button_MouseEnter(sender, e);
            ToolMouseOver?.Invoke(sender, (sender as Button)?.DataContext as Tool);
        }
    }
}
