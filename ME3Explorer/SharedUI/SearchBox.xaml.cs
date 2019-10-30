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

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for SearchBox.xaml
    /// </summary>
    public partial class SearchBox : NotifyPropertyChangedControlBase
    {
        public static readonly DependencyProperty WatermarkTextProperty = DependencyProperty.Register(
            nameof(WatermarkText), typeof(string), typeof(SearchBox), new PropertyMetadata(default(string)));

        public string WatermarkText
        {
            get => (string)GetValue(WatermarkTextProperty);
            set => SetValue(WatermarkTextProperty, value);
        }

        public delegate void SearchBoxTextChangedEventHandler(SearchBox sender, string newText);

        public event SearchBoxTextChangedEventHandler TextChanged;

        private string _text;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public SearchBox()
        {
            InitializeComponent();
        }

        public void Clear()
        {
            searchBox.Clear();
        }

        private void clearSearchTextButton_Clicked(object sender, RoutedEventArgs e)
        {
            searchBox.Text = "";
        }

        private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            Text = searchBox.Text;
            TextChanged?.Invoke(this, Text);
        }

        private void SearchBox_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            searchBox.Focus();
        }
    }
}
