using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DocumentFormat.OpenXml.Bibliography;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Unreal;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace LegendaryExplorer.Tools.CoalescedCompiler
{
    /// <summary>
    ///     Interaction logic for CoalescedCompilerWindow.xaml
    /// </summary>
    public partial class CoalescedCompilerWindow : TrackingNotifyPropertyChangedWindowBase
	{
		public CoalescedCompilerWindow() : base("Coalesced Compiler", true)
		{
			LoadCommands();
            if (!string.IsNullOrEmpty(SourcePath))
                (SourceType, DestinationType) = GetCoalescedTupleFromPath(SourcePath);

            InitializeComponent();
			DataContext = this;
        }

		public ICommand SelectSourceCommand { get; set; }
		public ICommand SelectDestinationCommand { get; set; }
        public ICommand ConvertCommand { get; set; }

		private void LoadCommands()
        {
			SelectSourceCommand = new GenericCommand(SelectSourceFile);
			SelectDestinationCommand = new GenericCommand(SelectDestinationFile, () => !string.IsNullOrEmpty(SourcePath));
            ConvertCommand = new GenericCommand(Convert, () => !string.IsNullOrEmpty(SourcePath) && !string.IsNullOrEmpty(DestinationPath));
        }

        private bool _convertingLECoalesced;
        public bool ConvertingLECoalesced
        {
            get => _convertingLECoalesced;
            set => SetProperty(ref _convertingLECoalesced, value);
        }

		private string _destinationPath = Settings.CoalescedEditor_DestinationPath;
		public string DestinationPath
		{
			get => _destinationPath;
		    set => SetProperty(ref _destinationPath, value);
		}

		private CoalescedType _destinationType;
		public CoalescedType DestinationType
		{
			get => _destinationType;
		    set => SetProperty(ref _destinationType, value);
		}

		private string _sourcePath = Settings.CoalescedEditor_SourcePath;
		public string SourcePath
		{
			get => _sourcePath;
		    set => SetProperty(ref _sourcePath, value);
		}

		private CoalescedType _sourceType;
		public CoalescedType SourceType
		{
			get => _sourceType;
		    set => SetProperty(ref _sourceType, value);
		}

		private void SelectSourceFile()
		{
			var dlg = new CommonOpenFileDialog("Open File");
			dlg.Filters.Add(new CommonFileDialogFilter("Coalesced Files", "*.bin;*.xml;*.extractedbin"));
			dlg.Filters.Add(new CommonFileDialogFilter("Binary Coalesced Files", "*.bin"));
			dlg.Filters.Add(new CommonFileDialogFilter("XML Coalesced Files", "*.xml"));
            dlg.Filters.Add(new CommonFileDialogFilter("LE Coalesced Manifest Files", "*.extractedbin"));

			if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
			{
				return;
			}

			SetSourceFile(dlg.FileName);
        }

        private void SetSourceFile(string sourcePath)
        {
            SourcePath = sourcePath;

            (SourceType, DestinationType) = GetCoalescedTupleFromPath(sourcePath);

            if (!string.IsNullOrEmpty(DestinationPath) && ChangeDestinationCheckBox.IsChecked == false)
            {
                return;
            }

            switch (SourceType)
            {
                case CoalescedType.Binary:
                {
                    // Output to folder
                    ConvertingLECoalesced = !CoalescedConverter.IsOTCoalesced(SourcePath);
                    DestinationPath = Path.ChangeExtension(SourcePath, null);
                    break;
                }
                case CoalescedType.Xml:
                {
                    DestinationPath = Path.ChangeExtension(SourcePath, "bin");
                    break;
                }
                case CoalescedType.ExtractedBin:
                {
                    DestinationPath = Path.Combine(Path.GetDirectoryName(SourcePath),
                        LECoalescedConverter.GetDestinationPathFromManifest(SourcePath));
                    break;
                }
            }
        }

        private void SelectDestinationFile()
		{
			switch (SourceType)
			{
				case CoalescedType.Binary:
				{
					var dlg = new CommonOpenFileDialog("Select Folder")
					{
						IsFolderPicker = true
					};

					if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
					{
						return;
					}

                    DestinationPath = dlg.FileName;
                    break;
				}

				case CoalescedType.Xml:
				case CoalescedType.ExtractedBin:
                {
						var dlg = new CommonOpenFileDialog("Open File");
                        dlg.Filters.Add(new CommonFileDialogFilter("Binary Coalesced Files", "*.bin"));

                        if (dlg.ShowDialog(this) != CommonFileDialogResult.Ok)
						{
							return;
						}

						DestinationPath = dlg.FileName;

						/*if (!Path.HasExtension(DestinationPath))
						{
							return;
						}

						var destinationExtension = Path.GetExtension(DestinationPath) ?? "";

						switch (destinationExtension.ToLower())
						{
							case ".bin":
							{
								break;
							}
							case ".xml":
							{
								break;
							}
						}*/

						break;
					}
			}
		}

        private void Convert()
		{
			if (!Path.IsPathRooted(SourcePath))
			{
				SourcePath = Path.GetFullPath(SourcePath);
			}

			if (!Path.IsPathRooted(DestinationPath))
			{
				DestinationPath = Path.GetFullPath(DestinationPath);
			}

			if (!File.Exists(SourcePath))
			{
				throw new FileNotFoundException("Source file not found.");
			}

            switch (SourceType)
			{

				case CoalescedType.Binary:
                    ConvertingLECoalesced = !CoalescedConverter.IsOTCoalesced(SourcePath);
                    if (!Directory.Exists(Path.GetDirectoryName(DestinationPath) ?? DestinationPath))
					{
						Directory.CreateDirectory(DestinationPath);
					}

                    if (ConvertingLECoalesced)
                    {
                        LECoalescedConverter.Unpack(SourcePath, DestinationPath);
					}
                    else
                    {
                        CoalescedConverter.ConvertToXML(SourcePath, DestinationPath);
                    }
					break;
				case CoalescedType.Xml:
                    CoalescedConverter.ConvertToBin(SourcePath, DestinationPath);
					break;
				case CoalescedType.ExtractedBin:
                    var containingFolder = Path.GetDirectoryName(SourcePath);
                    LECoalescedConverter.Pack(containingFolder, DestinationPath);
                    break;
				default:
                    throw new ArgumentOutOfRangeException();
			}

            MessageBox.Show("Done");
		}

		private void Root_Closed(object sender, EventArgs e)
		{
			Settings.CoalescedEditor_DestinationPath = DestinationPath;
			Settings.CoalescedEditor_SourcePath = SourcePath;
			Settings.Save();
		}

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Checking for unpacked coalesced folder
                if (Directory.Exists(files[0]))
                {
                    var info = new DirectoryInfo(files[0]);

                    // We don't check for XML because it's hard to tell if there's actually a manifest
                    if (!info.GetFiles().Any((f) =>
                        f.Name.ToLower().EndsWith(".extractedbin")))
                    {
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                    }
                }
                else
                {

                    string ext = Path.GetExtension(files[0]).ToLower();
                    if (ext != ".bin" && ext != ".xml" && ext != ".extractedbin")
                    {
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                    }
                }
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);


                if (Directory.Exists(files[0]))
                {
                    var dirFiles = (new DirectoryInfo(files[0])).GetFiles();
                    var extractedBin = dirFiles.FirstOrDefault(f => f.Extension == "extractedbin");
                    if(extractedBin != default(FileInfo)) SetSourceFile(extractedBin.FullName);

                }
                else
                {
                    SetSourceFile(files[0]);
                }

            }
		}

        /// <summary>
        /// Returns a tuple of the Source and Destination types for the given file path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private (CoalescedType, CoalescedType) GetCoalescedTupleFromPath(string path)
        {
            var info = new FileInfo(path);
            switch (info.Extension)
            {
                case ".bin":
                    return CoalescedConverter.IsOTCoalesced(path) ? (CoalescedType.Binary, CoalescedType.Xml) : (CoalescedType.Binary, CoalescedType.ExtractedBin);
                case ".extractedbin":
                    return (CoalescedType.ExtractedBin, CoalescedType.Binary);
                default:
                    return (CoalescedType.Xml, CoalescedType.Binary);
            }
        }
    }
}
