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
using Microsoft.Win32;
using UsefulThings.WPF;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for CreateJobFromPCCDiff.xaml
    /// </summary>
    public partial class CreateJobFromPCCDiff : Window
    {
        CreateModPCCVM vm;
        ModMaker ModmakerParent;

        public CreateJobFromPCCDiff()
        {
            ModmakerParent = ModMaker.GetCurrentInstance().Result;
            if (ModmakerParent == null)
                return;

            
            InitializeComponent();
            vm = new CreateModPCCVM();
            DataContext = vm;
        }

        private void BaseBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string browsed = Browse("Select base PCC to compare against");
            if (!string.IsNullOrEmpty(browsed))
                vm.BasePCCPath = browsed;
        }

        private void ModifiedBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string browsed = Browse("Select modified PCC");
            if (!string.IsNullOrEmpty(browsed))
                vm.ModPCCPath = browsed;
        }

        private string Browse(string title)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = title;
            ofd.Filter = "BioWare PCC's|*.pcc";
            if (ofd.ShowDialog() == true)
                return ofd.FileName;

            return null;
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            vm.TitleText = "Comparing...Please wait.";
            await Task.Run(() => ModmakerParent.CreateJobsFromPCCDiff(vm.BasePCCPath, vm.ModPCCPath));
            this.Close();
        }

        private void CancellationButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class CreateModPCCVM : ViewModelBase
    {
        string titleText = "Select PCC's to compare";
        public string TitleText
        {
            get
            {
                return titleText;
            }
            set
            {
                SetProperty(ref titleText, value);
            }
        }

        string basePCCPath = null;
        public string BasePCCPath
        {
            get
            {
                return basePCCPath;
            }
            set
            {
                SetProperty(ref basePCCPath, value);
            }
        }

        string modPCCPath = null;
        public string ModPCCPath
        {
            get
            {
                return modPCCPath;
            }
            set
            {
                SetProperty(ref modPCCPath, value);
            }
        }

        
    }
}
