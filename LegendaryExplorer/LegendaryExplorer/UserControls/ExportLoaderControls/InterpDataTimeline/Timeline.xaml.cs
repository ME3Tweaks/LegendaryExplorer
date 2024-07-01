using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.InterpEditor;
using LegendaryExplorer.Tools.PackageEditor;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Point = System.Windows.Point;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for Timeline.xaml
    /// </summary>
    public partial class Timeline : ExportLoaderControl
    {
        public override bool CanParse(ExportEntry exportEntry) => CanParseStatic(exportEntry);

        public static bool CanParseStatic(ExportEntry exportEntry) => exportEntry.ClassName == "InterpData";

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            LoadGroups();
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            InterpGroups.ClearEx();
            ResetView();
        }

        private double _scale;

        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        private double _offset;

        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }

        private string _lineStrRef;
        public string LineStrRef
        {
            get => _lineStrRef;
            set => SetProperty(ref _lineStrRef, value);
        }

        public event Action<ExportEntry> SelectionChanged;
        public bool HasSelection(object obj) { return MatineeTree.SelectedItem != null; }
        public bool HasData(object obj) { return CurrentLoadedExport != null; }
        public ObservableCollectionExtended<InterpGroup> InterpGroups { get; } = new();

        public Timeline() : base("Timeline")
        {
            LoadCommands();
            InitializeComponent();
            ResetView();
        }

        public ICommand OpenSelection { get; set; }
        public ICommand OpenInterpData { get; set; }
        public ICommand AddInterpGroupCmd { get; set; }
        public ICommand AddTrackCmd { get; set; }
        public ICommand RenameTrackCommand { get; set; }

        private void LoadCommands()
        {
            OpenSelection = new RelayCommand(OpenInToolkit, HasSelection);
            OpenInterpData = new RelayCommand(OpenInToolkit, HasData);
            AddInterpGroupCmd = new RelayCommand(AddInterpGroup, CanAddInterpGroup);
            AddTrackCmd = new GenericCommand(AddTrack, CanAddTrack);
            RenameTrackCommand = new GenericCommand(RenameTrack, CanRenameTrack);
        }

        private void AddTrack()
        {
            if (MatineeTree.SelectedItem is InterpGroup group)
            {
                if (ClassPickerDlg.GetClass(this, MatineeHelper.GetInterpTracks(Pcc.Game), "Choose Track to Add", "Add") is ClassInfo info)
                {
                    ExportEntry trackExport = MatineeHelper.AddNewTrackToGroup(group.Export, info.ClassName);
                    MatineeHelper.AddDefaultPropertiesToTrack(trackExport);
                }
            }
        }

        private bool CanAddTrack() => MatineeTree.SelectedItem is InterpGroup;

        public void RenameTrack()
        {
            if (MatineeTree.SelectedItem is InterpGroup group)
            {
                var groupNameProp = group.Export.GetProperty<NameProperty>("GroupName") ?? new NameProperty("GroupName");
                var result = SelectOrAddNamePromptDialog.Prompt(this, "Rename Group:", "Rename InterpGroup", Pcc,
                    out var newGroupName, groupNameProp.Value);

                if (!result || newGroupName == groupNameProp.Value) return;
                if (newGroupName == NameReference.None || newGroupName == "")
                {
                    group.Export.RemoveProperty("GroupName");
                    group.GroupName = group.Export.ObjectName.Instanced;
                }
                else
                {
                    groupNameProp.Value = newGroupName;
                    group.Export.WriteProperty(groupNameProp);
                    group.GroupName = newGroupName.Instanced;
                }
            }
            else if (MatineeTree.SelectedItem is InterpTrack track)
            {
                var newTitle = PromptDialog.Prompt(this, "Rename Track:", "Rename InterpTrack", track.TrackTitle);
                if (newTitle is null || newTitle == track.TrackTitle) return;
                if (newTitle != "")
                {
                    track.Export.WriteProperty(new StrProperty(newTitle, "TrackTitle"));
                    track.TrackTitle = newTitle;
                }
                else // Hitting 'OK' on an empty string removes the name
                {
                    track.Export.RemoveProperty("TrackTitle");
                    track.TrackTitle = track.Export.ObjectName.Instanced;
                }
            }
        }

        public bool CanRenameTrack() => MatineeTree.SelectedItem is InterpGroup or InterpTrack;

        private void AddInterpGroup(object obj)
        {
            if (CanAddInterpGroup(obj))
            {
                if (obj is "Director")
                {
                    MatineeHelper.AddNewGroupDirectorToInterpData(CurrentLoadedExport);
                }
                else if (PromptDialog.Prompt(this, "Name of InterpGroup:") is string groupName)
                {
                    MatineeHelper.AddNewGroupToInterpData(CurrentLoadedExport, groupName);
                }
            }
        }

        private bool CanAddInterpGroup(object obj)
        {
            if (CurrentLoadedExport is null)
            {
                return false;
            }
            return obj is not "Director" || InterpGroups.All(g => g.Export.ClassName != "InterpGroupDirector");
        }

        private void OpenInToolkit(object obj)
        {
            var command = obj as string;
            if (CurrentLoadedExport != null)
            {
                ExportEntry exportEntry = CurrentLoadedExport;
                if (command == "Track")
                {
                    switch (MatineeTree.SelectedItem)
                    {
                        case InterpGroup group:
                            exportEntry = group.Export;
                            break;
                        case InterpTrack track:
                            exportEntry = track.Export;
                            break;
                    }
                }

                var packEd = new PackageEditorWindow();
                packEd.Show();
                packEd.LoadFile(Pcc.FilePath, exportEntry.UIndex);
            }
        }

        private void LoadGroups()
        {
            ResetView();
            InterpGroups.ClearEx();
            var groupsProp = CurrentLoadedExport?.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
            if (groupsProp != null)
            {
                var groupExports = groupsProp.Where(prop => Pcc.IsUExport(prop.Value)).Select(prop => Pcc.GetUExport(prop.Value));
                InterpGroups.AddRange(groupExports.Select(exp => new InterpGroup(exp)));
            }

            int? strRef = InterpGroups.Select(g => g.TryGetStrRefId()).FirstOrDefault(id => id != null);
            if (strRef != null)
            {
                var me1PackageOrNull = CurrentLoadedExport?.Game.IsGame1() ?? false ? CurrentLoadedExport?.FileRef : null;
                LineStrRef = TLKManagerWPF.GlobalFindStrRefbyID(strRef.GetValueOrDefault(), CurrentLoadedExport?.Game ?? MEGame.ME3, me1PackageOrNull);
            }
            else LineStrRef = "";
        }

        public void RefreshInterpData(ExportEntry changedExport, PackageChange change)
        {
            //var selection = MatineeTree.SelectedItem;
            if (changedExport.ClassName is "InterpGroup" or "InterpGroupDirector")
            {
                if (change is PackageChange.ExportAdd)
                {
                    InterpGroups.Add(new InterpGroup(changedExport));
                }
                else if (InterpGroups.FirstOrDefault(g => g.Export == changedExport) is InterpGroup group)
                {
                    int idx = InterpGroups.IndexOf(group);
                    InterpGroups.RemoveAt(idx);
                    var newGroup = new InterpGroup(changedExport)
                    {
                        IsExpanded = group.IsExpanded,
                        IsSelected = group.IsSelected
                    };
                    InterpGroups.Insert(idx, newGroup);
                    var strRef = group.TryGetStrRefId();
                    if (strRef != null)
                    {
                        LineStrRef = TLKManagerWPF.GlobalFindStrRefbyID(strRef.Value, CurrentLoadedExport.Game);
                    }
                }
                else
                {
                    LoadGroups();
                }
            }
            else
            {
                foreach (InterpGroup interpGroup in InterpGroups)
                {
                    if (changedExport.Parent == interpGroup.Export)
                    {
                        // export is a child of this group
                        if (interpGroup.Tracks.FirstOrDefault(x => x.Export == changedExport) is InterpTrack track)
                        {
                            track.LoadTrack(); //reload
                        }
                        else
                        {
                            interpGroup.RefreshTracks();
                        }
                        break;
                    }
                }
            }

            //if (selection is InterpTrack strk) //Reselect item post edit
            //{
            //    foreach (var grp in InterpGroups)
            //    {
            //        foreach (var trk in grp.Tracks)
            //        {
            //            if (trk.Export.UIndex == strk.Export.UIndex)
            //            {
            //                MatineeTree.SelectItem(trk); //???
            //                return;
            //            }
            //        }
            //    }
            //}
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            switch (e.NewValue)
            {
                case InterpGroup group:
                    SelectionChanged?.Invoke(group.Export);
                    break;
                case InterpTrack track:
                    SelectionChanged?.Invoke(track.Export);
                    break;
            }
        }

        private void ResetView()
        {
            Scale = 100.0;
            Offset = 1.0;
            DrawGuideLines();
        }

        private void DrawGuideLines(bool autoSize = false)
        {
            double ToPosition(double time) => (time + Offset) * Scale;
            double ToTime(double position) => position / Scale - Offset;

            Guide.Children.Clear();

            if (autoSize && InterpGroups.Any(group => group.Tracks.Any(track => track.Keys.Any())))
            {
                double firstKey = InterpGroups.Min(group => group.Tracks.Min(track => track.Keys.FirstOrDefault()?.Time ?? float.MaxValue));
                double LastKey = InterpGroups.Max(group => group.Tracks.Max(track => track.Keys.LastOrDefault()?.Time ?? 0));
                double timeSpan = LastKey - firstKey;
                timeSpan = timeSpan > 0 ? timeSpan : 2;
            }

            double lineSpacing = 10;
            if (Scale > 1400)
            {
                lineSpacing = 0.05;
            }
            else if (Scale > 400)
            {
                lineSpacing = 0.1;
            }
            else if (Scale > 200)
            {
                lineSpacing = 0.25;
            }
            else if (Scale > 110)
            {
                lineSpacing = 0.5;
            }
            else if (Scale > 35)
            {
                lineSpacing = 1;
            }
            else if (Scale > 7)
            {
                lineSpacing = 5;
            }
            int numLines = (int)Math.Ceiling(Guide.ActualWidth / Scale / lineSpacing) + 1;
            double firstLinePos = (Math.Ceiling(ToTime(0) / lineSpacing) - 1) * lineSpacing;
            for (int i = 0; i < numLines; i++)
            {
                double linepos = firstLinePos + lineSpacing * i;
                var line = new Line();
                Canvas.SetLeft(line, ToPosition(linepos));
                Guide.Children.Add(line);

                var label = new Label();
                Canvas.SetLeft(label, ToPosition(linepos));
                Canvas.SetBottom(label, 0);
                label.Content = linepos.ToString("0.00");
                Guide.Children.Add(label);
            }
        }

        #region Scrolling and Dragging

        private void OnScroll(object sender, MouseWheelEventArgs e)
        {
            double xPos = e.GetPosition(Guide).X / Scale;
            double initialWidth = Guide.ActualWidth / Scale;
            Scale *= 1 + e.Delta / 1000.0;

            //Math here is to make zooming centered on the mouse

            double xPercent = xPos / initialWidth;
            double widthDiff = initialWidth - (Guide.ActualWidth / Scale);
            double zoomRelativeToMouseDiff = (xPercent - 0.5) * widthDiff;
            Offset -= widthDiff / 2 + zoomRelativeToMouseDiff;
            DrawGuideLines();
            e.Handled = true;
        }

        private bool dragging;
        private Point dragPos;

        private void Guide_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //not a right click
            if (e.RightButton == MouseButtonState.Released)
            {
                dragging = true;
                dragPos = e.GetPosition(Guide);
            }
        }

        private void Guide_OnPreviewMouseUp(object sender, MouseButtonEventArgs e) => dragging = false;

        private void Guide_OnMouseLeave(object sender, MouseEventArgs e) => dragging = false;

        private void Guide_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point newPos = e.GetPosition(Guide);
                double xDiff = newPos.X - dragPos.X;
                Offset += xDiff / Scale;
                dragPos = newPos;
                DrawGuideLines();
            }
        }

        #endregion

        private void Timeline_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGuideLines();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new Timeline(), CurrentLoadedExport)
                {
                    Title = $"InterpData Timeline - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            UnloadExport();
        }
    }
}
