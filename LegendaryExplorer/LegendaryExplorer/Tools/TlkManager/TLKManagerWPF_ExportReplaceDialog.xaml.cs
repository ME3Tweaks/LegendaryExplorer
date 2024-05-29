using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK.ME1;
using LegendaryExplorerCore.TLK.ME2ME3;
using static LegendaryExplorer.Tools.TlkManagerNS.TLKManagerWPF;
using HuffmanCompression = LegendaryExplorerCore.TLK.ME2ME3.HuffmanCompression;

namespace LegendaryExplorer.Tools.TlkManagerNS
{
    /// <summary>
    /// Interaction logic for TLKManagerWPF_ExportReplaceDialog.xaml
    /// </summary>
    public partial class TLKManagerWPF_ExportReplaceDialog : TrackingNotifyPropertyChangedWindowBase, IBusyUIHost
    {
        public ICommand ReplaceSelectedTLK { get; private set; }
        public ICommand ExportSelectedTLK { get; private set; }
        public ICommand EditSelectedTLK { get; private set; }

        public ObservableCollectionExtended<LoadedTLK> TLKSources { get; set; } = new();

        #region Busy variables
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        public TLKManagerWPF_ExportReplaceDialog(List<LoadedTLK> loadedTLKs) : base("TLKManager Export Replace Dialog", false)
        {
            TLKSources.AddRange(loadedTLKs);
            DataContext = this;
            LoadCommands();
            InitializeComponent();

            for (int i = 0; i < loadedTLKs.Count; i++)
            {
                if (loadedTLKs[i].selectedForLoad)
                {
                    TLKList.SelectedItem = loadedTLKs[i];
                    TLKList.SelectedIndex = i;
                    break;
                }
            }

            foreach (var tlk in loadedTLKs)
                if (tlk.selectedForLoad)
                    TLKList.SelectedItems.Add(tlk);

            TLKList.Focus();
        }

        private void LoadCommands()
        {
            ReplaceSelectedTLK = new RelayCommand(ReplaceTLK, TLKSelected);
            ExportSelectedTLK = new RelayCommand(ExportTLK, TLKSelected);
            EditSelectedTLK = new RelayCommand(EditTLK, CanEditTLK);
        }

        private void EditTLK(object obj)
        {
            if (TLKList.SelectedItem is LoadedTLK { embedded: true } tlk)
            {
                //TODO: Need to find a way for the export loader to register usage of the pcc.
                IMEPackage pcc = MEPackageHandler.OpenMEPackage(tlk.tlkPath);
                var export = pcc.GetUExport(tlk.exportNumber);
                var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader() { ForceHideRecents = true }, export)
                {
                    Title = $"TLK Editor - {export.UIndex} {export.InstancedFullPath} - {export.FileRef.FilePath}",
                };
                elhw.Show();
            }
        }

        private bool CanEditTLK(object obj)
        {
            //Current code checks if it is ME1 as currently only ME1 TLK can be loaded into an export loader for ME1TLKEditor.
            return TLKList.SelectedItems.Count == 1 && TLKList.SelectedItem is LoadedTLK { embedded: true };
        }

        private void ExportTLK(object obj)
        {
            string saveFolder = "";
            if (TLKList.SelectedItems.Count > 1)
            {
                var saveFolderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select destination folder",
                    UseDescriptionForTitle = true
                };
                if (saveFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    saveFolder = saveFolderDialog.SelectedPath;
                else
                    return;
            }

            var loadingWorker = new BackgroundWorker();
            foreach (LoadedTLK tlk in TLKList.SelectedItems)
            {
                string saveFile = "";
                if (saveFolder == "")
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "XML Files (*.xml)|*.xml"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                        saveFile = saveFileDialog.FileName;
                    else
                        return;
                }
                else
                    saveFile = System.IO.Path.ChangeExtension(saveFolder + "\\" + System.IO.Path.GetFileName(tlk.tlkPath), "xml");

                if (tlk.exportNumber != 0)
                {
                    //ME1
                    loadingWorker.DoWork += delegate
                    {
                        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(tlk.tlkPath);
                        if (!pcc.Game.IsGame1())
                            throw new Exception($@"ME1/LE1 pacakges are the only ones that contain TLK exports. The selected package is for {pcc.Game}");
                        var talkfile = new ME1TalkFile(pcc, tlk.exportNumber);
                        talkfile.SaveToXML(saveFile);
                    };
                }
                else
                {
                    //ME2,ME3
                    loadingWorker.DoWork += delegate
                    {
                        var tf = new ME2ME3TalkFile(tlk.tlkPath);
                        tf.SaveToXML(saveFile);
                    };
                }
            }
            BusyText = "Exporting TLK to XML";
            IsBusy = true;
            loadingWorker.RunWorkerCompleted += delegate
            {
                IsBusy = false;
            };
            loadingWorker.RunWorkerAsync();
        }

        private void ReplaceTLK(object obj)
        {
            string openFolder = "";
            if (TLKList.SelectedItems.Count > 1)
            {
                var openFolderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select source folder",
                    UseDescriptionForTitle = true
                };
                if (openFolderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    openFolder = openFolderDialog.SelectedPath;
                else
                    return;

                string missing = "";
                int missinglimit = 10;
                foreach (LoadedTLK tlk in TLKList.SelectedItems)
                {
                    var openFile = System.IO.Path.ChangeExtension(System.IO.Path.GetFileName(tlk.tlkPath), "xml");
                    if (!System.IO.File.Exists(openFolder + "\\" + openFile))
                        if (missinglimit-- > 0)
                            missing += "    " + openFile + "\n";
                        else
                        {
                            missing += "    <...>\n";
                            break;
                        }
                }
                if (missing != "")
                {
                    System.Windows.Forms.MessageBox.Show("The following files were not found in the source folder:\n\n" + missing, "Replace operation aborted", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Stop);
                    return;
                }
            }

            var replacingWork = new BackgroundWorker();
            foreach (LoadedTLK tlk in TLKList.SelectedItems)
            {
                var openFile = "";
                if (openFolder == "")
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Multiselect = false,
                        Filter = "XML Files (*.xml)|*.xml",
                        CustomPlaces = AppDirectories.GameCustomPlaces
                    };

                    if (openFileDialog.ShowDialog() == true)
                        openFile = openFileDialog.FileName;
                    else
                        return;
                }
                else
                    openFile = System.IO.Path.ChangeExtension(openFolder + "\\" + System.IO.Path.GetFileName(tlk.tlkPath), "xml");

                if (tlk.exportNumber != 0)
                {
                    //ME1
                    replacingWork.DoWork += delegate
                    {
                        LegendaryExplorerCore.TLK.ME1.HuffmanCompression compressor = new();
                        compressor.LoadInputData(openFile);
                        using IMEPackage pcc = MEPackageHandler.OpenME1Package(tlk.tlkPath);
                        compressor.SerializeTalkfileToExport(pcc.GetUExport(tlk.exportNumber), true);
                    };
                }
                else
                {
                    //ME2,ME3

                    replacingWork.DoWork += delegate
                    {
                        var hc = new HuffmanCompression();
                        hc.LoadInputData(openFile);
                        hc.SaveToFile(tlk.tlkPath);
                    };
                }
            }
            BusyText = "Converting XML to TLK";
            IsBusy = true;
            replacingWork.RunWorkerCompleted += delegate
            {
                IsBusy = false;
            };
            replacingWork.RunWorkerAsync();
        }

        private bool TLKSelected(object obj)
        {
            return TLKList.SelectedItem != null;
        }
    }
}
