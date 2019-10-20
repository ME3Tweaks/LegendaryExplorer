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
    public partial class SearchBox : UserControl
    {
        public delegate void SearchBoxTextChangedEventHandler(SearchBox sender, string newText);

        public event SearchBoxTextChangedEventHandler TextChanged;

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
            string text = searchBox.Text.ToLower();
            TextChanged?.Invoke(this, text);
        }

        private void SearchBox_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            searchBox.Focus();
        }
    }
}
