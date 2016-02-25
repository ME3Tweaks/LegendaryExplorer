using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using Caliburn.Micro;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Gammtek.Conduit.MassEffect.Coalesce.ViewModels
{
	[Export(typeof (IShell))]
	public class ShellViewModel : PropertyChangedBase, IShell
	{
		private bool _autoChangeDestination;
		private string _destinationPath;
		private CoalescedType _destinationType;
		private string _sourcePath;
		private CoalescedType _sourceType;

		public ShellViewModel()
		{
			AutoChangeDestination = true;
			DestinationType = CoalescedType.None;
		}

		public bool AutoChangeDestination
		{
			get { return _autoChangeDestination; }
			set
			{
				if (value == _autoChangeDestination)
				{
					return;
				}

				_autoChangeDestination = value;

				NotifyOfPropertyChange(() => AutoChangeDestination);
			}
		}

		public bool CanBrowseDestination => true;

		public bool CanBrowseSource => true;

		public bool CanConvertTo => !string.IsNullOrWhiteSpace(SourcePath) && !string.IsNullOrWhiteSpace(DestinationPath);

		public bool CanExit => true;

		public string DestinationPath
		{
			get { return _destinationPath; }
			set
			{
				if (value == _destinationPath)
				{
					return;
				}

				_destinationPath = value;

				NotifyOfPropertyChange(() => DestinationPath);
				NotifyOfPropertyChange(() => CanConvertTo);
			}
		}

		public CoalescedType DestinationType
		{
			get { return _destinationType; }
			set
			{
				if (value == _destinationType)
				{
					return;
				}

				_destinationType = value;

				NotifyOfPropertyChange(() => DestinationType);
			}
		}

		public string SourcePath
		{
			get { return _sourcePath; }
			set
			{
				if (value == _sourcePath)
				{
					return;
				}

				_sourcePath = value;

				NotifyOfPropertyChange(() => SourcePath);
				NotifyOfPropertyChange(() => CanConvertTo);
			}
		}

		public CoalescedType SourceType
		{
			get { return _sourceType; }
			set
			{
				if (value == _sourceType)
				{
					return;
				}

				_sourceType = value;

				NotifyOfPropertyChange(() => SourceType);
			}
		}

		public void BrowseDestination()
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
			var sourceExtension = Path.GetExtension(DestinationPath) ?? "";

			switch (sourceExtension.ToLower())
			{
				case ".bin":
				{
					DestinationType = CoalescedType.Binary;

					break;
				}
				case ".xml":
				{
					DestinationType = CoalescedType.Xml;

					break;
				}
				default:
				{
					DestinationType = CoalescedType.None;
					
					break;
				}
			}
		}

		public void BrowseSource()
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
				case ".tlk":
				{
					SourceType = CoalescedType.Binary;

					break;
				}
				case ".xml":
				{
					SourceType = CoalescedType.Xml;

					break;
				}
				default:
				{
					SourceType = CoalescedType.None;

					break;
				}
			}

			if ( /*!string.IsNullOrEmpty(DestinationPath) && */!AutoChangeDestination)
			{
				return;
			}

			switch (SourceType)
			{
				case CoalescedType.Binary:
				{
					DestinationPath = Path.ChangeExtension(SourcePath, "xml");
					DestinationType = CoalescedType.Xml;

					break;
				}
				case CoalescedType.Xml:
				{
					DestinationPath = Path.ChangeExtension(SourcePath, "bin");
					DestinationType = CoalescedType.Binary;

					break;
				}
				default:
				{
					DestinationType = CoalescedType.None;
					return;
				}
			}
		}

		public void ConvertTo()
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
		}

		public void Exit()
		{
			Application.Current.Shutdown();
		}
	}
}
