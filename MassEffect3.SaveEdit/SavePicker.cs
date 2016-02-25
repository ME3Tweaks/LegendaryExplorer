using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MassEffect3.SaveEdit.Properties;
using MassEffect3.SaveFormats;

namespace MassEffect3.SaveEdit
{
	public partial class SavePicker : Form
	{
		public enum PickerMode
		{
			Invalid,
			Load,
			Save,
		}

		public Dictionary<NotorietyType, string> NotorietyNames = new Dictionary<NotorietyType, string>
		{
			{ NotorietyType.None, Localization.PlayerNotoriety_None },
			{ NotorietyType.Ruthless, Localization.PlayerNotoriety_Ruthless },
			{ NotorietyType.Survivor, Localization.PlayerNotoriety_Survivor },
			{ NotorietyType.Warhero, Localization.PlayerNotoriety_Warhero }
		};

		public Dictionary<OriginType, string> OriginNames = new Dictionary<OriginType, string>
		{
			{ OriginType.None, Localization.PlayerOrigin_None },
			{ OriginType.Colony, Localization.PlayerOrigin_Colony },
			{ OriginType.Earthborn, Localization.PlayerOrigin_Earthborn },
			{ OriginType.Spacer, Localization.PlayerOrigin_Spacer }
		};

		private PickerMode _fileMode = PickerMode.Invalid;
		private int _highestSaveNumber;

		public SavePicker()
		{
			InitializeComponent();
			FileMode = PickerMode.Load;

			iconImageList.Images.Add("Unknown", new Bitmap(16, 16));

			careerListView.Items.Clear();
			careerListView.Items.Add(new ListViewItem
			{
				Text = Localization.SavePicker_NewCareerLabel,
				ImageKey = @"New",
			});

			saveListView.Items.Clear();
			saveListView.Items.Add(new ListViewItem
			{
				Text = Localization.SavePicker_NewSaveLabel,
				ImageKey = @"New",
			});

			/*var tuples = new Dictionary<PlayerClass, Tuple<ReadOnlyCollection<string>>>
			{
				{ PlayerClass.Adept, Tuple.Create(ClassFileNames.Adept) }
			};*/
		}

		public string FilePath { get; set; }
		public string SelectedPath { get; set; }

		public SFXSaveGameFile SaveFile { get; set; }

		public PickerMode FileMode
		{
			get { return _fileMode; }
			set
			{
				if (value == _fileMode)
				{
					return;
				}

				_fileMode = value;
				loadFileButton.Visible = value == PickerMode.Load;
				saveFileButton.Visible = value == PickerMode.Save;
			}
		}

		private void FindCareers()
		{
			careerListView.BeginUpdate();

			careerListView.Items.Clear();

			if (Directory.Exists(FilePath))
			{
				foreach (var careerPath in Directory
					.GetDirectories(FilePath)
					.OrderByDescending(Directory.GetLastWriteTime))
				{
					var careerFiles = Directory.EnumerateFiles(careerPath, "*.pcsav").ToList();

					if (!careerFiles.Any())
					{
						continue;
					}

					SFXSaveGameFile saveFile = null;

					foreach (var careerFile in careerFiles)
					{
						try
						{
							using (var input = File.OpenRead(careerFile))
							{
								saveFile = SFXSaveGameFile.Read(input);
							}

							break;
						}
						catch (Exception)
						{
							saveFile = null;
						}
					}

					// attempt to parse the directory name
					string name;
					OriginType originType;
					NotorietyType reputationType;
					string className;
					DateTime date;

					if (ParseCareerName(Path.GetFileName(careerPath),
						out name, out originType, out reputationType, out className, out date))
					{
						var classType = PlayerClass.Invalid;

						if (saveFile != null)
						{
							classType = GetPlayerClassFromStringId(saveFile.Player.ClassFriendlyName);
						}

						if (classType == PlayerClass.Invalid && className != null)
						{
							classType = GetPlayerClassFromLocalizedName(className);
						}

						var displayName = "";

						displayName += (saveFile == null ? name : saveFile.Player.FirstName) + "\n";
						displayName += string.Format("{0}, {1}", classType, date.ToString("d"));
						//displayName += date.ToString();

						careerListView.Items.Add(new ListViewItem
						{
							Text = displayName,
							ImageKey = @"Class_" + classType,
							Tag = careerPath,
							ToolTipText = string.Format("{0}\n{1}", OriginNames[originType], NotorietyNames[reputationType])
						});
					}
					else
					{
						careerListView.Items.Add(new ListViewItem
						{
							Text = Path.GetFileName(careerPath),
							ImageKey = @"Class_Unknown",
							Tag = careerPath,
						});
					}
				}
			}

			if (FileMode == PickerMode.Save)
			{
				if (careerListView.Items.Count > 0)
				{
					var item = new ListViewItem
					{
						Name = @"New Career",
						Text = Localization.SavePicker_NewCareerLabel,
						ImageKey = @"New",
					};

					careerListView.Items.Insert(1, item);
				}
				else
				{
					var item = new ListViewItem
					{
						Name = @"New Career",
						Text = Localization.SavePicker_NewCareerLabel,
						ImageKey = @"New",
					};

					careerListView.Items.Add(item);
				}
			}

			careerListView.EndUpdate();

			if (careerListView.Items.Count > 0)
			{
				careerListView.Items[0].Selected = true;
			}
			else
			{
				FindSaves(null);
			}
		}

