using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows;
using Caliburn.Micro;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.MassEffect3.SFXGame.CodexMap;
using Gammtek.Conduit.MassEffect3.SFXGame.QuestMap;
using Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap;
using ME3LibWV;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace MassEffect.NativesEditor.ViewModels
{
	[Export(typeof (IShell))]
	public class ShellViewModel : PropertyChangedBase, IShell
	{
		private CodexMapViewModel _codexMapViewModel;
		private FindObjectUsagesViewModel _findObjectUsagesViewModel;
		private QuestMapViewModel _questMapViewModel;
		private StateEventMapViewModel _stateEventMapViewModel;
		private string _windowTitle;
		private PCCPackage _pccPackage;
		private string _fileName;

		public ShellViewModel()
		{
			CodexMapViewModel = new CodexMapViewModel();
			QuestMapViewModel = new QuestMapViewModel();
			StateEventMapViewModel = new StateEventMapViewModel();

			FindObjectUsagesViewModel = new FindObjectUsagesViewModel();

			WindowTitle = "Natives Editor";
		}

		public bool CanCreateNewFile
		{
			get { return false; }
		}

		public bool CanOpenFile
		{
			get { return true; }
		}

		public bool CanSaveFile
		{
			get { return PccPackage != null && !FileName.IsNullOrWhiteSpace(); }
		}

		public bool CanSaveFileAs
		{
			get { return false; }
		}

		public CodexMapViewModel CodexMapViewModel
		{
			get { return _codexMapViewModel; }
			set
			{
				if (Equals(value, _codexMapViewModel))
				{
					return;
				}

				_codexMapViewModel = value;

				NotifyOfPropertyChange(() => CodexMapViewModel);
			}
		}

		public string FileName
		{
			get { return _fileName; }
			set
			{
				if (value == _fileName)
				{
					return;
				}

				_fileName = value;

				NotifyOfPropertyChange(() => FileName);
				NotifyOfPropertyChange(() => CanSaveFile);
			}
		}

		public FindObjectUsagesViewModel FindObjectUsagesViewModel
		{
			get { return _findObjectUsagesViewModel; }
			set
			{
				if (Equals(value, _findObjectUsagesViewModel))
				{
					return;
				}

				_findObjectUsagesViewModel = value;

				NotifyOfPropertyChange(() => FindObjectUsagesViewModel);
			}
		}

		public PCCPackage PccPackage
		{
			get { return _pccPackage; }
			set
			{
				if (Equals(value, _pccPackage))
				{
					return;
				}

				_pccPackage = value;

				NotifyOfPropertyChange(() => PccPackage);
				NotifyOfPropertyChange(() => CanSaveFile);
			}
		}

		public QuestMapViewModel QuestMapViewModel
		{
			get { return _questMapViewModel; }
			set
			{
				if (Equals(value, _questMapViewModel))
				{
					return;
				}

				_questMapViewModel = value;

				NotifyOfPropertyChange(() => QuestMapViewModel);
			}
		}

		public StateEventMapViewModel StateEventMapViewModel
		{
			get { return _stateEventMapViewModel; }
			set
			{
				if (Equals(value, _stateEventMapViewModel))
				{
					return;
				}

				_stateEventMapViewModel = value;

				NotifyOfPropertyChange(() => StateEventMapViewModel);
			}
		}

		public string WindowTitle
		{
			get { return _windowTitle; }
			set
			{
				if (value == _windowTitle)
				{
					return;
				}

				_windowTitle = value;

				NotifyOfPropertyChange(() => WindowTitle);
			}
		}

		public void CreateNewFile() {}

		public void LoadStateEventMap(PCCPackage pcc) {}

		public void OpenFile()
		{
			/*var dlg = new CommonOpenFileDialog();
			dlg.Filters.Add(new CommonFileDialogFilter("PCC Files", "pcc"));
			dlg.Multiselect = false;
			dlg.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

			if (dlg.ShowDialog() == CommonFileDialogResult.Cancel)
			{
				return;
			}

			OpenPccFile(dlg.FileName);*/

			OpenPccFile(@"C:\Program Files (x86)\Origin Games\Mass Effect 3\BIOGame\CookedPCConsole\SFXGameInfoSP_SF.pcc");
			//OpenPccFile(@"_Test\SFXGameInfoSP_SF.pcc");
		}

		public void OpenPccFile(string path)
		{
			if (path == null)
			{
				throw new ArgumentNullException("path");
			}

			if (!File.Exists(path))
			{
				return;
			}

			FileName = path;
			PccPackage = new PCCPackage(path, closestream: true);

			if (CodexMapViewModel != null)
			{
				CodexMapViewModel.Open(PccPackage);
			}

			if (QuestMapViewModel != null)
			{
				QuestMapViewModel.Open(PccPackage);
			}

			if (StateEventMapViewModel != null)
			{
				StateEventMapViewModel.Open(PccPackage);
			}
		}

		public void SaveFile()
		{
			if (PccPackage == null)
			{
				return;
			}

			if (CodexMapViewModel != null)
			{
				int exportIndex;
				int dataOffset;

				if (CodexMapViewModel.TryFindCodexMap(PccPackage, out exportIndex, out dataOffset))
				{
					var mapData = PccPackage.Exports[exportIndex].Data;

					byte[] bytes;

					using (var stream = new MemoryStream())
					{
						var codexMap = CodexMapViewModel.ToCodexMap();
						var binaryCodexMap = new BinaryBioCodexMap(codexMap.Sections, codexMap.Pages);

						binaryCodexMap.Save(stream);

						bytes = stream.ToArray();
					}

					Array.Resize(ref mapData, dataOffset + bytes.Length);
					bytes.CopyTo(mapData, dataOffset);

					var temp = PccPackage.Exports[exportIndex];
					Array.Resize(ref temp.Data, mapData.Length);
					mapData.CopyTo(temp.Data, 0);
					PccPackage.Exports[exportIndex] = temp;
				}
			}

			if (QuestMapViewModel != null)
			{
				int exportIndex;
				int dataOffset;

				if (QuestMapViewModel.TryFindQuestMap(PccPackage, out exportIndex, out dataOffset))
				{
					var mapData = PccPackage.Exports[exportIndex].Data;

					byte[] bytes;

					using (var stream = new MemoryStream())
					{
						var questMap = QuestMapViewModel.ToQuestMap();
						var binaryQuestMap = new BinaryBioQuestMap(questMap.Quests, questMap.BoolTaskEvals, questMap.IntTaskEvals, questMap.FloatTaskEvals);

						binaryQuestMap.Save(stream);

						bytes = stream.ToArray();
					}

					Array.Resize(ref mapData, dataOffset + bytes.Length);
					bytes.CopyTo(mapData, dataOffset);

					var temp = PccPackage.Exports[exportIndex];
					Array.Resize(ref temp.Data, mapData.Length);
					mapData.CopyTo(temp.Data, 0);
					PccPackage.Exports[exportIndex] = temp;
				}
			}

			if (StateEventMapViewModel != null)
			{
				int exportIndex;
				int dataOffset;

				if (StateEventMapViewModel.TryFindStateEventMap(PccPackage, out exportIndex, out dataOffset))
				{
					var mapData = PccPackage.Exports[exportIndex].Data;

					byte[] bytes;

					using (var stream = new MemoryStream())
					{
						var stateEventMap = StateEventMapViewModel.ToStateEventMap();
						var binaryStateEventMap = new BinaryBioStateEventMap(stateEventMap.StateEvents);

						binaryStateEventMap.Save(stream);

						bytes = stream.ToArray();
					}

					Array.Resize(ref mapData, dataOffset + bytes.Length);
					bytes.CopyTo(mapData, dataOffset);

					var temp = PccPackage.Exports[exportIndex];
					Array.Resize(ref temp.Data, mapData.Length);
					mapData.CopyTo(temp.Data, 0);
					PccPackage.Exports[exportIndex] = temp;
				}
			}

			PccPackage.Save(FileName);
		}

		public void SaveFileAs()
		{
			var dlg = new CommonSaveFileDialog();
			dlg.Filters.Add(new CommonFileDialogFilter("PCC File", "pcc"));

			if (!FileName.IsNullOrWhiteSpace())
			{
				dlg.InitialDirectory = Path.GetDirectoryName(FileName);
				dlg.DefaultFileName = Path.GetFileName(FileName);
			}

			if (dlg.ShowDialog() != CommonFileDialogResult.Ok || dlg.FileName.IsNullOrWhiteSpace())
			{
				return;
			}

			var fileName = dlg.FileName;
		}

		public void ExitApplication()
		{
			Application.Current.MainWindow.Close();
		}
	}
}
