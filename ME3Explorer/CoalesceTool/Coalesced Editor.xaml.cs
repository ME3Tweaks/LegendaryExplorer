using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MassEffect3.Coalesce;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace MassEffect3.CoalesceTool
{
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class CoalescedEditor : INotifyPropertyChanged
	{
		private string _destinationPath;
		private CoalescedType _destinationType;
		private string _sourcePath;
		private CoalescedType _sourceType;

		public CoalescedEditor()
		{
			InitializeComponent();

			DataContext = this;
		}

		public string DestinationPath
		{
			get { return _destinationPath; }
			set { SetProperty(ref _destinationPath, value); }
		}

		public CoalescedType DestinationType
		{
			get { return _destinationType; }
			set { SetProperty(ref _destinationType, value); }
		}

		public string SourcePath
		{
			get { return _sourcePath; }
			set { SetProperty(ref _sourcePath, value); }
		}

		public CoalescedType SourceType
		{
			get { return _sourceType; }
			set { SetProperty(ref _sourceType, value); }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void Browse_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var parameter = e.Parameter as string;

			if (parameter == null)
			{
				return;
			}

			//var isSource = (parameter.Equals("Source", StringComparison.OrdinalIgnoreCase));
			var isDestination = (parameter.Equals("Destination", StringComparison.OrdinalIgnoreCase));

			if (isDestination)
			{
				if (string.IsNullOrEmpty(SourcePath))
				{
					e.CanExecute = false;

					return;
				}
			}

			e.CanExecute = true;
		}

		private void Browse_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			var parameter = e.Parameter as string;

			if (parameter == null)
			{
				return;
			}

			var isSource = (parameter.Equals("Source", StringComparison.OrdinalIgnoreCase));
			var isDestination = (parameter.Equals("Destination", StringComparison.OrdinalIgnoreCase));

			if (isSource)
			{
				var dlg = new CommonOpenFileDialog("Open File");
				dlg.Filters.Add(new CommonFileDialogFilter("Coalesced Files", "*.bin;*.xml"));
				dlg.Filters.Add(new CommonFileDialogFilter("Binary Coalesced Files", "*.bin"));
				dlg.Filters.Add(new CommonFileDialogFilter("XML Coalesced Files", "*.xml"));

				if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
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
				}

				if (!string.IsNullOrEmpty(DestinationPath) && ChangeDestinationCheckBox.IsChecked == false)
				{
					return;
				}

				switch (SourceType)
				{
					case CoalescedType.Binary:
					{
						DestinationPath = Path.ChangeExtension(SourcePath, null);
						DestinationType = CoalescedType.Xml;

						break;
					}
					case CoalescedType.Xml:
					{
						DestinationPath = Path.ChangeExtension(SourcePath, "bin");
						DestinationType = CoalescedType.Binary;

						break;
					}
				}
			}
			else if (isDestination)
			{
				switch (SourceType)
				{
					case CoalescedType.Binary:
					{
						var dlg = new CommonOpenFileDialog("Select Folder")
						{
							IsFolderPicker = true
						};

						if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
						{
							return;
						}

						DestinationPath = dlg.FileName;
						//DestinationType = CoalescedType.Binary;

						break;
					}
					case CoalescedType.Xml:
					{
						var dlg = new CommonOpenFileDialog("Open File");
						dlg.Filters.Add(new CommonFileDialogFilter("Coalesced Files", "*.bin;*.xml"));
						dlg.Filters.Add(new CommonFileDialogFilter("Binary Coalesced Files", "*.bin"));
						dlg.Filters.Add(new CommonFileDialogFilter("XML Coalesced Files", "*.xml"));

						if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
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
		}

		private void ConvertTo_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SourcePath) || string.IsNullOrEmpty(DestinationPath))
			{
				e.CanExecute = false;

				return;
			}

			e.CanExecute = true;
		}

		private void ConvertTo_OnExecuted(object sender, ExecutedRoutedEventArgs e)
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

			switch (DestinationType)
			{
				case CoalescedType.Binary:
					if (!Directory.Exists(Path.GetDirectoryName(DestinationPath) ?? DestinationPath))
					{
						Directory.CreateDirectory(DestinationPath);
					}
                    Converter.ConvertToBin(SourcePath, DestinationPath);
					break;
				case CoalescedType.Xml:
					if (!Directory.Exists(DestinationPath))
					{
						Directory.CreateDirectory(DestinationPath);
					}
                    Converter.ConvertToXML(SourcePath, DestinationPath);
					break;
				default:
                    throw new ArgumentOutOfRangeException();
			}

            MessageBox.Show("Done");
		}

		private void Exit_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void Exit_OnExecuted(object sender, ExecutedRoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}
        
		protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
			{
				return false;
			}

			storage = value;

			if (propertyName != null)
			{
				OnPropertyChanged(propertyName);
			}

			return true;
		}
        
		protected void OnPropertyChanged(string propertyName)
		{
			var handler = PropertyChanged;

			handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
