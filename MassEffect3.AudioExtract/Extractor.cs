using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MassEffect3.AudioExtract
{
	public partial class Extractor : Form
	{
		private readonly List<WwiseLocation> _Index = new List<WwiseLocation>();
		private bool _BatchCheckUpdate;
		private string _ConverterPath;
		private CancellationTokenSource _ExtractCancellationToken;
		private string _PackagePath;
		private string _RevorbPath;

		public Extractor()
		{
			InitializeComponent();
			DoubleBuffered = true;
		}

		private static string GetExecutablePath()
		{
			return Path.GetDirectoryName(Application.ExecutablePath);
		}

		private void OnLoad(object sender, EventArgs e)
		{
			string exePath = GetExecutablePath();

			string path =
				(string) Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\Mass Effect 3", "Install Dir", null) ??
				(string)
					Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\BioWare\Mass Effect 3", "Install Dir", null);

			if (path != null)
			{
				path = Path.Combine(path, "BioGame");
				path = Path.Combine(path, "CookedPCConsole");
				_PackagePath = path;
			}
			else
			{
				_PackagePath = null;
			}

			string converterPath = Path.Combine(exePath, "ww2ogg.exe");
			if (File.Exists(converterPath) == false)
			{
				convertCheckBox.Checked = false;
				convertCheckBox.Enabled = false;
				LogError("ww2ogg.exe is not present in \"{0}\"!", exePath);
			}
			else
			{
				_ConverterPath = converterPath;
			}

			string revorbPath = Path.Combine(exePath, "revorb.exe");
			if (File.Exists(revorbPath) == false)
			{
				revorbCheckBox.Checked = false;
				revorbCheckBox.Enabled = false;
			}
			else
			{
				_RevorbPath = revorbPath;
			}

			ToggleControls(false);

			string indexPath = Path.Combine(exePath, "Wwise.idx");
			if (File.Exists(indexPath) == false)
			{
				LogError("Wwise.idx is not present in \"{0}\"!", exePath);
			}
			else
			{
				LogMessage("Loading Wwise index...");

				TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

				DateTime startTime = DateTime.Now;

				Task<List<WwiseLocation>> task = Task<List<WwiseLocation>>.Factory.StartNew(
					() =>
					{
						using (FileStream input = File.OpenRead(indexPath))
						{
							WwiseIndex index = WwiseIndex.Load(input);
							if (input.Position != input.Length)
							{
								throw new FormatException("did not consume entire file");
							}

							var locations = new List<WwiseLocation>();
							foreach (var resource in index.Resources)
							{
								WwiseIndex.Instance firstInstance = resource.Instances
									.OrderByDescending(i => i.IsPackage == false)
									.First();

								var location = new WwiseLocation
								{
									Hash = resource.Hash,
									Path = index.Strings[firstInstance.PathIndex],
									Name = index.Strings[firstInstance.NameIndex],
									Actor = index.Strings[firstInstance.ActorIndex],
									Group = index.Strings[firstInstance.GroupIndex],
									Locale = index.Strings[firstInstance.LocaleIndex],
									File = index.Strings[firstInstance.FileIndex],
									IsPackage = firstInstance.IsPackage,
									Offset = firstInstance.Offset,
									Size = firstInstance.Size,
								};

								foreach (var instance in resource.Instances.Except(new[]
								{
									firstInstance
								}))
								{
									location.Duplicates.Add(new WwiseLocation
									{
										Hash = resource.Hash,
										Path = index.Strings[instance.PathIndex],
										Name = index.Strings[instance.NameIndex],
										Actor = index.Strings[instance.ActorIndex],
										Group = index.Strings[instance.GroupIndex],
										Locale = index.Strings[instance.LocaleIndex],
										File = index.Strings[instance.FileIndex],
										IsPackage = instance.IsPackage,
										Offset = instance.Offset,
										Size = instance.Size,
									});
								}

								locations.Add(location);
							}

							return locations;
						}
					});

				task.ContinueWith(
					t =>
					{
						TimeSpan elapsed = DateTime.Now.Subtract(startTime);
						LogSuccess("Loaded Wwise index in {0}m {1}s {2}ms",
							elapsed.Minutes,
							elapsed.Seconds,
							elapsed.Milliseconds);
						LogMessage("{0} entries ({1} duplicates)",
							t.Result.Count,
							t.Result.Sum(i => i.Duplicates.Count));
						OnWwiseIndexLoaded(t.Result);
					},
					CancellationToken.None,
					TaskContinuationOptions.OnlyOnRanToCompletion,
					uiScheduler);

				task.ContinueWith(
					t =>
					{
						LogError("Failed to load Wwise index.");
						if (t.Exception != null)
						{
							if (t.Exception.InnerException != null)
							{
								LogError(t.Exception.InnerException.ToString());
							}
							else
							{
								LogError(t.Exception.ToString());
							}
						}
					},
					CancellationToken.None,
					TaskContinuationOptions.OnlyOnFaulted,
					uiScheduler);
			}
		}

		private static string FixFilename(string name, bool isPackage)
		{
			return
				name +
				(isPackage == false ? ".afc" : ".pcc");
		}

		private void OnWwiseIndexLoaded(IEnumerable<WwiseLocation> index)
		{
			_Index.Clear();
			_Index.AddRange(index);

			fileListView.BeginUpdate();
			fileListView.Items.Clear();
			fileListView.EndUpdate();

			containerListBox.BeginUpdate();
			containerListBox.Items.Clear();

			containerListBox.Items.Add(new FilterFile
			{
				Name = "(any file)",
				Value = null,
				IsPackage = false,
			});

			foreach (var kv in _Index
				.Select(i => new KeyValuePair<string, bool>(
					i.File, i.IsPackage)).Distinct()
				.OrderBy(kv => kv.Key))
			{
				containerListBox.Items.Add(new FilterFile
				{
					Name = FixFilename(kv.Key, kv.Value),
					Value = kv.Key,
					IsPackage = kv.Value,
				});
			}

			containerListBox.SelectedIndex = 0;
			containerListBox.EndUpdate();

			actorComboBox.BeginUpdate();
			actorComboBox.Items.Clear();

			actorComboBox.Items.Add(new FilterItem
			{
				Name = "(any actor)",
				Value = null,
			});

			actorComboBox.Items.Add(new FilterItem
			{
				Name = "(no actor)",
				Value = "",
			});

			foreach (var actor in _Index
				.Where(i => string.IsNullOrEmpty(i.Actor) == false)
				.Select(i => i.Actor)
				.Distinct()
				.OrderBy(a => a))
			{
				actorComboBox.Items.Add(new FilterItem
				{
					Name = actor,
					Value = actor,
				});
			}

			actorComboBox.SelectedIndex = 0;
			actorComboBox.EndUpdate();

			groupComboBox.BeginUpdate();
			groupComboBox.Items.Clear();

			groupComboBox.Items.Add(new FilterItem
			{
				Name = "(any group)",
				Value = null,
			});

			groupComboBox.Items.Add(new FilterItem
			{
				Name = "(no group)",
				Value = "",
			});

			foreach (var group in _Index
				.Where(i => string.IsNullOrEmpty(i.Group) == false)
				.Select(i => i.Group)
				.Distinct()
				.OrderBy(l => l))
			{
				groupComboBox.Items.Add(new FilterItem
				{
					Name = group,
					Value = group,
				});
			}

			groupComboBox.SelectedIndex = 0;
			groupComboBox.EndUpdate();

			localeComboBox.BeginUpdate();
			localeComboBox.Items.Clear();

			localeComboBox.Items.Add(new FilterItem
			{
				Name = "(any locale)",
				Value = null,
			});

			localeComboBox.Items.Add(new FilterItem
			{
				Name = "(no locale)",
				Value = "",
			});

			foreach (var locale in _Index
				.Where(i => string.IsNullOrEmpty(i.Locale) == false)
				.Select(i => i.Locale)
				.Distinct()
				.OrderBy(l => l))
			{
				localeComboBox.Items.Add(new FilterItem
				{
					Name = locale,
					Value = locale,
				});
			}

			localeComboBox.SelectedIndex = 0;
			localeComboBox.EndUpdate();

			UpdateTotals();
		}

		private IEnumerable<WwiseLocation> FilterInstances(IEnumerable<WwiseLocation> instances)
		{
			var containerFilter = (FilterFile) containerListBox.SelectedItem;
			var actorFilter = (FilterItem) actorComboBox.SelectedItem;
			var unknownFilter = (FilterItem) groupComboBox.SelectedItem;
			var localeFilter = (FilterItem) localeComboBox.SelectedItem;

			if (containerFilter != null &&
				containerFilter.Value != null)
			{
				instances = instances.Where(
					i => (i.File == containerFilter.Value &&
						i.IsPackage == containerFilter.IsPackage) ||
						i.Duplicates.Any(
							j => j.File == containerFilter.Value && j.IsPackage == containerFilter.IsPackage));
			}

			if (actorFilter != null &&
				actorFilter.Value != null)
			{
				instances = instances.Where(
					i => i.Actor == actorFilter.Value || i.Duplicates.Any(f => f.Actor == actorFilter.Value));
			}

			if (unknownFilter != null &&
				unknownFilter.Value != null)
			{
				instances = instances.Where(
					i => i.Group == unknownFilter.Value || i.Duplicates.Any(f => f.Group == unknownFilter.Value));
			}

			if (localeFilter != null &&
				localeFilter.Value != null)
			{
				instances = instances.Where(
					i => i.Locale == localeFilter.Value || i.Duplicates.Any(f => f.Locale == localeFilter.Value));
			}

			return instances;
		}

		private void OnFilter(object sender, EventArgs e)
		{
			var containerFilter = (FilterFile) containerListBox.SelectedItem;
			var actorFilter = (FilterItem) actorComboBox.SelectedItem;
			var unknownFilter = (FilterItem) groupComboBox.SelectedItem;
			var localeFilter = (FilterItem) localeComboBox.SelectedItem;

			if ((containerFilter == null || containerFilter.Value == null) &&
				actorFilter.Value == null &&
				unknownFilter.Value == null &&
				localeFilter.Value == null)
			{
				if (
					MessageBox.Show("Are you sure you want to list every file?",
						"Warning",
						MessageBoxButtons.YesNo,
						MessageBoxIcon.Warning) != DialogResult.Yes)
				{
					return;
				}
			}

			fileListView.BeginUpdate();
			fileListView.Items.Clear();
			foreach (var instance in FilterInstances(_Index).OrderBy(i => i.Name))
			{
				var item = new ListViewItem(instance.Name)
				{
					Checked = instance.Selected,
					Tag = instance,
				};
				item.SubItems.Add(NativeHelper.FormatByteSize(instance.Size));
				fileListView.Items.Add(item);
			}
			fileListView.EndUpdate();

			LogMessage("Found {0} files.", fileListView.Items.Count);
		}

		private void UpdateTotals()
		{
			long totalSize = 0;
			long totalCount = 0;

			foreach (var location in _Index.Where(l => l.Selected))
			{
				totalCount++;
				totalSize += location.Size;
			}

			totalSizeLabel.Text = string.Format(
				"Selected {0} files, {1}",
				totalCount,
				NativeHelper.FormatByteSize(totalSize));
		}

		private void UpdateFileChecks()
		{
			fileListView.BeginUpdate();
			_BatchCheckUpdate = true;
			foreach (ListViewItem item in fileListView.Items)
			{
				var location = (WwiseLocation) item.Tag;
				if (location != null &&
					item.Checked != location.Selected)
				{
					item.Checked = location.Selected;
				}
			}
			_BatchCheckUpdate = false;
			fileListView.EndUpdate();
		}

		private void OnSelectNone(object sender, EventArgs e)
		{
			_Index.ForEach(l => l.Selected = false);
			UpdateFileChecks();
			UpdateTotals();
		}

		private void OnSelectAll(object sender, EventArgs e)
		{
			_Index.ForEach(l => l.Selected = true);
			UpdateFileChecks();
			UpdateTotals();
		}

		private void OnSelectVisible(object sender, EventArgs e)
		{
			fileListView.BeginUpdate();
			foreach (ListViewItem item in fileListView.Items)
			{
				var location = (WwiseLocation) item.Tag;
				if (location != null)
				{
					location.Selected = true;
				}
			}
			fileListView.EndUpdate();
			UpdateFileChecks();
			UpdateTotals();
		}

		private void OnSelectSearch(object sender, EventArgs e)
		{
			var search = new SearchBox
			{
				Owner = this,
				InputText = "mus"
			};

			if (search.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			string text = search.InputText.Trim().ToLowerInvariant();

			fileListView.BeginUpdate();
			fileListView.Items.Clear();
			_BatchCheckUpdate = true;
			foreach (var instance in _Index.Where(
				l =>
					(l.Path + "." + l.Name).Contains(text) ||
					l.Duplicates.Any(m => (m.Path + "." + m.Name).Contains(text))))
			{
				instance.Selected = true;

				var item = new ListViewItem(instance.Name)
				{
					Checked = instance.Selected,
					Tag = instance,
				};
				item.SubItems.Add(NativeHelper.FormatByteSize(instance.Size));
				fileListView.Items.Add(item);
			}
			_BatchCheckUpdate = false;
			fileListView.EndUpdate();

			containerListBox.SelectedItem = null;
			UpdateFileChecks();
			UpdateTotals();

			LogMessage("Found {0} files.", fileListView.Items.Count);
		}

		private void OnFileSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			duplicatesTextBox.Clear();

			var location = (WwiseLocation) e.Item.Tag;
			var sb = new StringBuilder();
			if (location != null)
			{
				sb.Append(location.Path + "." + location.Name + Environment.NewLine);
				foreach (var dupe in location.Duplicates)
				{
					sb.Append(dupe.Path + "." + dupe.Name + Environment.NewLine);
				}
			}

			duplicatesTextBox.Text = sb.ToString();
			duplicatesTextBox.SelectionStart = 0;
		}

		private void OnFileChecked(object sender, ItemCheckEventArgs e)
		{
			if (_BatchCheckUpdate)
			{
				return;
			}

			var location = (WwiseLocation) fileListView.Items[e.Index].Tag;
			if (location != null)
			{
				location.Selected = e.NewValue == CheckState.Checked;
				UpdateTotals();
			}
		}

		private static Stream ReadPackage(Stream input)
		{
			uint magic = input.ReadValueU32(Endian.Little);
			if (magic != 0x9E2A83C1 &&
				magic.Swap() != 0x9E2A83C1)
			{
				throw new FormatException("not a package");
			}
			Endian endian = magic == 0x9E2A83C1
				? Endian.Little
				: Endian.Big;

			ushort versionLo = input.ReadValueU16(endian);
			ushort versionHi = input.ReadValueU16(endian);

			if (versionLo != 684 &&
				versionHi != 194)
			{
				throw new FormatException("unsupported version");
			}

			input.Seek(4, SeekOrigin.Current);

			int folderNameLength = input.ReadValueS32(endian);
			int folderNameByteLength =
				folderNameLength >= 0 ? folderNameLength : (-folderNameLength * 2);
			input.Seek(folderNameByteLength, SeekOrigin.Current);

			/*var packageFlagsOffset = input.Position;*/
			uint packageFlags = input.ReadValueU32(endian);

			if ((packageFlags & 8) != 0)
			{
				input.Seek(4, SeekOrigin.Current);
			}

			input.Seek(24, SeekOrigin.Current);

			if ((packageFlags & 0x02000000) == 0)
			{
				return input;
			}

			input.Seek(36, SeekOrigin.Current);

			uint generationsCount = input.ReadValueU32(endian);
			input.Seek(generationsCount * 12, SeekOrigin.Current);

			input.Seek(20, SeekOrigin.Current);

			uint blockCount = input.ReadValueU32(endian);

			var blockStream = new BlockStream(input);
			for (int i = 0; i < blockCount; i++)
			{
				uint uncompressedOffset = input.ReadValueU32(endian);
				uint uncompressedSize = input.ReadValueU32(endian);
				uint compressedOffset = input.ReadValueU32(endian);
				uint compressedSize = input.ReadValueU32(endian);
				blockStream.AddBlock(
					uncompressedOffset,
					uncompressedSize,
					compressedOffset,
					compressedSize);
			}

			return blockStream;
		}

		private void OnStart(object sender, EventArgs e)
		{
			List<WwiseLocation> locations = _Index.Where(l => l.Selected).ToList();
			if (locations.Count == 0)
			{
				LogError("No files selected.");
				return;
			}

			progressBar1.Minimum = 0;
			progressBar1.Maximum = locations.Count;
			progressBar1.Value = 0;

			if (saveFolderBrowserDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}

			string basePath = saveFolderBrowserDialog.SelectedPath;

			if (Directory.GetFiles(basePath).Length > 0 ||
				Directory.GetDirectories(basePath).Length > 0)
			{
				if (MessageBox.Show(
					this,
					"Folder is not empty, continue anyway?",
					"Question",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question) == DialogResult.No)
				{
					return;
				}
			}

			var paths = new Dictionary<KeyValuePair<string, bool>, string>();

			foreach (var kv in locations.Select(l => new KeyValuePair<string, bool>(l.File, l.IsPackage)).Distinct())
			{
				string inputName = FixFilename(kv.Key, kv.Value);
				string inputPath = null;

				if (_PackagePath != null)
				{
					inputPath = Path.Combine(_PackagePath, inputName);
					if (File.Exists(inputPath) == false)
					{
						inputPath = null;
					}
				}

				if (inputPath == null)
				{
					openContainerFileDialog.Title = "Open " + inputName;
					openContainerFileDialog.Filter = inputName + "|" + inputName;

					if (openContainerFileDialog.ShowDialog() != DialogResult.OK)
					{
						LogError("Could not find \"{0}\"!", inputName);
					}
					else
					{
						inputPath = openContainerFileDialog.FileName;
					}
				}

				paths.Add(kv, inputPath);
			}

			ToggleControls(true);

			TaskScheduler uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

			DateTime startTime = DateTime.Now;

			string pcbPath = Path.Combine(Path.GetDirectoryName(_ConverterPath), "packed_codebooks.bin");

			bool validating = validateCheckBox.Checked;
			bool converting = convertCheckBox.Checked;
			bool revorbing = revorbCheckBox.Checked;

			_ExtractCancellationToken = new CancellationTokenSource();
			CancellationToken token = _ExtractCancellationToken.Token;
			Task task = Task.Factory.StartNew(
				() =>
				{
					int succeeded, failed;

					var streams = new Dictionary<KeyValuePair<string, bool>, Stream>();
					var dupeNames = new Dictionary<string, int>();
					try
					{
						int current;
						succeeded = failed = current = 0;

						foreach (var location in locations)
						{
							if (token.IsCancellationRequested)
							{
								LogWarning("Extraction cancelled.");
								break;
							}

							current++;
							SetProgress(current);

							var source = new KeyValuePair<string, bool>(location.File, location.IsPackage);
							if (paths.ContainsKey(source) == false)
							{
								failed++;
								continue;
							}

							Stream input;
							if (streams.ContainsKey(source) == false)
							{
								if (source.Value == false)
								{
									input = File.OpenRead(paths[source]);
									streams[source] = input;
								}
								else
								{
									input = File.OpenRead(paths[source]);
									input = ReadPackage(input);
								}
							}
							else
							{
								input = streams[source];
							}

							if (validating)
							{
								input.Seek(location.Offset, SeekOrigin.Begin);
								byte[] bytes = input.ReadBytes(location.Size);
								WwiseIndex.FileHash hash = WwiseIndex.FileHash.Compute(bytes);

								if (hash != location.Hash)
								{
									failed++;

									LogError(
										"Hash mismatch for \"{0}.{1}\"! ({2} vs {3})",
										location.File,
										location.Name,
										location.Hash,
										hash);

									continue;
								}
							}

							string name = location.Name;
							if (name.EndsWith("_wav"))
							{
								name = name.Substring(0, name.Length - 4);
							}


							string outputPath = Path.Combine(basePath, location.File, location.Path + "." + name);

							int dupeCounter;
							if (dupeNames.TryGetValue(outputPath, out dupeCounter) == false)
							{
								dupeCounter = 1;
							}

							if (dupeCounter > 1)
							{
								outputPath += string.Format(" [#{0}]", dupeCounter);
							}

							dupeCounter++;
							dupeNames[outputPath] = dupeCounter;

							string oggPath = outputPath + ".ogg";
							string riffPath = outputPath + ".riff";

							Directory.CreateDirectory(Path.GetDirectoryName(riffPath));

							using (FileStream output = File.Create(riffPath))
							{
								input.Seek(location.Offset, SeekOrigin.Begin);
								output.WriteFromStream(input, location.Size);
							}

							if (converting)
							{
								var ogger = new Process
								{
									StartInfo =
									{
										UseShellExecute = false,
										CreateNoWindow = true,
										RedirectStandardOutput = true,
										FileName = _ConverterPath,
										Arguments = string.Format(
											"-o \"{0}\" --pcb \"{2}\" \"{1}\"",
											oggPath,
											riffPath,
											pcbPath)
									}
								};

								ogger.Start();
								ogger.WaitForExit();

								if (ogger.ExitCode != 0)
								{
									string stdout = ogger.StandardOutput.ReadToEnd();

									LogError("Failed to convert \"{0}.{1}\"!",
										location.File,
										location.Name);
									LogMessage(stdout);
									File.Delete(oggPath);
									failed++;
									continue;
								}

								File.Delete(riffPath);

								if (revorbing)
								{
									var revorber = new Process
									{
										StartInfo =
										{
											UseShellExecute = false,
											CreateNoWindow = true,
											RedirectStandardOutput = true,
											FileName = _RevorbPath,
											Arguments = string.Format(
												"\"{0}\"",
												oggPath)
										}
									};

									revorber.Start();
									revorber.WaitForExit();

									if (revorber.ExitCode != 0)
									{
										string stdout = revorber.StandardOutput.ReadToEnd();

										LogError("Failed to revorb \"{0}.{1}\"!",
											location.File,
											location.Name);
										LogMessage(stdout);
									}
								}
							}

							succeeded++;
						}
					}
					finally
					{
						foreach (var stream in streams.Values)
						{
							if (stream != null)
							{
								stream.Close();
							}
						}
					}

					LogSuccess("Done, {0} succeeded, {1} failed, {2} total.", succeeded, failed, succeeded + failed);
				},
				_ExtractCancellationToken.Token);

			task.ContinueWith(
				t =>
				{
					TimeSpan elapsed = DateTime.Now.Subtract(startTime);
					LogSuccess("Extracted in {0}m {1}s {2}ms",
						elapsed.Minutes,
						elapsed.Seconds,
						elapsed.Milliseconds);
					ToggleControls(false);
				},
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnRanToCompletion,
				uiScheduler);

			task.ContinueWith(
				t =>
				{
					LogError("Failed to extract!");
					if (t.Exception != null)
					{
						LogError(t.Exception.InnerException != null
							? t.Exception.InnerException.ToString()
							: t.Exception.ToString());
					}
					ToggleControls(false);
				},
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted,
				uiScheduler);
		}

		private void OnCancel(object sender, EventArgs e)
		{
			_ExtractCancellationToken.Cancel();
		}

		#region ToggleControls

		private void ToggleControls(bool isExtracting)
		{
			if (InvokeRequired)
			{
				Invoke(
					(ToggleControlsDelegate) ToggleControls,
					isExtracting);
				return;
			}

			containerListBox.Enabled = isExtracting == false;
			fileListView.Enabled = isExtracting == false;
			selectNoneButton.Enabled = isExtracting == false;
			selectAllButton.Enabled = isExtracting == false;
			selectVisibleButton.Enabled = isExtracting == false;
			selectSearchButton.Enabled = isExtracting == false;
			listButton.Enabled = isExtracting == false;
			convertCheckBox.Enabled = _ConverterPath != null && isExtracting == false;
			revorbCheckBox.Enabled = _RevorbPath != null && isExtracting == false;
			validateCheckBox.Enabled = isExtracting == false;
			cancelButton.Enabled = isExtracting;
			startButton.Enabled = isExtracting == false;
		}

		private delegate void ToggleControlsDelegate(bool isExtracting);

		#endregion

		#region SetProgress

		private void SetProgress(int percent)
		{
			if (progressBar1.InvokeRequired)
			{
				Invoke((SetProgressDelegate) SetProgress, new object[]
				{
					percent
				});
				return;
			}

			progressBar1.Value = percent;
		}

		private delegate void SetProgressDelegate(int percent);

		#endregion

		#region Logging

		private void LogMessage(Color color, string message, params object[] args)
		{
			if (logTextBox.InvokeRequired)
			{
				logTextBox.Invoke(
					(LogMessageDelegate) LogMessage,
					color,
					message,
					args);
				return;
			}

			Color oldColor = logTextBox.SelectionColor;
			logTextBox.SelectionStart = logTextBox.Text.Length;
			logTextBox.SelectionColor = color;
			logTextBox.SelectedText = string.Format(message, args);
			logTextBox.SelectionStart = logTextBox.Text.Length;
			logTextBox.AppendText(Environment.NewLine);
			logTextBox.SelectionColor = oldColor;
			logTextBox.ScrollToCaret();
		}

		private void LogMessage(string message, params object[] args)
		{
			LogMessage(Color.Black, message, args);
		}

		private void LogSuccess(string message, params object[] args)
		{
			LogMessage(Color.Green, message, args);
		}

		private void LogWarning(string message, params object[] args)
		{
			LogMessage(Color.Brown, message, args);
		}

		private void LogError(string message, params object[] args)
		{
			LogMessage(Color.Red, message, args);
		}

		private delegate void LogMessageDelegate(Color color, string message, params object[] args);

		#endregion

		private class FilterFile
		{
			public bool IsPackage;
			public string Name;
			public string Value;

			public override string ToString()
			{
				return Name;
			}
		}

		private class FilterItem
		{
			public string Name;
			public string Value;

			public override string ToString()
			{
				return Name;
			}
		}

		private class WwiseLocation
		{
			public readonly List<WwiseLocation> Duplicates = new List<WwiseLocation>();
			public string Actor;
			public string File;
			public string Group;
			public WwiseIndex.FileHash Hash;
			public bool IsPackage;
			public string Locale;
			public string Name;
			public int Offset;
			public string Path;

			public bool Selected;
			public int Size;
		}
	}
}