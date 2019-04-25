using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using ME3Explorer;
using ME3Explorer.Packages;
using Microsoft.Win32;

namespace MassEffect.NativesEditor.Views
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class ShellView : WPFBase, INotifyPropertyChanged
	{
		public ShellView()
		{
            ME3Explorer.ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Plot Editor", new WeakReference(this));
            WindowTitle = "Plot Editor";
			InitializeComponent();
            FindObjectUsagesControl.parentRef = this;
        }

        private string _windowTitle;
        private string _fileName;
        
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public void OpenFile()
        {
            var dlg = new OpenFileDialog {Filter = "ME3 PCC Files|*.pcc", Multiselect = false};

            if (dlg.ShowDialog() != true)
			{
				return;
			}

			OpenPccFile(dlg.FileName);

            //OpenPccFile(@"C:\Program Files (x86)\Origin Games\Mass Effect 3\BIOGame\CookedPCConsole\SFXGameInfoSP_SF.pcc");
            //OpenPccFile(@"_Test\SFXGameInfoSP_SF.pcc");
        }

        public void OpenPccFile(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (!File.Exists(path))
            {
                return;
            }

            FileName = path;
            LoadME3Package(path);

            CodexMapControl?.Open(Pcc);

            QuestMapControl?.Open(Pcc);

            StateEventMapControl?.Open(Pcc);
        }

        public void SaveFile()
        {
            if (Pcc == null)
            {
                return;
            }

            if (CodexMapControl != null)
            {

                if (CodexMapView.TryFindCodexMap(Pcc, out IExportEntry export, out int _))
                {
                    using (var stream = new MemoryStream())
                    {
                        var codexMap = CodexMapControl.ToCodexMap();
                        var binaryCodexMap = new BinaryBioCodexMap(codexMap.Sections, codexMap.Pages);

                        binaryCodexMap.Save(stream);

                        export.setBinaryData(stream.ToArray());
                    }
                }
            }

            if (QuestMapControl != null)
            {

                if (QuestMapControl.TryFindQuestMap(Pcc, out IExportEntry export, out int _))
                {
                    using (var stream = new MemoryStream())
                    {
                        var questMap = QuestMapControl.ToQuestMap();
                        var binaryQuestMap = new BinaryBioQuestMap(questMap.Quests, questMap.BoolTaskEvals, questMap.IntTaskEvals, questMap.FloatTaskEvals);

                        binaryQuestMap.Save(stream);

                        export.setBinaryData(stream.ToArray());
                    }
                }
            }

            if (StateEventMapControl != null)
            {

                if (StateEventMapControl.TryFindStateEventMap(Pcc, out IExportEntry export, out int _))
                {
                    using (var stream = new MemoryStream())
                    {
                        var stateEventMap = StateEventMapControl.ToStateEventMap();
                        var binaryStateEventMap = new BinaryBioStateEventMap(stateEventMap.StateEvents);

                        binaryStateEventMap.Save(stream);

                        export.setBinaryData(stream.ToArray());
                    }
                }
            }

            Pcc.save(FileName);
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            //TODO: implement handleUpdate
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute =  Pcc != null && !string.IsNullOrEmpty(FileName);
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
