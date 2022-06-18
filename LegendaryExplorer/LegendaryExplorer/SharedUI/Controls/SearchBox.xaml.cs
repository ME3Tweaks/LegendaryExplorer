using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.SharedUI.Controls
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
            DataContext = this;
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
