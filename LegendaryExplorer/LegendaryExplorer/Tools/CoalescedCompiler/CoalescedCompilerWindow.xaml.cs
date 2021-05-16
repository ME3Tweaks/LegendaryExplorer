using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorerCore.Coalesced;
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

			SourcePath = dlg.FileName;
			var sourceExtension = Path.GetExtension(SourcePath) ?? "";

			switch (sourceExtension.ToLower())
			{
				case ".bin":
				{
					SourceType = CoalescedType.Binary;

					break;
				}
				case ".xml":
                {
                    SourceType = CoalescedType.Xml;
                    break;
				}
                case ".extractedbin":
                {
                    SourceType = CoalescedType.ExtractedBin;
                    break;
                }
            }

			if (!string.IsNullOrEmpty(DestinationPath) && ChangeDestinationCheckBox.IsChecked == false)
			{
				return;
			}

			switch (SourceType)
			{
				case CoalescedType.Binary:
                {
                    if (CoalescedConverter.IsOTCoalesced(SourcePath))
                    {
                        ConvertingLECoalesced = false;
                        DestinationPath = Path.ChangeExtension(SourcePath, null);
                        DestinationType = CoalescedType.Xml;
                    }
                    else
                    {
                        ConvertingLECoalesced = true;
                        DestinationPath = Path.ChangeExtension(SourcePath, null);
                        DestinationType = CoalescedType.ExtractedBin;
                    }

					break;
				}
				case CoalescedType.Xml:
                {
					DestinationPath = Path.ChangeExtension(SourcePath, "bin");
					DestinationType = CoalescedType.Binary;
                    break;
				}
                case CoalescedType.ExtractedBin:
                {
                    DestinationPath = Path.Combine(Path.GetDirectoryName(SourcePath), LECoalescedConverter.GetDestinationPathFromManifest(SourcePath));
                    DestinationType = CoalescedType.Binary;
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
                    if (!Directory.Exists(DestinationPath))
					{
						Directory.CreateDirectory(DestinationPath);
					}
                    CoalescedConverter.ConvertToBin(SourcePath, DestinationPath);
					break;
				case CoalescedType.ExtractedBin:
                    if (!Directory.Exists(DestinationPath))
                    {
                        Directory.CreateDirectory(DestinationPath);
                    }
					LECoalescedConverter.Pack(SourcePath, DestinationPath);
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
    }
}
