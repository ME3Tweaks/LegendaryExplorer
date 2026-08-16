using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor.IDE;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST = LegendaryExplorerCore.UnrealScript.Analysis.Visitors.ST;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.ScriptEditor
{
    /// <summary>
    /// Interaction logic for IdeThemePicker.xaml
    /// </summary>
    public sealed partial class IdeThemePicker : NotifyPropertyChangedWindowBase
    {
        private const string DefaultThemeName = "Default";
        private const string WindowTitle = "Script Editor Theme Editor";

        public ObservableCollection<ColorBinding> Colors { get; } = [];
        public ObservableCollection<ThemeEntry> ThemeEntries { get; } = [];

        private ThemeEntry _selectedThemeEntry;
        public ThemeEntry SelectedThemeEntry
        {
            get => _selectedThemeEntry;
            set => SetProperty(ref _selectedThemeEntry, value);
        }

        private bool _suppressLivePreview;

        // In-memory working copies — includes unsaved edits. Key = theme name.
        private readonly Dictionary<string, ThemeData> _workingThemes = [];

        // Snapshot of what's persisted in settings. Key = theme name (excludes Default).
        private readonly Dictionary<string, ThemeData> _savedThemes = [];

        // The theme that was selected before a selection change, used for stashing.
        private ThemeEntry _previousThemeEntry;

        private IdeThemePicker(Window owner)
        {
            _suppressLivePreview = true;

            // Build Default color map from hardcoded defaults
            _workingThemes[DefaultThemeName] = BuildDefaultThemeData();

            // Load all saved themes from settings into both saved and working
            var persisted = Settings.ScriptIDE_SavedThemes;
            foreach (var kvp in persisted)
            {
                _savedThemes[kvp.Key] = kvp.Value.DeepCopy();
                _workingThemes[kvp.Key] = kvp.Value.DeepCopy();
            }

            // Populate theme entries
            ThemeEntries.Add(new ThemeEntry(DefaultThemeName, isDefault: true));
            foreach (string name in persisted.Keys)
            {
                ThemeEntries.Add(new ThemeEntry(name, isDefault: false));
            }

            // Build color bindings from current SyntaxInfo state
            Colors.Add(new ColorBinding("Background", SyntaxInfo.BackgroundBrush.Color));
            foreach (ST val in Enum.GetValues<ST>())
            {
                Colors.Add(new ColorBinding(val.ToString(), SyntaxInfo.ColorBrushes[val].Color));
            }

            // Select the active theme
            string activeThemeName = Settings.ScriptIDE_ActiveTheme;
            ThemeEntry activeEntry = null;
            if (!string.IsNullOrEmpty(activeThemeName))
            {
                activeEntry = ThemeEntries.FirstOrDefault(t => t.Name == activeThemeName);
            }
            activeEntry ??= ThemeEntries[0]; // Default

            // Load the selected theme's working copy into the Colors collection
            LoadColorsFromThemeData(_workingThemes[activeEntry.Name]);

            InitializeComponent();
            Owner = owner;
            Title = WindowTitle;

            // Select after InitializeComponent so the ListBox is ready
            SelectedThemeEntry = activeEntry;
            _previousThemeEntry = activeEntry;

            foreach (ColorBinding cb in Colors)
            {
                cb.PropertyChanged += ColorBinding_PropertyChanged;
            }

            _suppressLivePreview = false;

            // Apply live preview for the initially selected theme
            ApplyCurrentColorsToSyntaxInfo();
            UpdateDirtyIndicators();

            Closed += (_, _) =>
            {
                //ensure singleton is cleared even if Closing was bypassed
                Instance = null;
            };
        }

        private static IdeThemePicker Instance;

        public static void ShowThemeEditor(Window owner)
        {
            if (Instance is not null)
            {
                Instance.Owner = owner;
                Instance.RestoreAndBringToFront();
            }
            else
            {
                Instance = new IdeThemePicker(owner);
                Instance.Show();
            }
        }

        private static ThemeData BuildDefaultThemeData()
            => new(SyntaxInfo.DefaultBackground, new Dictionary<ST, Color>(SyntaxInfo.DefaultColors));

        private void ColorBinding_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressLivePreview || e.PropertyName != nameof(ColorBinding.Color)) return;

            if (SelectedThemeEntry is { IsDefault: true })
            {
                // Capture attempted color before reverting
                var cb = (ColorBinding)sender;
                Color attemptedColor = cb.Color;

                // Revert to the Default value
                var defaultData = _workingThemes[DefaultThemeName];
                _suppressLivePreview = true;
                if (cb.Name == "Background")
                    cb.Color = defaultData.Background;
                else if (Enum.TryParse<ST>(cb.Name, out var ef) && defaultData.Colors.TryGetValue(ef, out var c))
                    cb.Color = c;
                _suppressLivePreview = false;

                var result = MessageBox.Show(
                    "The Default theme cannot be edited. Create a new theme based on Default?",
                    "Default Theme",
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    // Run new-theme flow
                    ThemeEntry newEntry = PromptAndCreateNewTheme();
                    if (newEntry != null)
                    {
                        // Re-apply the attempted edit on the new theme
                        _suppressLivePreview = true;
                        foreach (ColorBinding c in Colors)
                        {
                            if (c.Name == cb.Name)
                            {
                                c.Color = attemptedColor;
                                break;
                            }
                        }
                        _suppressLivePreview = false;

                        // Update the working copy and live preview
                        _workingThemes[newEntry.Name] = GetCurrentThemeData();
                        ApplyCurrentColorsToSyntaxInfo();
                        UpdateDirtyIndicators();
                    }
                }
            }
            else
            {
                ApplyCurrentColorsToSyntaxInfo();
                UpdateDirtyIndicators();
            }
        }

        private void ApplyCurrentColorsToSyntaxInfo()
        {
            var data = GetCurrentThemeData();
            SyntaxInfo.ApplyTheme(data.Colors, data.Background);
        }

        private void ThemeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedThemeEntry is not { } entry) return;

            // Stash current color picker values for the previous theme
            if (_previousThemeEntry != null && _workingThemes.ContainsKey(_previousThemeEntry.Name))
            {
                _workingThemes[_previousThemeEntry.Name] = GetCurrentThemeData();
            }

            // Load the new theme's working copy into color pickers
            if (_workingThemes.TryGetValue(entry.Name, out var data))
            {
                LoadColorsFromThemeData(data);
            }

            _previousThemeEntry = entry;

            ApplyCurrentColorsToSyntaxInfo();
            UpdateDirtyIndicators();
        }

        private void New_OnClick(object sender, RoutedEventArgs e)
        {
            ThemeEntry newEntry = PromptAndCreateNewTheme();
            if (newEntry != null)
            {
                UpdateDirtyIndicators();
            }
        }

        private ThemeEntry PromptAndCreateNewTheme()
        {
            var name = PromptDialog.Prompt(this, "Theme name:", "New Theme",
                validator: str =>
                {
                    str = str?.Trim();
                    if (string.IsNullOrEmpty(str))
                    {
                        return (false, "Theme name cannot be empty.");
                    }
                    if (str == DefaultThemeName)
                    {
                        return (false, $"Cannot use the name \"{DefaultThemeName}\".");
                    }
                    if (ThemeEntries.Any(t => t.Name == str))
                    {
                        return (false, $"A theme named \"{str}\" already exists.");
                    }
                    return (true, "");
                })?.Trim();
            if (name is null) return null;

            // Copy current colors as the new theme (saved immediately = starts clean)
            var themeData = GetCurrentThemeData();
            _workingThemes[name] = themeData.DeepCopy();
            _savedThemes[name] = themeData.DeepCopy();

            // Persist all saved themes
            PersistSavedThemes();
            Settings.ScriptIDE_ActiveTheme = name;

            var entry = new ThemeEntry(name, isDefault: false);
            ThemeEntries.Add(entry);
            SelectedThemeEntry = entry;
            return entry;
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: ThemeEntry { IsDefault: false } entry }) return;

            // Stash current colors into working
            _workingThemes[entry.Name] = GetCurrentThemeData();

            // Deep-copy working → saved
            _savedThemes[entry.Name] = _workingThemes[entry.Name].DeepCopy();

            // Persist
            PersistSavedThemes();
            Settings.ScriptIDE_ActiveTheme = entry.Name;

            UpdateDirtyIndicators();
        }

        private void DeleteInline_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: ThemeEntry { IsDefault: false } entry }) return;

            if (MessageBoxResult.Yes != MessageBox.Show(this, $"Are you sure you want to delete the {entry.Name} theme?", "Delete theme?", MessageBoxButton.YesNo))
            {
                return;
            }

            _workingThemes.Remove(entry.Name);
            _savedThemes.Remove(entry.Name);
            ThemeEntries.Remove(entry);

            PersistSavedThemes();

            if (Settings.ScriptIDE_ActiveTheme == entry.Name)
            {
                Settings.ScriptIDE_ActiveTheme = "";
            }

            if (SelectedThemeEntry == null || SelectedThemeEntry == entry)
            {
                SelectedThemeEntry = ThemeEntries[0]; // Select Default
            }

            UpdateDirtyIndicators();
        }

        private ThemeData GetCurrentThemeData()
        {
            Color bg = SyntaxInfo.DefaultBackground;
            var colors = new Dictionary<ST, Color>();
            foreach (ColorBinding cb in Colors)
            {
                if (cb.Name == "Background")
                    bg = cb.Color;
                else if (Enum.TryParse<ST>(cb.Name, out var ef))
                    colors[ef] = cb.Color;
            }
            return new ThemeData(bg, colors);
        }

        private void StashCurrentColors()
        {
            if (SelectedThemeEntry is { } entry && _workingThemes.ContainsKey(entry.Name))
            {
                _workingThemes[entry.Name] = GetCurrentThemeData();
            }
        }

        private void LoadColorsFromThemeData(ThemeData data)
        {
            _suppressLivePreview = true;
            foreach (ColorBinding cb in Colors)
            {
                if (cb.Name == "Background")
                    cb.Color = data.Background;
                else if (Enum.TryParse<ST>(cb.Name, out var ef) && data.Colors.TryGetValue(ef, out var c))
                    cb.Color = c;
            }
            _suppressLivePreview = false;
        }

        private bool IsThemeDirty(string themeName)
        {
            if (themeName == DefaultThemeName) return false;
            if (!_workingThemes.TryGetValue(themeName, out var working)) return false;
            if (!_savedThemes.TryGetValue(themeName, out var saved)) return true; // new unsaved theme
            return !working.Equals(saved);
        }

        private void UpdateDirtyIndicators()
        {
            // Stash current colors so the working copy is up to date
            StashCurrentColors();

            bool anyDirty = false;
            foreach (ThemeEntry entry in ThemeEntries)
            {
                entry.IsDirty = IsThemeDirty(entry.Name);
                anyDirty |= entry.IsDirty;
            }
            Title = anyDirty ? $"{WindowTitle}*" : WindowTitle;
        }

        private void PersistSavedThemes()
        {
            Settings.ScriptIDE_SavedThemes = new Dictionary<string, ThemeData>(_savedThemes);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Stash current color picker values
            StashCurrentColors();

            // Collect dirty theme names
            var dirtyNames = ThemeEntries
                .Where(t => !t.IsDefault && IsThemeDirty(t.Name))
                .Select(t => t.Name)
                .ToList();

            if (dirtyNames.Count > 0)
            {
                var result = MessageBox.Show(
                    $"You have unsaved changes to: {string.Join(", ", dirtyNames)}. Save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        return;
                    case MessageBoxResult.Yes:
                        // Save all dirty themes
                        foreach (string name in dirtyNames)
                        {
                            if (_workingThemes.TryGetValue(name, out var working))
                            {
                                _savedThemes[name] = working.DeepCopy();
                            }
                        }
                        PersistSavedThemes();
                        break;
                    case MessageBoxResult.No:
                        // Revert live SyntaxInfo to the saved version of the selected theme (or Default)
                        string revertName = SelectedThemeEntry?.Name ?? DefaultThemeName;
                        ThemeData revertData;
                        if (revertName == DefaultThemeName)
                        {
                            revertData = BuildDefaultThemeData();
                        }
                        else if (_savedThemes.TryGetValue(revertName, out var savedData))
                        {
                            revertData = savedData;
                        }
                        else
                        {
                            revertData = BuildDefaultThemeData();
                        }
                        LoadColorsFromThemeData(revertData);
                        ApplyCurrentColorsToSyntaxInfo();
                        break;
                }
            }

            // Persist the selected theme name
            Settings.ScriptIDE_ActiveTheme = SelectedThemeEntry is { IsDefault: false } entry ? entry.Name : "";

            foreach (ColorBinding cb in Colors)
            {
                cb.PropertyChanged -= ColorBinding_PropertyChanged;
            }
            Instance = null;
        }
    }

    public class ThemeEntry(string name, bool isDefault) : NotifyPropertyChangedBase
    {
        public string Name { get; } = name;
        public bool IsDefault { get; } = isDefault;
        public bool CanDelete => !IsDefault;

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        public override string ToString() => Name;
    }

    public class ColorBinding(string name, Color color) : NotifyPropertyChangedBase
    {
        public Color Color
        {
            get => color;
            set => SetProperty(ref color, value);
        }

        public string Name { get; } = name;
    }

    [JsonConverter(typeof(ThemeDataJsonConverter))]
    public record ThemeData(Color Background, Dictionary<ST, Color> Colors)
    {
        public ThemeData DeepCopy() => new(Background, new Dictionary<ST, Color>(Colors));

        public virtual bool Equals(ThemeData other)
        {
            if (other is null) return false;
            return Background == other.Background
                && Colors.Count == other.Colors.Count
                && Colors.All(kvp => other.Colors.TryGetValue(kvp.Key, out var c) && c == kvp.Value);
        }

        public override int GetHashCode() => HashCode.Combine(Background, Colors.Count);
    }

    public class ThemeDataJsonConverter : JsonConverter<ThemeData>
    {
        public override ThemeData ReadJson(JsonReader reader, Type objectType, ThemeData existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            Color bg = SyntaxInfo.DefaultBackground;
            var colors = new Dictionary<ST, Color>();
            foreach (var prop in obj.Properties())
            {
                string colorStr = prop.Value.ToString();
                if (prop.Name == "Background")
                {
                    try { bg = (Color)ColorConverter.ConvertFromString(colorStr); }
                    catch (FormatException ex) { Debug.WriteLine($"Failed to parse background color '{colorStr}': {ex.Message}"); }
                }
                else if (Enum.TryParse<ST>(prop.Name, out var ef))
                {
                    try { colors[ef] = (Color)ColorConverter.ConvertFromString(colorStr); }
                    catch (FormatException ex) { Debug.WriteLine($"Failed to parse color '{prop.Name}' = '{colorStr}': {ex.Message}"); }
                }
            }
            return new ThemeData(bg, colors);
        }

        public override void WriteJson(JsonWriter writer, ThemeData value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Background");
            writer.WriteValue(value.Background.ToString());
            foreach (var kvp in value.Colors)
            {
                writer.WritePropertyName(kvp.Key.ToString());
                writer.WriteValue(kvp.Value.ToString());
            }
            writer.WriteEndObject();
        }
    }
}