		private void FindSaves(string savePath)
		{
			saveListView.BeginUpdate();

			saveListView.Items.Clear();

			if (FileMode == PickerMode.Save)
			{
				var item = new ListViewItem
				{
					Name = @"New Save",
					Text = Localization.SavePicker_NewSaveLabel,
					ImageKey = @"New",
				};

				saveListView.Items.Add(item);
			}

			_highestSaveNumber = 1;

			if (savePath != null)
			{
				if (Directory.Exists(FilePath))
				{
					foreach (var inputPath in Directory
						.GetFiles(savePath, "*.pcsav")
						.OrderByDescending(Directory.GetLastWriteTime))
					{
						var baseName = Path.GetFileNameWithoutExtension(inputPath);
						if (baseName != null &&
							baseName.StartsWith("Save_") &&
							baseName.Length == 9)
						{
							int saveNumber;

							if (int.TryParse(baseName.Substring(5).TrimStart('0'), out saveNumber))
							{
								_highestSaveNumber = Math.Max(saveNumber, _highestSaveNumber);
							}
						}

						try
						{
							using (var input = File.OpenRead(inputPath))
							{
								SFXSaveGameFile.Read(input);
							}
						}
						catch (Exception ex)
						{
							MessageBox.Show(ex.Message, Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
						}

						var item = new ListViewItem
						{
							Text = Path.GetFileName(inputPath),
							ImageKey = "",
							Tag = inputPath,
						};

						saveListView.Items.Add(item);
					}
				}
			}

			saveListView.EndUpdate();

			if (saveListView.Items.Count > 0)
			{
				saveListView.Items[0].Selected = true;
			}
		}

		private static string GetClassNameFromStringId(int id)
		{
			switch (id)
			{
				case 93954:
				{
					return "Adept";
				}
				case 93952:
				{
					return "Soldier";
				}
				case 93953:
				{
					return "Engineer";
				}
				case 93957:
				{
					return "Sentinel";
				}
				case 93955:
				{
					return "Infiltrator";
				}
				case 93956:
				{
					return "Vanguard";
				}
			}

			return "Unknown";
		}

		private static PlayerClass GetPlayerClassFromLocalizedName(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return PlayerClass.Invalid;
			}

			name = SanitizePath(name); // just in case
			name = name.ToLowerInvariant();

			/* due to how poorly Bioware's career naming code is implemented
             * this cannot be guaranteed to find a match, especially on
             * Russian versions of the game where the class names turn into
             * empty strings due to filtering. */

			if (ClassFileNames.Adept.Any(n => n == name))
			{
				return PlayerClass.Adept;
			}

			if (ClassFileNames.Soldier.Any(n => n == name))
			{
				return PlayerClass.Soldier;
			}

			if (ClassFileNames.Engineer.Any(n => n == name))
			{
				return PlayerClass.Engineer;
			}

			if (ClassFileNames.Sentinel.Any(n => n == name))
			{
				return PlayerClass.Sentinel;
			}

			if (ClassFileNames.Infiltrator.Any(n => n == name))
			{
				return PlayerClass.Infiltrator;
			}

			if (ClassFileNames.Vanguard.Any(n => n == name))
			{
				return PlayerClass.Vanguard;
			}

			return PlayerClass.Invalid;
		}

