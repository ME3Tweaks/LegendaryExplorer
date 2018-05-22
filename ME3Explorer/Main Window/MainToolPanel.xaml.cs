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
    /// Interaction logic for MainToolPanel.xaml
    /// </summary>
    public partial class MainToolPanel : ToolListControl
    {
        public event EventHandler<Tool> ToolMouseOver;

        public MainToolPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            SortedDictionary<string, List<Tool>> subCats = new SortedDictionary<string, List<Tool>>(new ToolCategoryComparer());
            foreach (var tool in tools)
            {
                if (!subCats.ContainsKey(tool.subCategory))
                {
                    subCats.Add(tool.subCategory, new List<Tool>());
                }
                subCats[tool.subCategory].Add(tool);
            }
            SubCategoryList.ItemsSource = subCats;
            scrollIndicator.Visibility = Visibility.Visible;
        }

        protected override void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            base.Button_MouseEnter(sender, e);
            ToolMouseOver?.Invoke(sender, (sender as Button)?.DataContext as Tool);
        }

        protected override void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            base.Button_MouseLeave(sender, e);
        }

        /// <summary>
        /// Compares unicode string by bytes
        /// </summary>
        class ToolCategoryComparer : Comparer<string>
        {
            public override int Compare(string x, string y)
            {
                var a = Encoding.Unicode.GetBytes(x);
                var b = Encoding.Unicode.GetBytes(y);
                var len = Math.Min((int)a.Length, (int)b.Length);
                for (var i = 0; i < len; i++)
                {
                    var c = a[i].CompareTo(b[i]);
                    if (c != 0)
                    {
                        return c;
                    }
                }

                return a.Length.CompareTo(b.Length);
            }
        }
    }
}
