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
using Gammtek.Conduit.Extensions;
using ME3Explorer.Packages;
using Microsoft.Win32;

namespace ME3Explorer.Audio_Editor
{
    /// <summary>
    /// Interaction logic for AudioEditor.xaml
    /// </summary>
    public partial class AudioEditor : WPFBase
    {
        public AudioEditor()
        {
            InitializeComponent();
        }

        private void OpenFile()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "ME3 PCC Files|*.pcc";
            dlg.Multiselect = false;

            if (dlg.ShowDialog() == true)
            {
                LoadME3Package(dlg.FileName);
            }
        }

        private void SaveFile()
        {
            throw new NotImplementedException();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //TODO: implement handleUpdate
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = pcc != null && !pcc.FileName.IsNullOrWhiteSpace();
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFile();
        }

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFile();
        }
    }
}
