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
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.ConditionalsEditor
{
    /// <summary>
    /// Interaction logic for ConditionalsEditorWindow.xaml
    /// </summary>
    public partial class ConditionalsEditorWindow : TrackingNotifyPropertyChangedWindowBase, IRecents
    {
        public const string CNDFileFilter = "ME3/LE3 conditional file|*.cnd";
        public ObservableCollectionExtended<CondListEntry> Conditionals { get; } = new();

        private CondListEntry _selectedCond;
        public CondListEntry SelectedCond
        {
            get => _selectedCond;
            set
            {
                if (SetProperty(ref _selectedCond, value))
                {
                    ConditionalTextBox.Text = _selectedCond is null ? "" : _selectedCond.Conditional.Decompile();
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
            RecentsController.InitRecentControl(Toolname, Recents_MenuItem, LoadFile);
        }

        //TODO: implement search feature, like the old one had

        public ICommand OpenCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }
        public ICommand CompileCommand { get; set; }
        public ICommand CloneCommand { get; set; }
        public ICommand ChangeIDCommand { get; set; }
        public ICommand DeleteCommand { get; set; }

        private void LoadCommands()
        {
            OpenCommand = new GenericCommand(OpenFile);
            SaveCommand = new GenericCommand(() => SavePackage(), FileIsLoaded);
            SaveAsCommand = new GenericCommand(SavePackageAs, FileIsLoaded);
            CompileCommand = new GenericCommand(Compile, CanCompile);
            CloneCommand = new GenericCommand(CloneEntry, EntryIsSelected);
            ChangeIDCommand = new GenericCommand(ChangeID, EntryIsSelected);
            DeleteCommand = new GenericCommand(DeleteEntry, EntryIsSelected);
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
                        Data = SelectedCond.Conditional.Data.TypedClone(),
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

        private void SavePackageAs()
        {
            var d = new SaveFileDialog { Filter = CNDFileFilter };
            if (d.ShowDialog() == true)
            {
                SavePackage(d.FileName);
                MessageBox.Show(this, "Done.");
            }
        }

        private void SavePackage(string filePath = null)
        {
            File.ConditionalEntries.Clear();
            File.ConditionalEntries.AddRange(Conditionals.Select(c => c.Conditional));
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

        private bool FileIsLoaded() => File is not null;

        private void OpenFile()
        {
            var d = new OpenFileDialog { Filter = CNDFileFilter };
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
                compilationMsgBox.Text = SelectedCond?.Compile(ConditionalTextBox.Text);
            }
        }

        private bool CanCompile()
        {
            return SelectedCond is not null && !string.IsNullOrWhiteSpace(ConditionalTextBox.Text);
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

        public void PropogateRecentsChange(IEnumerable<RecentsControl.RecentItem> newRecents)
        {
            RecentsController.PropogateRecentsChange(false, newRecents);
        }

        public string Toolname => "ConditionalsEditor";

        private void ConditionalsEditorWindow_OnClosing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
            if (Conditionals.Any(c => c.IsModified) &&
                MessageBoxResult.No == MessageBox.Show($"{Path.GetFileName(File.FilePath)} has unsaved changes. Do you really want to close Conditionals Editor?",
                                                       "Unsaved changes", MessageBoxButton.YesNo))
            {
                e.Cancel = true;
                return;
            }

            RecentsController?.Dispose();
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
                    }
                }
            }

            public CNDFile.ConditionalEntry Conditional;

            public CondListEntry(CNDFile.ConditionalEntry conditional)
            {
                Conditional = conditional;
                _iD = conditional.ID;
            }

            public string Compile(string text)
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
                    return $"Compilation Error!\n{e.GetType().Name}: {e.Message}";
                }
                if (!original.AsSpan().SequenceEqual(Conditional.Data))
                {
                    IsModified = true;
                }

                return "Compiled!";
            }
        }
    }
}
