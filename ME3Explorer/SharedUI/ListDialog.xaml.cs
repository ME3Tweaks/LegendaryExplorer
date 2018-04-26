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

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ListDialog.xaml
    /// </summary>
    public partial class ListDialog : Window
    {
        List<string> items;
        public ListDialog(List<string> listItems, String title, String message, int width = 0, int height = 0)
        {
            InitializeComponent();
            Title = title;
            ListDialog_Message.Text = message;
            items = listItems;
            if (width != 0)
            {
                Width = width;
            }
            if (height != 0)
            {
                Height = height;
            }
            foreach (string str in listItems)
            {
                ListDialog_List.Items.Add(str);
            }
        }

        private void CopyItemsToClipBoard_Click(object sender, RoutedEventArgs e)
        {
            string toClipboard = string.Join("\n", items);
            try
            {
                Clipboard.SetText(toClipboard);
                ListDialog_Status.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                //yes, this actually happens sometimes...
                MessageBox.Show("Could not set data to clipboard:\n" + ex.Message);
            }
        }
    }
}
