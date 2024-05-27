using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Be.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.ConditionalsEditor
{
    /// <summary>
    /// Interaction logic for ConditionalsEditorWindow.xaml
    /// </summary>
    public partial class ConditionalsEditorWindow : TrackingNotifyPropertyChangedWindowBase, IRecents
    {
        #region DependencyProperties

        public int HexBoxMinWidth
        {
            get => (int)GetValue(HexBoxMinWidthProperty);
            set => SetValue(HexBoxMinWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMinWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMinWidth), typeof(int), typeof(ConditionalsEditorWindow), new PropertyMetadata(default(int)));

        public int HexBoxMaxWidth
        {
            get => (int)GetValue(HexBoxMaxWidthProperty);
            set => SetValue(HexBoxMaxWidthProperty, value);
        }
        public static readonly DependencyProperty HexBoxMaxWidthProperty = DependencyProperty.Register(
            nameof(HexBoxMaxWidth), typeof(int), typeof(ConditionalsEditorWindow), new PropertyMetadata(default(int)));

        public bool HideHexBox
        {
            get => (bool)GetValue(HideHexBoxProperty);
            set => SetValue(HideHexBoxProperty, value);
        }
        public static readonly DependencyProperty HideHexBoxProperty = DependencyProperty.Register(
            nameof(HideHexBox), typeof(bool), typeof(ConditionalsEditorWindow), new PropertyMetadata(false, (obj, e) =>
            {
                var window = (ConditionalsEditorWindow)obj;
                if ((bool)e.NewValue)
                {
                    window.hexboxContainer.Visibility = window.HexProps_GridSplitter.Visibility = Visibility.Collapsed;
                    window.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(0);
                    window.HexboxColumnDefinition.MinWidth = 0;
                    window.HexboxColumnDefinition.MaxWidth = 0;
                    window.HexboxColumnDefinition.Width = new GridLength(0);
                }
                else
                {
                    window.hexboxContainer.Visibility = window.HexProps_GridSplitter.Visibility = Visibility.Visible;
                    window.HexboxColumnDefinition.Width = new GridLength(window.HexBoxMinWidth);
                    window.HexboxColumn_GridSplitter_ColumnDefinition.Width = new GridLength(1);
                    window.HexboxColumnDefinition.bind(ColumnDefinition.MinWidthProperty, window, nameof(HexBoxMinWidth));
                    window.HexboxColumnDefinition.bind(ColumnDefinition.MaxWidthProperty, window, nameof(HexBoxMaxWidth));
                }
            }));

        #endregion

        public const string CNDFileFilter = "ME3/LE3 conditional file|*.cnd";

        private HexBox hexBox;

        public ObservableCollectionExtended<CondListEntry> Conditionals { get; } = new();

        private CondListEntry _selectedCond;
        public CondListEntry SelectedCond
        {
            get => _selectedCond;
            set
            {
                if (SetProperty(ref _selectedCond, value))
                {
                    if (_selectedCond is null)
                    {
                        ConditionalTextBox.Text = "";
                        hexBox.ByteProvider = new ReadOptimizedByteProvider();
                    }
                    else
                    {
                        DisplayCondition();
                    }
                    compilationMsgBox.Clear();
                }
            }
        }

        private CNDFile _file;
        public CNDFile File
        {
            get => _file;
            set => SetProperty(ref _file, value);
        }

        public ConditionalsEditorWindow() : base("Conditionals Editor", true)
        {
            LoadCommands();
            InitializeComponent();
            HideHexBox = true;
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);
        }

        private void ConditionalsEditorWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            hexBox = (HexBox)hexbox_Host.Child;
            hexBox.ByteProvider = new ReadOptimizedByteProvider();
            this.bind(HexBoxMinWidthProperty, hexBox, nameof(hexBox.MinWidth));
            this.bind(HexBoxMaxWidthProperty, hexBox, nameof(hexBox.MaxWidth));
        }

        public ICommand OpenCommand { get; set; }
        public ICommand NewFileCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand CompileCommand { get; set; }
        public ICommand CloneCommand { get; set; }
        public ICommand ChangeIDCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand ToggleHexBoxCommand { get; set; }
        public ICommand SaveHexChangesCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand SearchAgainCommand { get; set; }
        public ICommand AddBlankCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenFile);
            NewFileCommand = new GenericCommand(NewFile);
            SaveCommand = new GenericCommand(Save, FileIsLoaded);
            SaveAsCommand = new GenericCommand(SaveAs, FileIsLoaded);
            CompileCommand = new GenericCommand(Compile, CanCompile);
            CloneCommand = new GenericCommand(CloneEntry, EntryIsSelected);
            ChangeIDCommand = new GenericCommand(ChangeID, EntryIsSelected);
            DeleteCommand = new GenericCommand(DeleteEntry, EntryIsSelected);
            ToggleHexBoxCommand = new GenericCommand(ToggleHexBox, FileIsLoaded);
            SaveHexChangesCommand = new GenericCommand(SaveHexChanges, EntryIsSelected);
            SearchCommand = new GenericCommand(SearchPrompt, FileIsLoaded);
            SearchAgainCommand = new GenericCommand(Search, CanSearchAgain);
            AddBlankCommand = new GenericCommand(AddBlankConditional, FileIsLoaded);
        }

        private bool CanSearchAgain() => FileIsLoaded() && !string.IsNullOrEmpty(searchText);

        private void Search()
        {
            foreach (CondListEntry entry in Conditionals.AfterThenBefore(SelectedCond))
            {
                try
                {
                    string text = entry.Conditional.Decompile();
                    string entryPlotPath = entry.PlotPath ?? "";
                    if (text.Contains(searchText, StringComparison.OrdinalIgnoreCase) || entryPlotPath.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedCond = entry;
                        ConditionalsListBox.ScrollIntoView(entry);
                        return;
                    }
                }
                catch
                {
                    //
                }
            }

            MessageBox.Show($"'{searchText}' was not found!");
        }

        private string searchText = "";
        private void SearchPrompt()
        {
            var s = PromptDialog.Prompt(this, "Input string to search for", "Search Input", searchText, true);
            if (s is not null)
            {
                searchText = s;
                if (searchText is not "")
                {
                    Search();
                }
            }
        }

        private void SaveHexChanges()
        {
            if (SelectedCond is not null)
            {
                var originalData = _selectedCond.Conditional.Data;
                var newData = ((ReadOptimizedByteProvider)hexBox.ByteProvider).Span;
                if (!newData.SequenceEqual(originalData))
                {
                    _selectedCond.Conditional.Data = newData.ToArray();
                    _selectedCond.IsModified = true;
                    DisplayCondition();
                }
            }
        }

        private void ToggleHexBox()
        {
            HideHexBox = !HideHexBox;
        }

        private void ChangeID()
        {
            if (PromptDialog.Prompt(this, "Enter new ID", defaultValue: SelectedCond.ID.ToString(), selectText: true) is string txt)
            {
                if (int.TryParse(txt, out int newID) && newID > 0)
                {
                    SelectedCond.ID = newID;
                }
                else
                {
                    MessageBox.Show($"'{txt}' is not a positive integer!");
                }
            }
        }

        private void DeleteEntry()
        {
            Conditionals.Remove(SelectedCond);
        }

        private void CloneEntry()
        {
            if (PromptDialog.Prompt(this, "Enter ID for new entry", defaultValue: SelectedCond.ID.ToString(), selectText: true) is string txt)
            {
                if (int.TryParse(txt, out int newID) && newID > 0)
                {
                    var newCond = new CondListEntry(new CNDFile.ConditionalEntry
                    {
                        Data = SelectedCond.Conditional.Data.ArrayClone(),
                        ID = newID
                    })
                    {
                        IsModified = true
                    };
                    Conditionals.Add(newCond);
                    SelectedCond = newCond;
                    ConditionalsListBox.ScrollIntoView(SelectedCond);
                }
                else
                {
                    MessageBox.Show($"'{txt}' is not a positive integer!");
                }
            }
        }

        private bool EntryIsSelected() => SelectedCond is not null;

        private void Save()
        {
            if (Validate())
            {
                if (File.FilePath is null)
                {
                    // Unsaved new file
                    var d = new SaveFileDialog { Filter = CNDFileFilter };
                    if (d.ShowDialog() == false) return;
                    File.FilePath = d.FileName;
                    RecentsController.AddRecent(d.FileName, false, null); // Can we infer game this file is for?
                    RecentsController.SaveRecentList(true);
                    Title = $"Conditionals Editor - {Path.GetFileName(d.FileName)}";
                }

                SaveFile();
            }
        }

        private void SaveAs()
        {
            if (Validate())
            {
                var d = new SaveFileDialog { Filter = CNDFileFilter };
                if (d.ShowDialog() == true)
                {
                    SaveFile(d.FileName);
                    MessageBox.Show(this, "Done.");
                }
            }
        }

        private void SaveFile(string filePath = null)
        {
            File.ConditionalEntries.Clear();
            File.ConditionalEntries.AddRange(Conditionals.Select(c => c.Conditional).OrderBy(c => c.ID));
            File.ToFile(filePath);

            //don't reset modified state on save as
            if (filePath is null)
            {
                foreach (CondListEntry listEntry in Conditionals)
                {
                    listEntry.IsModified = false;
                }
            }
        }

        private bool Validate()
        {
            int id = 0;
            try
            {
                foreach (CondListEntry entry in Conditionals)
                {
                    id = entry.ID;
                    entry.Conditional.Decompile();
                }
            }
            catch
            {
                MessageBox.Show($"Cannot save this file: Conditional {id} is malformed!", "Broken Conditional!", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private bool FileIsLoaded() => File is not null;

        private void OpenFile()
        {
            var d = new OpenFileDialog
            {
                Filter = CNDFileFilter,
                Title = "Open Conditionals file",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadFile(d.FileName);
                }
                catch (Exception ex) when (!App.IsDebug)
                {
                    MessageBox.Show(this, "Unable to open file:\n" + ex.Message);
                }
            }
        }

        private void Compile()
        {
            if (SelectedCond is not null)
            {
                bool error = true;
                compilationMsgBox.Text = SelectedCond?.Compile(ConditionalTextBox.Text, out error);
                if (!error)
                {
                    DisplayCondition();
                }
            }
        }

        private void DisplayCondition()
        {
            try
            {
                hexBox.ByteProvider = new ReadOptimizedByteProvider(_selectedCond.Conditional.Data);
                ConditionalTextBox.Text = _selectedCond.Conditional.Decompile();
            }
            catch (Exception e)
            {
                ConditionalTextBox.Text = $"ERROR! COULD NOT DECOMPILE!\n{e.FlattenException()}";
            }
        }

        private bool CanCompile()
        {
            return SelectedCond is not null && !string.IsNullOrWhiteSpace(ConditionalTextBox.Text);
        }

        public void LoadFile(string filePath, int cndId)
        {
            LoadFile(filePath);
            SelectedCond = Conditionals.FirstOrDefault(c => c.ID == cndId);
            ConditionalsListBox.ScrollIntoView(SelectedCond);
        }

        public void LoadFile(string filePath)
        {
            Conditionals.ClearEx();
            SelectedCond = null;
            try
            {
                File = CNDFile.FromFile(filePath);
                RecentsController.AddRecent(filePath, false, null); // Can we infer game this file is for?
                RecentsController.SaveRecentList(true);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);

                File = null;
                Title = "Conditionals Editor";
                return;
            }

            Title = $"Conditionals Editor - {Path.GetFileName(filePath)}";
            Conditionals.AddRange(File.ConditionalEntries.OrderBy(c => c.ID).Select(c => new CondListEntry(c)));
        }

        private void NewFile()
        {
            if (FileIsLoaded()) Save();
            File = new CNDFile
            {
                ConditionalEntries = new List<CNDFile.ConditionalEntry>()
            };
            Conditionals.Clear();
            SelectedCond = null;
            Save();
        }

        private void AddBlankConditional()
        {
            if (PromptDialog.Prompt(this, "Enter ID for new entry", selectText: true) is string txt)
            {
                if (int.TryParse(txt, out int newID) && newID > 0)
                {
                    var newCond = new CondListEntry(new CNDFile.ConditionalEntry
                    {
                        Data = ME3ConditionalsCompiler.Compile("Bool false"),
                        ID = newID
                    })
                    {
                        IsModified = true
                    };
                    if (Conditionals.Any(c => c.ID == newCond.ID))
                    {
                        var wdlg = MessageBox.Show("This conditional ID already exists in this file. Continue?", "Warning", MessageBoxButton.OKCancel);
                        if (wdlg == MessageBoxResult.Cancel)
                            return;
                    }
                    Conditionals.Add(newCond);
                    SelectedCond = newCond;
                    ConditionalsListBox.ScrollIntoView(SelectedCond);
                }
                else
                {
                    MessageBox.Show($"'{txt}' is not a positive integer!");
                }
            }
        }

        public void PropogateRecentsChange(string propogationSource, IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "ConditionalsEditor";

        private void ConditionalsEditorWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
            if (Conditionals.Any(c => c.IsModified) &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(File.FilePath) ?? "Untitled file"} has unsaved changes. Do you really want to close Conditionals Editor?",
                                                       "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
                return;
            }

            RecentsController?.Dispose();
            hexBox = null;
        }

        private void ConditionalTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            compilationMsgBox.Clear();
        }

        public class CondListEntry : NotifyPropertyChangedBase
        {
            private bool _isModified;
            public bool IsModified
            {
                get => _isModified;
                set => SetProperty(ref _isModified, value);
            }

            private int _iD;
            public int ID
            {
                get => _iD;
                set
                {
                    if (SetProperty(ref _iD, value))
                    {
                        IsModified = true;
                        Conditional.ID = value;
                        PlotPath = PlotDatabases.FindPlotConditionalByID(value, MEGame.LE3)?.Path;
                    }
                }
            }

            private string _plotPath;

            public string PlotPath
            {
                get => _plotPath;
                set => SetProperty(ref _plotPath, value);
            }

            public CNDFile.ConditionalEntry Conditional;

            public CondListEntry(CNDFile.ConditionalEntry conditional)
            {
                Conditional = conditional;
                _iD = conditional.ID;
                PlotPath = PlotDatabases.FindPlotConditionalByID(conditional.ID, MEGame.LE3)?.Path;
            }

            public string Compile(string text, out bool error)
            {
                var original = Conditional.Data;
                try
                {
                    Conditional.Compile(text);
                    //the compiler is somewhat... lacking, in proper validation, so we use decompiler to see if compilation
                    //produced something useful (it should throw if there's an error)
                    Conditional.Decompile();
                }
                catch (Exception e)
                {
                    Conditional.Data = original;
                    error = true;
                    return $"Compilation Error!\n{e.GetType().Name}: {e.Message}";
                }
                if (!original.AsSpan().SequenceEqual(Conditional.Data))
                {
                    IsModified = true;
                }

                error = false;
                return "Compiled!";
            }
        }

        private void RecompileAll_Click(object sender, RoutedEventArgs e)
        {
            var modified = new List<string>();
            foreach (CondListEntry condListEntry in Conditionals)
            {
                condListEntry.Compile(condListEntry.Conditional.Decompile(), out bool error);
                if (error)
                {
                    modified.Add(condListEntry.ID.ToString());
                }
            }

            modified.AddRange(Conditionals.Where(c => c.IsModified).Select(c => c.ID.ToString()).ToList());

            if (modified.Any())
            {
                new ListDialog(modified, "Modified Conditionals", "These conditionals did not recompile properly!", this).Show();
            }
            else
            {
                MessageBox.Show("All conditionals recompiled identically!");
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string ext = Path.GetExtension(files[0]).ToLower();
                if (ext != ".cnd")
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                }
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadFile(files[0]);
            }
        }
    }
}
