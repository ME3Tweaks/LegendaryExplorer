using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;

namespace LegendaryExplorer.Dialogs
{
    public sealed class ExperimentShortcutTarget
    {
        public ExperimentShortcutTarget(string displayName, string storedPath, MenuItem menuItem)
        {
            DisplayName = displayName;
            StoredPath = storedPath;
            MenuItem = menuItem;
        }

        public string DisplayName { get; }
        public string StoredPath { get; }
        public MenuItem MenuItem { get; }
    }

    public partial class ExperimentKeyBindingDialog : NotifyPropertyChangedWindowBase
    {
        private readonly KeyGestureConverter keyGestureConverter = new();
        private bool isUpdatingSelection;

        private ExperimentKeyBindingDialog(Control owner, IEnumerable<ExperimentShortcutTarget> experiments, Dictionary<string, string> currentBindings)
        {
            Experiments = experiments.ToList();
            Bindings = new Dictionary<string, string>(currentBindings ?? []);
            SelectedExperiment = Experiments.FirstOrDefault(x => Bindings.ContainsKey(x.StoredPath)) ?? Experiments.FirstOrDefault();
            UpdateShortcutTextForSelection();
            OKCommand = new GenericCommand(AcceptSelection);
            ClearCommand = new GenericCommand(ClearBinding);

            InitializeComponent();

            if (owner != null)
            {
                Owner = owner as Window ?? GetWindow(owner);
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            ShortcutTextBox.Focus();
        }

        public static ExperimentKeyBindingDialog Show(Control owner, IEnumerable<ExperimentShortcutTarget> experiments, Dictionary<string, string> currentBindings)
        {
            var dialog = new ExperimentKeyBindingDialog(owner, experiments, currentBindings);
            dialog.ShowDialog();
            return dialog;
        }

        public List<ExperimentShortcutTarget> Experiments { get; }
        public Dictionary<string, string> Bindings { get; }

        private ExperimentShortcutTarget selectedExperiment;
        public ExperimentShortcutTarget SelectedExperiment
        {
            get => selectedExperiment;
            set
            {
                if (SetProperty(ref selectedExperiment, value))
                {
                    UpdateShortcutTextForSelection();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string shortcutText;
        public string ShortcutText
        {
            get => shortcutText;
            set
            {
                if (SetProperty(ref shortcutText, value))
                {
                    if (!isUpdatingSelection)
                    {
                        ValidationText = "";
                    }

                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string validationText;
        public string ValidationText
        {
            get => validationText;
            set => SetProperty(ref validationText, value);
        }

        public ICommand OKCommand { get; }
        public ICommand ClearCommand { get; }
        public bool ShouldSaveBindings { get; private set; }

        private void AcceptSelection()
        {
            ShouldSaveBindings = true;
            DialogResult = true;
        }

        private void ClearBinding()
        {
            if (SelectedExperiment == null)
            {
                return;
            }

            Bindings.Remove(SelectedExperiment.StoredPath);
            UpdateShortcutTextForSelection();
        }

        private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                return;
            }

            if ((key == Key.Back || key == Key.Delete) && Keyboard.Modifiers == ModifierKeys.None)
            {
                ClearBinding();
                return;
            }

            var modifiers = Keyboard.Modifiers;
            try
            {
                var gesture = new KeyGesture(key, modifiers);
                var gestureText = keyGestureConverter.ConvertToString(null, CultureInfo.InvariantCulture, gesture);
                if (TryParseShortcut(gestureText, out _))
                {
                    AssignShortcut(gestureText);
                }
            }
            catch (NotSupportedException)
            {
                ValidationText = "That key combination cannot be used as a shortcut.";
            }
            catch (InvalidOperationException)
            {
                ValidationText = "That key combination cannot be used as a shortcut.";
            }
            catch (ArgumentException)
            {
                ValidationText = "That key combination cannot be used as a shortcut.";
            }
        }

        private void AssignShortcut(string gestureText)
        {
            if (SelectedExperiment == null)
            {
                return;
            }

            var existingBinding = Bindings.FirstOrDefault(x => x.Value == gestureText && x.Key != SelectedExperiment.StoredPath);
            if (!string.IsNullOrEmpty(existingBinding.Key))
            {
                var existingExperiment = Experiments.FirstOrDefault(x => x.StoredPath == existingBinding.Key);
                var existingName = existingExperiment?.DisplayName ?? "another experiment";
                var result = MessageBox.Show(this, $"{gestureText} is already bound to {existingName}.\n\nReplace that binding?", "Bind Experiment to Key", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    UpdateShortcutTextForSelection();
                    return;
                }

                Bindings.Remove(existingBinding.Key);
            }

            Bindings[SelectedExperiment.StoredPath] = gestureText;
            UpdateShortcutTextForSelection();
        }

        private void UpdateShortcutTextForSelection()
        {
            isUpdatingSelection = true;
            ShortcutText = SelectedExperiment != null && Bindings.TryGetValue(SelectedExperiment.StoredPath, out var shortcut) ? shortcut : "";
            ValidationText = "";
            isUpdatingSelection = false;
        }

        private bool TryParseShortcut(string shortcut, out KeyGesture gesture)
        {
            gesture = null;
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                return false;
            }

            try
            {
                gesture = keyGestureConverter.ConvertFromString(null, CultureInfo.InvariantCulture, shortcut) as KeyGesture;
                return gesture != null;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