		private static PlayerClass GetPlayerClassFromStringId(int id)
		{
			switch (id)
			{
				case 93954:
				{
					return PlayerClass.Adept;
				}
				case 93952:
				{
					return PlayerClass.Soldier;
				}
				case 93953:
				{
					return PlayerClass.Engineer;
				}
				case 93957:
				{
					return PlayerClass.Sentinel;
				}
				case 93955:
				{
					return PlayerClass.Infiltrator;
				}
				case 93956:
				{
					return PlayerClass.Vanguard;
				}
			}

			return PlayerClass.Invalid;
		}

		private string GetSelectedPath(out bool exists)
		{
			if (saveListView.SelectedItems.Count > 0 &&
				(saveListView.SelectedItems[0].Tag is string))
			{
				exists = true;
				return (string) saveListView.SelectedItems[0].Tag;
			}

			exists = false;

			if (FileMode == PickerMode.Load)
			{
				return null;
			}

			if (careerListView.SelectedItems.Count == 0)
			{
				return null;
			}

			var path = FilePath;

			int saveNumber;

			if (careerListView.SelectedItems[0].Name == @"New Career")
			{
				SaveFile.Player.Guid = Guid.NewGuid();

				var firstName = SanitizePath(SaveFile.Player.FirstName);

				if (string.IsNullOrEmpty(firstName))
				{
					firstName = "Shep";
				}

				var className = SanitizePath(GetClassNameFromStringId(SaveFile.Player.ClassFriendlyName));

				var now = DateTime.Now;

				var stamp = 0;
				stamp += now.Hour;
				stamp *= 60;
				stamp += now.Minute;
				stamp *= 60;
				stamp += now.Second;
				stamp *= 1000;
				stamp += now.Millisecond;

				var name = string.Format("{0}_{1}{2}_{3}_{4:d2}{5:d2}{6:d2}_{7:x7}",
					firstName,
					(int) SaveFile.Player.Origin,
					(int) SaveFile.Player.Notoriety,
					className,
					DateTime.Now.Day,
					DateTime.Now.Month,
					DateTime.Now.Year % 100,
					stamp);

				path = Path.Combine(path, name);
				saveNumber = 1;
			}
			else if ((careerListView.SelectedItems[0].Tag is string) == false)
			{
				return null;
			}
			else
			{
				path = (string) careerListView.SelectedItems[0].Tag;
				saveNumber = _highestSaveNumber + 1;
			}

			path = Path.Combine(path, string.Format("Save_{0}.pcsav", saveNumber.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')));

			return path;
		}

		private void OnCancel(object sender, EventArgs e)
		{
			SelectedPath = null;
			DialogResult = DialogResult.Cancel;

			Close();
		}

		private void OnChooseSave(object sender, EventArgs e)
		{
			bool exists;
			var path = GetSelectedPath(out exists);

			if (path == null)
			{
				return;
			}

			if (FileMode == PickerMode.Save && exists)
			{
				if (MessageBox.Show(Localization.SavePicker_SaveOverwriteConfirmation,
					Localization.Question, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				{
					return;
				}
			}

			SelectedPath = path;
			DialogResult = DialogResult.OK;

			Close();
		}

		private void OnDeleteCareer(object sender, EventArgs e)
		{
			if (careerListView.SelectedItems.Count == 0 || (careerListView.SelectedItems[0].Tag is string) == false)
			{
				return;
			}

			if (MessageBox.Show(Localization.SavePicker_DeleteCareerConfirmation,
				Localization.Warning, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
			{
				return;
			}

			careerListView.BeginUpdate();
			var item = careerListView.SelectedItems[0];
			careerListView.Items.Remove(item);

			var basePath = (string) item.Tag;
			var savePaths = Directory.GetFiles(basePath, "*.pcsav");

			if (savePaths.Length > 0)
			{
				foreach (var savePath in savePaths)
				{
					File.Delete(savePath);
				}

				try
				{
					Directory.Delete(basePath);
				}
				catch (IOException ex)
				{
					MessageBox.Show(string.Format(Localization.SavePicker_DeleteCareerIOException, ex.Message),
						Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}

			careerListView.EndUpdate();

			FindCareers();
		}

		private void OnDeleteSave(object sender, EventArgs e)
		{
			if (saveListView.SelectedItems.Count == 0 ||
				(saveListView.SelectedItems[0].Tag is string) == false)
			{
				return;
			}

			if (MessageBox.Show(Localization.SavePicker_DeleteSaveConfirmation,
				Localization.Warning, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
			{
				return;
			}

			saveListView.BeginUpdate();
			var item = saveListView.SelectedItems[0];
			saveListView.Items.Remove(item);

			var savePath = (string) item.Tag;

			try
			{
				File.Delete(savePath);
			}
			catch (IOException ex)
			{
				MessageBox.Show(string.Format(Localization.SavePicker_DeleteSaveIOException, ex.Message), 
					Localization.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			saveListView.EndUpdate();
		}

		private void OnRefresh(object sender, EventArgs e)
		{
			FindCareers();
		}

		private void OnSaveActivate(object sender, EventArgs e)
		{
			OnChooseSave(sender, e);
		}

		private void OnSelectCareer(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			loadFileButton.Enabled = false;
			saveFileButton.Enabled = false;
			deleteSaveButton.Enabled = false;

			if (e.IsSelected)
			{
				FindSaves(e.Item.Tag as string);

				deleteCareerButton.Enabled = e.Item.Tag is string;
			}
			else
			{
				deleteCareerButton.Enabled = false;

				FindSaves(null);
			}
		}

		private void OnSelectSave(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			loadFileButton.Enabled = false;
			saveFileButton.Enabled = false;
			deleteSaveButton.Enabled = false;

			if (!e.IsSelected)
			{
				return;
			}

			if (e.Item.Name != @"New Save" && !(e.Item.Tag is string))
			{
				return;
			}

			loadFileButton.Enabled = true;
			saveFileButton.Enabled = true;
			deleteSaveButton.Enabled = e.Item.Tag is string;
		}

		private void OnShown(object sender, EventArgs e)
		{
			loadFileButton.Enabled = false;
			saveFileButton.Enabled = false;

			FindCareers();
		}

		private static bool ParseCareerName(string input, out string name, out OriginType originType,
			out NotorietyType reputationType, out string className, out DateTime date)
		{
			name = null;
			className = null;
			originType = OriginType.None;
			reputationType = NotorietyType.None;
			date = DateTime.Now;

			var parts = input.Split('_');
			if (parts.Length != 5)
			{
				return false;
			}

			name = parts[0];

			if (parts[1] == null ||
				parts[1].Length != 2)
			{
				return false;
			}

			switch (parts[1][0])
			{
				case '0':
				{
					originType = OriginType.None;
					break;
				}
				case '1':
				{
					originType = OriginType.Spacer;
					break;
				}
				case '2':
				{
					originType = OriginType.Colony;
					break;
				}
				case '3':
				{
					originType = OriginType.Earthborn;
					break;
				}
				default:
				{
					return false;
				}
			}

			switch (parts[1][1])
			{
				case '0':
				{
					reputationType = NotorietyType.None;
					break;
				}
				case '1':
				{
					reputationType = NotorietyType.Survivor;
					break;
				}
				case '2':
				{
					reputationType = NotorietyType.Warhero;
					break;
				}
				case '3':
				{
					reputationType = NotorietyType.Ruthless;
					break;
				}
				default:
				{
					return false;
				}
			}

			if (parts[2] == null)
			{
				return false;
			}

			className = parts[2];

			if (parts[3] == null || parts[3].Length != 6)
			{
				return false;
			}

			if (parts[4] == null || parts[4].Length != 7)
			{
				return false;
			}

			// Day
			int day;

			if (int.TryParse(parts[3].Substring(0, 2), out day) == false)
			{
				return false;
			}

			// Month
			int month;

			if (int.TryParse(parts[3].Substring(2, 2), out month) == false)
			{
				return false;
			}

			// Year
			int year;

			if (int.TryParse(parts[3].Substring(4, 2), out year) == false)
			{
				return false;
			}

			date = new DateTime(2000 + year, month, day);

			return true;
		}

		private static char Sanitize(char c /*, bool extended = true*/)
		{
			if ((c >= 'A' && c <= 'Z') ||
				(c >= 'a' && c <= 'z') ||
				(c >= '0' && c <= '9'))
			{
				return c;
			}

			return '\0';

			/* disabled because it's broken in ME3 and doesn't actually
             * sanitize these characters at all
             */

			/*
            if (extended == false)
            {
                return '\0';
            }

            var remaps = new Dictionary<string, char>()
            {
                {"ÀÁÂÃÄÅ", 'A'},
                {"àáâãäå", 'a'},
                {"Ç", 'C'},
                {"ç", 'c'},
                {"ÈÉÊË", 'E'},
                {"èéêë", 'e'},
                {"ÌÍÎÏ", 'I'},
                {"ìíîï", 'i'},
                {"Ñ", 'N'},
                {"ñ", 'n'},
                {"ÐÒÓÔÕÖ", 'O'},
                {"ðòóôõö", 'o'},
                {"Þ", 'P'},
                {"þ", 'p'},
                {"ÙÚÛÜ", 'U'},
                {"ùúûü", 'u'},
                {"Ý", 'Y'},
                {"ýÿ", 'y'},
            };

            return remaps.FirstOrDefault(r => r.Key.IndexOf(c) >= 0).Value;
            */
		}

		private static string SanitizePath(string path)
		{
			return path.Select(Sanitize)
				.Where(d => d != '\0')
				.Aggregate("", (c, d) => c + d);
		}

		private static class ClassFileNames
		{
			// 93954
			public static readonly ReadOnlyCollection<string> Adept;

			// 93953
			public static readonly ReadOnlyCollection<string> Engineer;

			// 93955
			public static readonly ReadOnlyCollection<string> Infiltrator;

			// 93957
			public static readonly ReadOnlyCollection<string> Sentinel;

			// 93952
			public static readonly ReadOnlyCollection<string> Soldier;

			// 93956
			public static readonly ReadOnlyCollection<string> Vanguard;

			static ClassFileNames()
			{
				// 93954
				Adept = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Adept", // INT
								"Adepto", // ESN
								"Adepte", // FRA
								"Adepto", // ITA
								"Experte", // DEU
								"Adept", // POL
								"Адепт" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));

				// 93953
				Engineer = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Engineer", // INT
								"Ingeniero", // ESN
								"Ingénieur", // FRA
								"Ingegnere", // ITA
								"Techniker", // DEU
								"Inżynier", // POL
								"Инженер" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));

				// 93955
				Infiltrator = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Infiltrator", // INT
								"Infiltrado", // ESN
								"Franc-tireur", // FRA
								"Incursore", // ITA
								"Infiltrator", // DEU
								"Szpieg", // POL
								"Разведчик" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));

				// 93957
				Sentinel = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Sentinel", // INT
								"Centinela", // ESN
								"Sentinelle", // FRA
								"Sentinella", // ITA
								"Wächter", // DEU
								"Strażnik", // POL
								"Страж" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));

				// 93952
				Soldier = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Soldier", // INT
								"Soldado", // ESN
								"Soldat", // FRA
								"Soldato", // ITA
								"Soldat", // DEU
								"Żołnierz", // POL
								"Солдат" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));

				// 93956
				Vanguard = new ReadOnlyCollection<string>(
						(IList<string>)
							new[]
							{
								"Vanguard", // INT
								"Vanguardia", // ESN
								"Porte-étendard", // FRA
								"Ricognitore", // ITA
								"Frontkämpfer", // DEU
								"Szturmowiec", // POL
								"Штурмовик" // RUS
							}
							.Select(s => SanitizePath(s).ToLowerInvariant())
							.Where(s => string.IsNullOrEmpty(s) == false));
			}
		}

		private enum PlayerClass
		{
			Invalid,
			Adept,
			Soldier,
			Engineer,
			Sentinel,
			Infiltrator,
			Vanguard,
		}
	}
}
