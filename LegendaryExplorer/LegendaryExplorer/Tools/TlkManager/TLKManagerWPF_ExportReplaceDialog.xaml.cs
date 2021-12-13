using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
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
                var elhw = new ExportLoaderHostedWindow(new TLKEditorExportLoader(), export)
                {
                    Title = $"TLK Editor - {export.UIndex} {export.InstancedFullPath} - {export.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        private bool CanEditTLK(object obj)
        {
            //Current code checks if it is ME1 as currently only ME1 TLK can be loaded into an export loader for ME1TLKEditor.
            return TLKList.SelectedItem is LoadedTLK {embedded: true};
        }

        private void ExportTLK(object obj)
        {
            if (TLKList.SelectedItem is LoadedTLK tlk)
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "XML Files (*.xml)|*.xml"
                };
                if (saveFileDialog.ShowDialog() == true)
                {
                    BusyText = "Exporting TLK to XML";
                    IsBusy = true;
                    var loadingWorker = new BackgroundWorker();

                    if (tlk.exportNumber != 0)
                    {
                        //ME1
                        loadingWorker.DoWork += delegate
                        {
                            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(tlk.tlkPath);
                            if (!pcc.Game.IsGame1())
                                throw new Exception($@"ME1/LE1 pacakges are the only ones that contain TLK exports. The selected package is for {pcc.Game}");
                            var talkfile = new ME1TalkFile(pcc, tlk.exportNumber);
                            talkfile.SaveToXMLFile(saveFileDialog.FileName);
                        };
                    }
                    else
                    {
                        //ME2,ME3
                        loadingWorker.DoWork += delegate
                        {
                            var tf = new TalkFile();
                            tf.LoadTlkData(tlk.tlkPath);
                            tf.DumpToFile(saveFileDialog.FileName);
                        };
                    }
                    loadingWorker.RunWorkerCompleted += delegate
                    {
                        IsBusy = false;
                    };
                    loadingWorker.RunWorkerAsync();
                }
            }
        }

        private void ReplaceTLK(object obj)
        {
            if (TLKList.SelectedItem is LoadedTLK tlk)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Multiselect = false,
                    Filter = "XML Files (*.xml)|*.xml"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    BusyText = "Converting XML to TLK";
                    IsBusy = true;
                    var replacingWork = new BackgroundWorker();

                    if (tlk.exportNumber != 0)
                    {
                        //ME1
                        replacingWork.DoWork += delegate
                        {
                            LegendaryExplorerCore.TLK.ME1.HuffmanCompression compressor = new();
                            compressor.LoadInputData(openFileDialog.FileName);
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
                            hc.LoadInputData(openFileDialog.FileName);
                            hc.SaveToFile(tlk.tlkPath);
                        };
                    }
                    replacingWork.RunWorkerCompleted += delegate
                    {
                        IsBusy = false;
                    };
                    replacingWork.RunWorkerAsync();
                }
            }
        }

        private bool TLKSelected(object obj)
        {
            return TLKList.SelectedItem != null;
        }
    }
}
