using System;
using System.Collections.Generic;
using System.IO;
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
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using UsefulThings.WPF;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for InitialDLCExtractor.xaml
    /// </summary>
    public partial class InitialDLCExtractor : Window
    {
        public ViewModel vm = null;

        public InitialDLCExtractor()
        {
            InitializeComponent();
            vm = new ViewModel();
            if (vm.Required == null)
            {
                MessageBox.Show("ME3 not found :(  Check your path and try again.");
                this.Close();
            }
            DataContext = vm;
        }

        private void DontExtractButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() =>
            {
                DLCEditor2.DLCEditor2 dlcedit2 = new DLCEditor2.DLCEditor2();
                DebugOutput.PrintLn("Extracting DLC...");
                dlcedit2.ExtractAllDLC();
            });

            MessageBox.Show("DLC Extracted!");
        }
    }

    public class ViewModel : ViewModelBase
    {
        public bool? Required = true;

        string requiredSpace = null;
        public string RequiredSpace
        {
            get
            {
                return requiredSpace;
            }
            set
            {
                SetProperty(ref requiredSpace, value);
            }
        }

        string availableSpace = null;
        public string AvailableSpace
        {
            get
            {
                return availableSpace;
            }
            set
            {
                SetProperty(ref availableSpace, value);
            }
        }

        public bool SpaceOK
        {
            get; set;
        }

        public ViewModel()
        {
            if (!Directory.Exists(ME3Directory.GamePath()))
            {
                Required = null;
                return;
            }

            double required = GetRequiredSize();
            if (required == 0)
            {
                Required = false;
                return;
            }

            double available = GetAvailableSpace();

            SpaceOK = available > required;

            RequiredSpace = UsefulThings.General.GetFileSizeAsString(required);
            AvailableSpace = UsefulThings.General.GetFileSizeAsString(available);
        }

        public double GetRequiredSize()
        {
            var folders = Directory.EnumerateDirectories(ME3Directory.DLCPath);
            var extracted = folders.Where(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(file => file.EndsWith(".bin")));
            var unextracted = folders.Except(extracted);

            double size = 0;
            foreach (var folder in unextracted)
            {
                if (folder.Contains("__metadata"))
                    continue;
                FileInfo info = new FileInfo(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(file => file.EndsWith(".sfar")).First());
                size += info.Length*1.1; // KFreon: Fudge factor for decompression
            }
            return size;
        }

        public double GetAvailableSpace()
        {
            var parts = ME3Directory.GamePath().Split(':');
            DriveInfo info = new DriveInfo(parts[0]);
            return info.AvailableFreeSpace;
        }
    }
}
