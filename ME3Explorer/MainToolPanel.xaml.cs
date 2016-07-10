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
        public MainToolPanel()
        {
            InitializeComponent();
        }

        public override void setToolList(IEnumerable<Tool> enumerable)
        {
            base.setToolList(enumerable);
            Dictionary<string, List<Tool>> subCats = new Dictionary<string, List<Tool>>();
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
        }

        protected override void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            base.Button_MouseLeave(sender, e);
        }
    }
}
