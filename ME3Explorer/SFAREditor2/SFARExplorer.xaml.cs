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
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal;
using Microsoft.Win32;

namespace ME3Explorer.SFARUI
{
    /// <summary>
    /// Interaction logic for SFARExplorer.xaml
    /// </summary>
    public partial class SFARExplorer : NotifyPropertyChangedWindowBase
    {
        private DLCPackage _dlcPackage;
        public DLCPackage LoadedDLCPackage
        {
            get => _dlcPackage;
            set => SetProperty(ref _dlcPackage, value);
        }

        public SFARExplorer()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
        }

        public GenericCommand LoadDLCCommand { get; set; }

        private void LoadCommands()
        {
            LoadDLCCommand = new GenericCommand(PromptForDLC, CanLoadDLC);
        }


        private bool CanLoadDLC()
        {
            return true;
        }

        private void PromptForDLC()
        {
            OpenFileDialog ofd = new OpenFileDialog()
            {
                Filter = "SFAR files|*.sfar"
            };

            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                loadSfar(ofd.FileName);
            }
        }

        private void loadSfar(string sfarPath)
        {
            LoadedDLCPackage = new DLCPackage(sfarPath);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length != 1) return;
                var file = files[0];
                if (file.EndsWith(".sfar"))
                {
                    loadSfar(file);
                }

            }
        }
    }
}