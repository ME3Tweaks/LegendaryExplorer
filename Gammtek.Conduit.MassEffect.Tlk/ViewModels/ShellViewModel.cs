using System.IO;
using System.Windows;
using Caliburn.Micro;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Gammtek.Conduit.MassEffect.Tlk.ViewModels
{
	public class ShellViewModel : PropertyChangedBase, IShell
	{
		public bool AutoChangeDestination { get; set; }

		public string DestinationPath { get; set; }

		public TlkType DestinationType { get; set; }

		public string SourcePath { get; set; }

		public TlkType SourceType { get; set; }

		public string Title => "Shell View";

		private bool ConvertTo_OnCanExecute()
		{
			return string.IsNullOrEmpty(SourcePath)
				   || string.IsNullOrEmpty(DestinationPath);
		}

		private void ConvertTo_OnExecuted()
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

		private bool DestinationBrowse_OnCanExecute()
		{
			return true;
		}

		private void DestinationBrowse_OnExecuted()
		{
			var dlg = new CommonOpenFileDialog("Open File");
			dlg.Filters.Add(new CommonFileDialogFilter("Talk Table Files", "*.tlk;*.xml"));
			dlg.Filters.Add(new CommonFileDialogFilter("Binary Talk Table Files", "*.tlk"));
			dlg.Filters.Add(new CommonFileDialogFilter("XML Talk Table Files", "*.xml"));

			if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
			{
				return;
			}

			DestinationPath = dlg.FileName;
			var sourceExtension = Path.GetExtension(DestinationPath) ?? "";

			switch (sourceExtension.ToLower())
			{
				case ".tlk":
				{
					DestinationType = TlkType.Binary;

					break;
				}
				case ".xml":
				{
					DestinationType = TlkType.Xml;

					break;
				}
			}
		}

		private bool Exit_OnCanExecute()
		{
			return true;
		}

		private void Exit_OnExecuted()
		{
			Application.Current.Shutdown();
		}

		private bool SourceBrowse_OnCanExecute()
		{
			return true;
		}

		private void SourceBrowse_OnExecuted()
		{
			var dlg = new CommonOpenFileDialog("Open File");
			dlg.Filters.Add(new CommonFileDialogFilter("Talk Table Files", "*.tlk;*.xml"));
			dlg.Filters.Add(new CommonFileDialogFilter("Binary Talk Table Files", "*.tlk"));
			dlg.Filters.Add(new CommonFileDialogFilter("XML Talk Table Files", "*.xml"));

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
					SourceType = TlkType.Binary;

					break;
				}
				case ".xml":
				{
					SourceType = TlkType.Xml;

					break;
				}
			}

			if ( /*!string.IsNullOrEmpty(DestinationPath) && */!AutoChangeDestination)
			{
				return;
			}

			switch (SourceType)
			{
				case TlkType.Binary:
				{
					DestinationPath = Path.ChangeExtension(SourcePath, "xml");
					DestinationType = TlkType.Xml;

					break;
				}
				case TlkType.Xml:
				{
					DestinationPath = Path.ChangeExtension(SourcePath, "tlk");
					DestinationType = TlkType.Binary;

					break;
				}
			}
		}
	}
}
