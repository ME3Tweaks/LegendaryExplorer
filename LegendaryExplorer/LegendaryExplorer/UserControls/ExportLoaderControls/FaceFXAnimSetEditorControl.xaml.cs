using ClosedXML.Excel;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorer.UserControls.SharedToolControls.Curves;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Point = System.Windows.Point;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for FaceFXAnimSetEditorControl.xaml
    /// </summary>
    public partial class FaceFXAnimSetEditorControl : ExportLoaderControl
    {
        public class FaceFXEditorTreeNode
        {
            /// <summary>
            /// Text to show
            /// </summary>
            public string Header { get; set; }

            /// <summary>
            /// Delegate to invoke on double click
            /// </summary>
            public Action DoubleClickAction { get; set; }
        }

        public ObservableCollectionExtended<FaceFXEditorTreeNode> TreeNodes { get; } = new();

        #region Single Line Mode
        public bool SingleLineMode
        {
            get => (bool)GetValue(SingleLineModeProperty);
            set => SetValue(SingleLineModeProperty, value);
        }
        public static readonly DependencyProperty SingleLineModeProperty = DependencyProperty.Register(
            nameof(SingleLineMode), typeof(bool), typeof(FaceFXAnimSetEditorControl), new PropertyMetadata(default(bool), SingleLineModeChanged));

        private static void SingleLineModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FaceFXAnimSetEditorControl editor)
            {
                if ((bool)e.NewValue)
                {
                    editor.linesColumnDef.Width = new GridLength(0);
                    editor.linesSplitterColumnDef.Width = new GridLength(0);
                    editor.lineTextRowDef.Height = new GridLength(0);
                }
                else
                {
                    editor.linesColumnDef.Width = new GridLength(310);
                    editor.linesSplitterColumnDef.Width = new GridLength(5);
                    editor.lineTextRowDef.Height = new GridLength(40);
                }
            }
        }
        #endregion

        public FaceFXAnimSetEditorControl() : base("FaceFXAnimSetEditor")
        {
            InitializeComponent();
            DataContext = this;
            AddKeyWithZeroWeightCommand = new GenericCommand(() => graph.AddKeyAtZero_MousePosition());
        }

        public IFaceFXBinary FaceFX;

        public ObservableCollectionExtended<FaceFXLineEntry> Lines { get; } = new();

        FaceFXLineEntry _selectedLineEntry;

        public FaceFXLineEntry SelectedLineEntry
        {
            get => _selectedLineEntry;
            set
            {
                if (SetProperty(ref _selectedLineEntry, value) && _selectedLineEntry != null)
                {
                    SelectedLineEntry.UpdateLength();
                    UpdateAnimListBox();
                    UpdateAudioPlayer();
                    UpdateTreeItems(FaceFX, SelectedLineEntry.Line);
                }
            }
        }

        private void UpdateAudioPlayer()
        {
            if (SelectedLineEntry == null)
            {
                audioPlayer.StopPlaying();
                audioPlayer.UnloadExport();
                return;
            }
            // Find voice line in file
            if (CurrentLoadedExport.Game.IsGame2() || CurrentLoadedExport.Game.IsGame3())
            {
                var audioExport = FindVoiceStreamFromExport(SelectedLineEntry);
                if (audioExport != null)
                {
                    audioPlayer.LoadExport(audioExport);
                }
            }
            else if (CurrentLoadedExport.Game.IsGame1())
            {
                // I'm not entirely sure how we would load this given that it's in an ISB and we don't really know how they're linked...
            }
        }

        public FaceFXLine SelectedLine => SelectedLineEntry?.Line;

        /// <summary>
        /// The extra playhead position line in the curve graph
        /// </summary>
        private readonly ExtraCurveGraphLine PlayheadPositionLine = new() { Label = "Playhead", LabelOffset = 15, Color = new SolidColorBrush(Colors.Aqua) };

        public ObservableCollectionExtended<Animation> Animations { get; } = new();

        Animation _selectedAnimation;
        public Animation SelectedAnimation
        {
            get => _selectedAnimation;
            set => SetProperty(ref _selectedAnimation, value);
        }

        Animation _referenceAnimation;
        public Animation ReferenceAnimation
        {
            get => _referenceAnimation;
            set
            {
                if (_referenceAnimation != null) _referenceAnimation.IsReferenceAnim = false;
                SetProperty(ref _referenceAnimation, value);
                if (_referenceAnimation != null) _referenceAnimation.IsReferenceAnim = true;
                graph.ComparisonCurve = _referenceAnimation?.ToCurve(SaveChanges);
                graph.Paint();
            }
        }

        public ICommand AddKeyWithZeroWeightCommand { get; set; }
        #region ExportLoaderControl

        public override bool CanParse(ExportEntry exportEntry) => (exportEntry.ClassName == "FaceFXAnimSet" || (exportEntry.ClassName == "FaceFXAsset" && exportEntry.Game != MEGame.ME2)) && !exportEntry.IsDefaultObject;

        public override void LoadExport(ExportEntry exportEntry)
        {
            if (CurrentLoadedExport != exportEntry || !IsKeyboardFocusWithin)
            {
                UnloadExport();
                CurrentLoadedExport = exportEntry;
                LoadFaceFXAnimset();
            }
        }

        public override void UnloadExport()
        {
            audioPlayer?.StopPlaying();
            audioPlayer?.UnloadExport();
            CurrentLoadedExport = null;
            FaceFX = null;
            Lines.Clear();
            Animations.Clear();
            TreeNodes.ClearEx();
            graph.Clear();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new FaceFXAnimSetEditorControl(), CurrentLoadedExport)
                {
                    Title = $"FaceFX Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            UnloadExport();
            graph.Dispose();
            audioPlayer?.Dispose();
        }

        #endregion

        private void LoadFaceFXAnimset()
        {
            Lines.Clear();
            switch (CurrentLoadedExport.ClassName)
            {
                case "FaceFXAnimSet":
                    FaceFX = new FaceFXAnimSetHandler(CurrentLoadedExport);
                    break;
                case "FaceFXAsset":
                    FaceFX = new FaceFXAssetHandler(CurrentLoadedExport);
                    break;

            }
            foreach (var faceFXLine in FaceFX.Lines)
            {
                var LineEntry = new FaceFXLineEntry(faceFXLine);
                var idStr = LineEntry.Line.ID;
                var voPos = idStr.IndexOf("VO_", StringComparison.Ordinal);
                bool isFemale = idStr.EndsWith("_F") || LineEntry.Line.NameAsString.EndsWith("_F");
                if (voPos > 0)
                {
                    // Cut off the start of the string
                    idStr = idStr.Substring(voPos + 3);

                    idStr = idStr.TrimEnd('M', 'F').TrimEnd('_'); // Hack
                }
                LineEntry.IsMale = !isFemale;
                if (int.TryParse(idStr, out int tlkID))
                {
                    LineEntry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                    LineEntry.TLKID = tlkID;
                }
                Lines.Add(LineEntry);
            }
            graph.Clear();
        }

        private ExportEntry FindVoiceStreamFromExport(FaceFXLineEntry selectedLine)
        {
            if (CurrentLoadedExport != null && selectedLine.TLKID > 0)
            {
                var wwiseEventSearchName = $"VO_{selectedLine.TLKID:D6}_{(selectedLine.IsMale ? "m" : "f")}";
                var wwiseStreamSearchName = $"{selectedLine.TLKID:D8}";
                var wwiseStreamSearchNameGendered = $"{wwiseStreamSearchName}_{(selectedLine.IsMale ? "m" : "f")}";
                var wwiseEventExp = CurrentLoadedExport.FileRef.Exports.FirstOrDefault(x => x.ClassName == "WwiseEvent" && x.ObjectName.Name.Contains(wwiseEventSearchName, StringComparison.InvariantCultureIgnoreCase));
                if (wwiseEventExp != null)
                {
                    ExportEntry possible;
                    var wwiseEvent = ObjectBinary.From<WwiseEvent>(wwiseEventExp);
                    if (wwiseEvent.Links != null)
                    {
                        foreach (var link in wwiseEvent.Links)
                        {
                            // Look through these exports instead of all exports (faster)
                            var possibleExports = link.WwiseStreams.Where(x => CurrentLoadedExport.FileRef.IsUExport(x)).Select(x => CurrentLoadedExport.FileRef.GetUExport(x)).ToList();

                            //Do gendered search first
                            possible = possibleExports.FirstOrDefault(x => x.ObjectName.Name.Contains(wwiseStreamSearchNameGendered, StringComparison.InvariantCultureIgnoreCase));
                            if (possible != null) return possible;

                            // Fallback to non-gendered search. Sometimes if line has same thing (e.g. nonplayer line) it'll just use male version as there's only one gender
                            // Should only be one version for this TLK...
                            possible = possibleExports.FirstOrDefault(x => x.ObjectName.Name.Contains(wwiseStreamSearchName, StringComparison.InvariantCultureIgnoreCase));
                            if (possible != null)
                                return possible;
                        }
                    }
                    else
                    {
                        // Look through all the exports I guess.
                        var possibleExports = CurrentLoadedExport.FileRef.Exports.Where(x => x.ClassName == "WwiseStream").ToList();

                        //Do gendered search first
                        possible = possibleExports.FirstOrDefault(x => x.ObjectName.Name.Contains(wwiseStreamSearchNameGendered, StringComparison.InvariantCultureIgnoreCase));
                        if (possible != null) return possible;

                        // Fallback to non-gendered search. Sometimes if line has same thing (e.g. nonplayer line) it'll just use male version as there's only one gender
                        // Should only be one version for this TLK...
                        possible = possibleExports.FirstOrDefault(x => x.ObjectName.Name.Contains(wwiseStreamSearchName, StringComparison.InvariantCultureIgnoreCase));
                        if (possible != null)
                            return possible;
                    }
                }
            }
            return null;
        }

        private void animationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedAnimation != null)
            {
                graph.SelectedCurve = SelectedAnimation.ToCurve(SaveChanges);
                graph.Paint(true);
            }
        }

        private void UpdateAnimListBox()
        {
            List<CurvePoint> points = SelectedLine.Points.Select(p => new CurvePoint(p.time, p.weight, p.inTangent, p.leaveTangent)).ToList();

            Animations.Clear();
            SelectedAnimation = null;
            int pos = 0;
            for (int i = 0; i < SelectedLine.AnimationNames.Count; i++)
            {
                int animLength = SelectedLine.NumKeys[i];
                Animations.Add(new Animation
                {
                    Name = FaceFX.Names[SelectedLine.AnimationNames[i]],
                    Points = new LinkedList<CurvePoint>(points.Skip(pos).Take(animLength))
                });
                pos += animLength;
            }
            graph.Clear();
        }

        public void SaveChanges()
        {
            if (SelectedLine != null && animationListBox.ItemsSource != null)
            {
                var curvePoints = new List<CurvePoint>();
                var numKeys = new List<int>();
                var animationNames = new List<int>();
                foreach (Animation anim in Animations)
                {
                    animationNames.Add(FaceFX.Names.FindOrAdd(anim.Name));
                    curvePoints.AddRange(anim.Points);
                    numKeys.Add(anim.Points.Count);
                }
                SelectedLine.AnimationNames = animationNames;
                SelectedLine.Points = curvePoints.Select(x => new FaceFXControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }).ToList();
                SelectedLine.NumKeys = numKeys;
                SelectedLineEntry.UpdateLength();
            }
            CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
        }

        public void SelectLineByName(string name)
        {
            if (FaceFX != null)
            {
                foreach (var line in Lines)
                {
                    if (line.Line.NameAsString == name)
                    {
                        SelectedLineEntry = line;
                        linesListBox.ScrollIntoView(line);
                        break;
                    }
                }
            }
        }

        private Point dragStart;
        private bool dragEnabled;

        #region Line dragging
        private struct FaceFXLineDragDropObject
        {
            public FaceFXLine line;
            public string[] sourceNames;
            public string fromExport;
        }

        private void linesListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragEnabled = false;
        }

        private void linesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(linesListBox);
            dragEnabled = true;
        }

        private void linesListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !dragStart.Equals(new Point(0, 0)) && dragEnabled)
            {
                Vector diff = dragStart - e.GetPosition(linesListBox);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    dragStart = new Point(0, 0);
                    if (!(e.OriginalSource is ScrollViewer) && SelectedLine != null)
                    {
                        FaceFXLine d = SelectedLine.Clone();
                        var dragDropObject = new FaceFXLineDragDropObject { line = d, sourceNames = FaceFX.Names.ToArray(), fromExport = CurrentLoadedExport.InstancedFullPath };
                        DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXLine", dragDropObject), DragDropEffects.Copy);
                    }
                }
            }
        }

        private void linesListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FaceFXLine"))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void linesListBox_Drop(object sender, DragEventArgs e)
        {
            Window.GetWindow(this).RestoreAndBringToFront();
            if (e.Data.GetDataPresent("FaceFXLine") && e.Data.GetData("FaceFXLine") is FaceFXLineDragDropObject d)
            {
                if (CurrentLoadedExport == null || d.fromExport == CurrentLoadedExport.InstancedFullPath) return;

                string[] sourceNames = d.sourceNames;
                FaceFXLineEntry lineEntry = new FaceFXLineEntry(d.line);
                lineEntry.Line.NameIndex = FaceFX.Names.FindOrAdd(sourceNames[lineEntry.Line.NameIndex]);
                if (FaceFX.Binary is FaceFXAnimSet animSet) animSet.FixNodeTable();
                lineEntry.Line.AnimationNames = lineEntry.Line.AnimationNames.Select(idx => FaceFX.Names.FindOrAdd(sourceNames[idx])).ToList();
                lineEntry.Line.Index = FaceFX.Lines.Count;
                FaceFX.Lines.Add(lineEntry.Line);

                if (int.TryParse(lineEntry.Line.ID, out int tlkID))
                {
                    lineEntry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                }

                Lines.Add(lineEntry);
                SaveChanges();
            }
        }

        #endregion

        #region Anim dragging

        private struct FaceFXAnimDragDropObject
        {
            public Animation anim;
            public int group;
            public string fromDlg;
            public string fromAnimset;
        }

        private void animationListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.OriginalSource is TextBlock ? e.GetPosition(null) : default;
        }

        private void animationListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !dragStart.Equals(default))
            {
                Vector diff = dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        dragStart = new Point(0, 0);
                        if (!(e.OriginalSource is ScrollViewer) && SelectedAnimation != null)
                        {
                            Animation a = SelectedAnimation;
                            var dragDropObject = new FaceFXAnimDragDropObject
                            {
                                anim = a,
                                group = SelectedLine.NumKeys[animationListBox.SelectedIndex],
                                fromDlg = SelectedLine.NameAsString,
                                fromAnimset = CurrentLoadedExport.InstancedFullPath
                            };
                            DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXAnim", dragDropObject), DragDropEffects.Copy);
                        }
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        private void animationListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FaceFXAnim"))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void animationListBox_Drop(object sender, DragEventArgs e)
        {
            Window.GetWindow(this).RestoreAndBringToFront();
            if (e.Data.GetDataPresent("FaceFXAnim") && e.Data.GetData("FaceFXAnim") is FaceFXAnimDragDropObject d)
            {
                if (CurrentLoadedExport == null || SelectedLine == null
                    || (d.fromDlg == SelectedLine.NameAsString
                    && d.fromAnimset == CurrentLoadedExport.InstancedFullPath)) return;

                Animations.Add(d.anim);
                FaceFX.Names.FindOrAdd(d.anim.Name);
                SaveChanges();
                UpdateAnimListBox();
            }
        }
        #endregion

        private void DeleteAnim_Click(object sender, RoutedEventArgs e)
        {
            Animations.RemoveAt(animationListBox.SelectedIndex);
            SaveChanges();
        }

        private void DeleteAnimKeys_Click(object sender, RoutedEventArgs e)
        {
            SelectedAnimation.Points = new LinkedList<CurvePoint>();
            SaveChanges();
        }

        private void SelectForCompare_Click(object sender, RoutedEventArgs e)
        {
            ReferenceAnimation = SelectedAnimation;
        }

        private void AddAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLine == null) { return; }

            string name = PromptDialog.Prompt(null, "New animation name");

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Not a valid animation name", "Error", MessageBoxButton.OK);
                return;
            }

            Animation animation = new()
            {
                Name = name,
                Points = new()
            };

            Animations.Add(animation);
            SelectedLine.AnimationNames.Add(FaceFX.Names.FindOrAdd(animation.Name));

            // This writes the animations back to the export, which basically clones the data properly
            SaveChanges();
            UpdateAnimListBox();
        }

        private void CloneAnimation_Click(object sender, RoutedEventArgs e)
        {
            Animations.Add(SelectedAnimation);
            SelectedLine.AnimationNames.Add(FaceFX.Names.FindOrAdd(SelectedAnimation.Name));

            // This writes the animations back to the export, which basically clones the data properly
            SaveChanges();
            UpdateAnimListBox();
        }

        private void ClearLipSyncKeys_Click(object sender, RoutedEventArgs e)
        {
            foreach (var anim in Animations)
            {
                if (anim.Name.StartsWith("m_"))
                {
                    anim.Points = new LinkedList<CurvePoint>();
                }
            }
            SaveChanges();
        }

        private void ClearAnimations_Click(object sender, RoutedEventArgs e)
        {
            Animations.ClearEx();
            SaveChanges();
        }

        private void ReplaceAnimations_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLine == null) { return; }

            string extID = PromptDialog.Prompt(null, "Line ID to copy the animations from:");

            FaceFXLineEntry extLineEntry = Lines.FirstOrDefault(l => l.Line.ID.CaseInsensitiveEquals(extID));
            if (string.IsNullOrEmpty(extID) || extLineEntry == null)
            {
                MessageBox.Show("The Line ID doesn't exist in the loaded FaceFX set", "Error", MessageBoxButton.OK);
                return;
            }

            string filter = ((MenuItem)sender).CommandParameter as string;

            // Save animations if required, otherwise make a new list
            List<Animation> animations = !string.IsNullOrEmpty(filter) ? Animations.Where(a => a.Name.StartsWith(filter)).ToList() : new();
            Animations.Clear();
            Animations.AddRange(animations); // Add back any saved animations

            // Add the external animations
            List<CurvePoint> extPoints = extLineEntry.Points.Select(p => new CurvePoint(p.time, p.weight, p.inTangent, p.leaveTangent)).ToList();
            for (int i = 0, pos = 0; i < extLineEntry.Line.AnimationNames.Count; i++)
            {
                int animLength = extLineEntry.Line.NumKeys[i];
                string animName = FaceFX.Names[extLineEntry.Line.AnimationNames[i]];

                // If a filter is passed, skip animations that contain the filter
                if (!string.IsNullOrEmpty(filter))
                {
                    if (animName.StartsWith(filter))
                    {
                        pos += animLength;
                        continue;
                    }
                }

                Animations.Add(new Animation
                {
                    Name = animName,
                    Points = new LinkedList<CurvePoint>(extPoints.Skip(pos).Take(animLength))
                }); ;

                SelectedLine.AnimationNames.Add(extLineEntry.Line.AnimationNames[i]);

                pos += animLength;
            }

            SaveChanges();
            UpdateAnimListBox();
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            FaceFX.Lines.Remove(SelectedLine);
            Lines.Remove(SelectedLineEntry);
            SaveChanges();
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentLoadedExport == null) { return; }

            bool isNonSpkr = CurrentLoadedExport.ObjectName.Name.Contains("NonSpkr");
            string id = "";
            IEntry audio = null;

            // Get the line name
            string name = PromptDialog.Prompt(null, "New line name");
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Not a valid line name.", "Error", MessageBoxButton.OK);
                return;
            }

            // PREPARE THE REQUIRED ELEMENTS
            if (!isNonSpkr)
            {
                // Get the SoundCue or WwiseEvent export
                string prompt = PromptDialog.Prompt(null, $"Export number of the {(Pcc.Game.IsGame1() ? "SoundCue" : "WwiseEvent")} to reference:", "Audio reference");
                if (string.IsNullOrEmpty(prompt) || !int.TryParse(prompt, out int intPrompt) || intPrompt < 1 || !Pcc.TryGetEntry(intPrompt, out audio))
                {
                    MessageBox.Show("Invalid export number.", "Error", MessageBoxButton.OK);
                    return;
                }

                // Get the SoundNodeWave for ME1/LE1, as it uses a different convention for the line ID
                if (Pcc.Game.IsGame1())
                {
                    string nodeWavePrompt = PromptDialog.Prompt(null, $"Export number of the SoundNodeWave that references the SoundCue:", "SoundNodeWave reference");
                    if (string.IsNullOrEmpty(nodeWavePrompt) || !int.TryParse(nodeWavePrompt, out int expNum) || expNum < 1 || !Pcc.TryGetEntry(expNum, out IEntry soundNodeWave))
                    {
                        MessageBox.Show("Invalid export number.", "Error", MessageBoxButton.OK);
                        return;
                    }

                    id = soundNodeWave.ObjectName.Name;
                }
                else
                {
                    // Extract the StrRefID
                    id = name.Replace("FXA_", "", StringComparison.CurrentCultureIgnoreCase)
                        .Replace("_M", "", StringComparison.CurrentCultureIgnoreCase)
                        .Replace("_F", "", StringComparison.CurrentCultureIgnoreCase);
                }
            }

            int nameIdx = FaceFX.Names.FindIndex(l => string.Equals(l, name, StringComparison.CurrentCultureIgnoreCase));

            // CREATE THE LINE
            FaceFXLine line;
            if (!isNonSpkr)
            {
                line = new()
                {
                    NameIndex = nameIdx < 0 ? FaceFX.Names.Count : nameIdx,
                    NameAsString = name,
                    AnimationNames = new(),
                    Points = new(),
                    NumKeys = new(),
                    FadeInTime = 0.16F,
                    FadeOutTime = 0.22F,
                    Path = audio.InstancedFullPath,
                    ID = id,
                    Index = FaceFX.Lines.Count
                };
            }
            else
            {
                line = new()
                {
                    NameIndex = nameIdx < 0 ? FaceFX.Names.Count : nameIdx,
                    NameAsString = name,
                    AnimationNames = new(),
                    Points = new(),
                    NumKeys = new(),
                    FadeInTime = 0.16F,
                    FadeOutTime = 0.22F,
                    Path = "",
                    ID = "",
                    Index = -1
                };
            }
            FaceFXLineEntry entry = new(line);

            // CREATE ELEMENTS REQUIRING THE LINE
            if (!isNonSpkr)
            {
                ArrayProperty<ObjectProperty> referencedSoundCues = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues")
                    ?? new ArrayProperty<ObjectProperty>("ReferencedSoundCues");
                referencedSoundCues.Add(new ObjectProperty(audio.UIndex));
                CurrentLoadedExport.WriteProperty(referencedSoundCues);

                if (int.TryParse(entry.Line.ID, out int tlkID))
                {
                    entry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                }
            }

            if (nameIdx < 0) { FaceFX.Names.Add(line.NameAsString); }
            FaceFX.Lines.Add(line);
            Lines.Add(entry);
            SaveChanges();
        }

        private void CloneLine_Click(object sender, RoutedEventArgs e)
        {
            // HenBagle: We don't need to do anything with names here because we're cloning within the same file
            var newEntry = new FaceFXLineEntry(SelectedLine.Clone());
            newEntry.Line.Index = FaceFX.Lines.Count;
            FaceFX.Lines.Add(newEntry.Line);

            if (int.TryParse(newEntry.Line.ID, out int tlkID))
            {
                newEntry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
            }

            Lines.Add(newEntry);
        }

        private void SortLines_Click(object sender, RoutedEventArgs e)
        {
            // Sort lines by TLKID (as per Bioware)
            Lines.Sort(l => l.TLKID);
            FaceFX.Lines.Sort((a, b) => int.Parse(a.ID).CompareTo(int.Parse(b.ID)));
            CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
        }

        private void SetPaths_Click(object sender, RoutedEventArgs e)
        {
            // Set paths
            var eventRefs = CurrentLoadedExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
            if (eventRefs == null)
                return;
            foreach (var line in Lines)
            {
                var wwiseevent = Pcc.GetEntry(eventRefs[line.Line.Index].Value);
                if (wwiseevent != null)
                {
                    line.Line.Path = wwiseevent.FullPath;
                }
            }
            SaveChanges();
        }
        private void UpdateTreeItems(IFaceFXBinary animSet, FaceFXLine d)
        {
            TreeNodes.ClearEx();

            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"Name : 0x{d.NameIndex:X8} \"{animSet.Names[d.NameIndex].Trim()}\"", DoubleClickAction = NameDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"FadeInTime : {d.FadeInTime}", DoubleClickAction = FadeInDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"FadeOutTime : {d.FadeOutTime}", DoubleClickAction = FadeOutDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"Path : {d.Path}", DoubleClickAction = PathDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"ID : {d.ID}", DoubleClickAction = IDDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"Index : {d.Index} (0x{d.Index:X8})", DoubleClickAction = IndexDoubleClick });
            TreeNodes.Add(new FaceFXEditorTreeNode() { Header = $"Class : {(animSet.Binary is FaceFXAnimSet ? "FaceFXAnimSet" : "FaceFXAsset")}", DoubleClickAction = null });
        }

        private void IndexDoubleClick()
        {
            var result = PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.Index.ToString(), true);
            if (int.TryParse(result, out int i))
            {
                SelectedLine.Index = i;
            }
        }

        private void PathDoubleClick()
        {
            if (PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.Path, true) is string path)
            {
                SelectedLine.Path = path;
            }
        }

        private void IDDoubleClick()
        {
            if (PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.ID, true) is string id)
            {
                SelectedLine.ID = id;
                if (int.TryParse(id, out int tlkID))
                {
                    SelectedLineEntry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                }
            }
        }

        private void FadeOutDoubleClick()
        {
            var result = PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.FadeInTime.ToString(), true);
            if (float.TryParse(result, out var f))
            {
                SelectedLine.FadeOutTime = f;
            }
        }

        private void FadeInDoubleClick()
        {
            var result = PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.FadeInTime.ToString(), true);
            if (float.TryParse(result, out var f))
            {
                SelectedLine.FadeInTime = f;
            }
        }

        private void Graph_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.Delete or Key.Back)
            {
                graph.DeleteSelectedKey();
            }
        }

        (float start, float end, float span) GetTimeRange()
        {
            string startS = PromptDialog.Prompt(this, "Please enter start time:");
            string endS = PromptDialog.Prompt(this, "Please enter end time:");
            if (!(float.TryParse(startS, out float start) && float.TryParse(endS, out float end)))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return (0, 0, -1);
            }
            float span = end - start;
            if (span <= 0)
            {
                MessageBox.Show("The end time must be after the start time!");
                return (0, 0, -1);
            }
            return (start, end, span);
        }

        private void DelLineSec_Click(object sender, RoutedEventArgs e)
        {
            var (start, end, span) = GetTimeRange();
            if (span < 0)
            {
                return;
            }
            var newPoints = new List<FaceFXControlPoint>();
            for (int i = 0, j = 0; i < SelectedLine.NumKeys.Count; i++)
            {
                int keptPoints = 0;
                for (int k = 0; k < SelectedLine.NumKeys[i]; k++)
                {
                    FaceFXControlPoint tmp = SelectedLine.Points[j + k];
                    if (tmp.time < start)
                    {
                        newPoints.Add(tmp);
                        keptPoints++;
                    }
                    else if (tmp.time > end)
                    {
                        tmp.time -= span;
                        newPoints.Add(tmp);
                        keptPoints++;
                    }
                }
                j += SelectedLine.NumKeys[i];
                SelectedLine.NumKeys[i] = keptPoints;
            }
            SelectedLineEntry.Points = newPoints;
            CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
            UpdateAnimListBox();
        }

        public struct LineSection
        {
            //don't alter capitalization of these fields, since that will break deserialization.
            public float span;
            public Dictionary<string, List<FaceFXControlPoint>> animSecs;
        }

        private void ImpLineSec_Click(object sender, RoutedEventArgs e)
        {
            string startS = PromptDialog.Prompt(this, "Please enter the time to insert at");
            if (!float.TryParse(startS, out float start))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return;
            }
            var ofd = new OpenFileDialog
            {
                Filter = "*.json|*.json",
                CheckFileExists = true,
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (ofd.ShowDialog() == true)
            {
                var lineSec = JsonConvert.DeserializeObject<LineSection>(File.ReadAllText(ofd.FileName));
                var newPoints = new List<FaceFXControlPoint>();
                for (int i = 0, j = 0; i < SelectedLine.AnimationNames.Count; i++)
                {
                    int k = 0;
                    int newNumPoints = 0;
                    FaceFXControlPoint tmp;
                    for (; k < SelectedLine.NumKeys[i]; k++)
                    {
                        tmp = SelectedLine.Points[j + k];
                        if (tmp.time >= start)
                        {
                            break;
                        }
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    string animName = FaceFX.Names[SelectedLine.AnimationNames[i]];
                    if (lineSec.animSecs.TryGetValue(animName, out List<FaceFXControlPoint> points))
                    {
                        newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                        newNumPoints += points.Count;
                        lineSec.animSecs.Remove(animName);
                    }
                    for (; k < SelectedLine.NumKeys[i]; k++)
                    {
                        tmp = SelectedLine.Points[j + k];
                        tmp.time += lineSec.span;
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    j += SelectedLine.NumKeys[i];
                    SelectedLine.NumKeys[i] = newNumPoints;
                }
                //if the line we are importing from had more animations than this one, we need to add some animations
                if (lineSec.animSecs.Count > 0)
                {
                    foreach ((string name, List<FaceFXControlPoint> points) in lineSec.animSecs)
                    {
                        SelectedLine.AnimationNames.Add(FaceFX.Names.FindOrAdd(name));
                        SelectedLine.NumKeys.Add(points.Count);
                        newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                    }
                }
                SelectedLineEntry.Points = newPoints;
                CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
                UpdateAnimListBox();
            }
        }

        private void ExpLineSec_Click(object sender, RoutedEventArgs e)
        {
            var (start, end, span) = GetTimeRange();
            if (span < 0)
            {
                return;
            }
            var animSecs = new Dictionary<string, List<FaceFXControlPoint>>();
            for (int i = 0, j = 0; i < SelectedLine.AnimationNames.Count; i++)
            {
                var points = new List<FaceFXControlPoint>();
                for (int k = 0; k < SelectedLine.NumKeys[i]; k++)
                {
                    FaceFXControlPoint tmp = SelectedLine.Points[j + k];
                    if (tmp.time >= start && tmp.time <= end)
                    {
                        tmp.time -= start;
                        points.Add(tmp);
                    }
                }
                j += SelectedLine.NumKeys[i];
                animSecs.Add(FaceFX.Names[SelectedLine.AnimationNames[i]], points);
            }
            string output = JsonConvert.SerializeObject(new LineSection { span = span + 0.01f, animSecs = animSecs });
            var sfd = new SaveFileDialog
            {
                Filter = "*.json|*.json",
                AddExtension = true
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, output);
            }
        }

        private void OffsetKeysAfterTime_Click(object sender, RoutedEventArgs e)
        {
            string startS = PromptDialog.Prompt(this, "Please enter the start time (keys at or after this time will be offset):");
            string offsetS = PromptDialog.Prompt(this, "Please enter offset amount:");
            if (!(float.TryParse(startS, out float start) && float.TryParse(offsetS, out float offset)))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return;
            }
            for (int i = 0, j = 0; i < SelectedLine.NumKeys.Count; i++)
            {
                for (int k = 0; k < SelectedLine.NumKeys[i]; k++)
                {
                    if (k > 0 && (SelectedLine.Points[j + k].time + offset <= SelectedLine.Points[j + k - 1].time))
                    {
                        MessageBox.Show($"Offsetting every key after {start} by {offset} would lead to reordering in at least one animation",
                                        "Cannot Reorder keys", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                j += SelectedLine.NumKeys[i];
            }
            for (int i = 0; i < SelectedLine.Points.Count; i++)
            {
                if (SelectedLine.Points[i].time >= start)
                {
                    var tmp = SelectedLine.Points[i];
                    tmp.time += offset;
                    SelectedLine.Points[i] = tmp;
                }
            }
            CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
            UpdateAnimListBox();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            var curve = graph.SelectedCurve;
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Curve");
            //Setup XL
            worksheet.Cell(1, 1).Value = "Time";
            worksheet.Cell(1, 2).Value = curve.Name;
            int xlrow = 1;
            //write data to list
            foreach (var point in curve.CurvePoints)
            {
                xlrow++;
                float time = point.InVal;
                float value = point.OutVal;
                worksheet.Cell(xlrow, 1).Value = point.InVal;
                worksheet.Cell(xlrow, 2).Value = point.OutVal;
            }

            CommonSaveFileDialog m = new CommonSaveFileDialog
            {
                Title = "Select excel output",
                DefaultFileName = $"{SelectedLine.NameAsString}_{curve.Name}.xlsx",
                DefaultExtension = "xlsx",
            };
            m.Filters.Add(new CommonFileDialogFilter("Excel Files", "*.xlsx"));
            var owner = Window.GetWindow(this);
            if (m.ShowDialog(owner) == CommonFileDialogResult.Ok)
            {
                owner.RestoreAndBringToFront();
                try
                {
                    workbook.SaveAs(m.FileName);
                    MessageBox.Show($"Curve exported to {System.IO.Path.GetFileName(m.FileName)}.");
                }
                catch
                {
                    MessageBox.Show($"Save to {System.IO.Path.GetFileName(m.FileName)} failed.\nCheck the excel file is not open.");
                }
            }
        }

        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            var wdlg = MessageBox.Show("Do you want to import a new curve from Excel and overwrite the existing curve values?\n \nThe sheet must be in the correct format:\n- Headers must match the overwritten curve\n- All cells must contain a value\n- Time values must be ordered.\n- Values only, no links or formulas", "Import Curves", MessageBoxButton.OKCancel);
            if (wdlg == MessageBoxResult.Cancel)
                return;

            var curve = graph.SelectedCurve;
            var oDlg = new OpenFileDialog //Load Excel
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Import Excel table",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };

            if (oDlg.ShowDialog() != true)
                return;

            var Workbook = new XLWorkbook(oDlg.FileName);
            IXLWorksheet iWorksheet;
            if (Workbook.Worksheets.Count() > 1)
            {
                try
                {
                    iWorksheet = Workbook.Worksheet(1);
                }
                catch
                {
                    MessageBox.Show("Curve Sheet not found");
                    return;
                }
            }
            else
            {
                iWorksheet = Workbook.Worksheet(1);
            }

            try
            {
                var xlrowCount = iWorksheet.RowsUsed().Count();
                //Check headers
                var returned = (string)iWorksheet.Cell(1, 2).Value; //2 as XL starts at 1, and skip time column
                if (SelectedAnimation.Name != returned)
                {
                    var chkbx = MessageBox.Show("The imported column header does not match current selection.\n", "Import Curves", MessageBoxButton.OK);
                    return;
                }

                //Check time is in order
                float previoustime = -9999;
                for (int row = 2; row <= xlrowCount; row++)
                {
                    var t = iWorksheet.Cell(row, 1).Value.ToString();
                    if (!float.TryParse(t, out float time) || time < previoustime)
                    {
                        MessageBox.Show("The imported timings are not in order.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                    previoustime = time;
                }
                //CHECK Every cell has a numeric value
                foreach (var cell in iWorksheet.RangeUsed().Cells())
                {
                    if (cell.IsNull() || cell.IsEmpty())
                    {
                        MessageBox.Show("The sheet contains empty cells.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                    if (cell.Address.RowNumber > 1 && !float.TryParse(cell.Value.ToString(), out float f))
                    {
                        MessageBox.Show("The values contain text.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                }

                //Import data to curve
                var newCurvePoints = new LinkedList<CurvePoint>();
                for (int xlrow = 2; xlrow <= xlrowCount; xlrow++) //Get Excel points start at 2 because top contains headers
                {
                    var time = iWorksheet.Cell(xlrow, 1).Value.ToString();
                    var outval = iWorksheet.Cell(xlrow, 2).Value.ToString();
                    if (outval != null && float.TryParse(time, out float t) && float.TryParse(outval, out float v))
                    {
                        var point = new CurvePoint(t, v, 0, 0, EInterpCurveMode.CIM_CurveUser);
                        newCurvePoints.AddLast(point);
                    }
                    else
                    {
                        MessageBox.Show("Data error. Aborted");
                        return;
                    }
                }
                SelectedAnimation.Points.Clear();
                foreach (CurvePoint point in newCurvePoints)
                {
                    SelectedAnimation.Points.AddLast(point);
                }

                graph.SelectedCurve = SelectedAnimation.ToCurve(SaveChanges);
                graph.Paint(true);
                SaveChanges();
            }
            catch (Exception exp)
            {
                MessageBox.Show("Import failed. Check Import data.\n", "Error");
#if DEBUG
                MessageBox.Show($"{exp.FlattenException()}", "Error");
#endif
            }
        }

        public interface IFaceFXBinary
        {
            List<string> Names { get; }
            List<FaceFXLine> Lines { get; }
            ObjectBinary Binary { get; }
        }

        internal class FaceFXAssetHandler : IFaceFXBinary
        {
            private FaceFXAsset Asset;
            public ObjectBinary Binary => Asset;
            public List<string> Names => Asset.Names;
            public List<FaceFXLine> Lines => Asset.Lines;

            public FaceFXAssetHandler(ExportEntry export)
            {
                Asset = export.GetBinaryData<FaceFXAsset>();
            }
        }

        internal class FaceFXAnimSetHandler : IFaceFXBinary
        {
            private FaceFXAnimSet AnimSet;
            public ObjectBinary Binary => AnimSet;
            public List<string> Names => AnimSet.Names;
            public List<FaceFXLine> Lines => AnimSet.Lines;

            public FaceFXAnimSetHandler(ExportEntry export)
            {
                AnimSet = export.GetBinaryData<FaceFXAnimSet>();
            }
        }

        private void FaceFXAnimSetEditorControl_OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Called when the control is no longer visible.
            graph?.ExtraXLines.Remove(PlayheadPositionLine);

            if (audioPlayer != null)
                audioPlayer.SeekbarPositionChanged -= AudioPositionChanged;
            audioPlayer?.StopPlaying();
        }

        private void FaceFXAnimSetEditorControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (graph != null && !graph.ExtraXLines.Contains(PlayheadPositionLine))
            {
                graph.ExtraXLines.Add(PlayheadPositionLine);
            }

            if (audioPlayer != null)
                audioPlayer.SeekbarPositionChanged += AudioPositionChanged;
        }

        private void AudioPositionChanged(object sender, AudioPlayheadEventArgs e)
        {
            PlayheadPositionLine.Position = e.PlayheadTime;
            graph.Paint();
        }

        private void NameDoubleClick()
        {
            var result = PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", FaceFX.Names.ElementAtOrDefault(SelectedLine.NameIndex), true);
            if (result is "" or null)
            {
                return;
            }
            if (FaceFX.Names.Contains(result))
            {
                SelectedLine.NameIndex = FaceFX.Names.IndexOf(result);
                SelectedLine.NameAsString = result;
            }
            else if (MessageBoxResult.Yes == MessageBox.Show($"The names list does not contain the name \"{result}\", do you want to add it?", "", MessageBoxButton.YesNo))
            {
                FaceFX.Names.Add(result);
                SelectedLine.NameIndex = FaceFX.Names.Count - 1;
                SelectedLine.NameAsString = result;
            }
        }

        private void OnTreeItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (treeView.SelectedItem is FaceFXEditorTreeNode ffetvn)
            {
                ffetvn.DoubleClickAction?.Invoke();
            }

            UpdateTreeItems(FaceFX, SelectedLine);
            linesListBox.Focus();
            SaveChanges();
            /*
            var t = treeView.Sele;
            if (t == null)
                return;
            string result;
            float f;
            int subidx = t.Index;
            switch (subidx)
            {
                
                case 3://Path
                    if (PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.Path, true) is string path)
                    {
                        SelectedLine.Path = path;
                        break;
                    }
                    return;
                case 4://ID
                    if (PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.ID, true) is string id)
                    {
                        SelectedLine.ID = id;
                        if (int.TryParse(id, out int tlkID))
                        {
                            SelectedLineEntry.TLKString = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                        }
                        break;
                    }
                    return;
                case 5://index
                    result = PromptDialog.Prompt(this, "Please enter new value", "Legendary Explorer", SelectedLine.Index.ToString(), true);
                    if (int.TryParse(result, out int i))
                    {
                        SelectedLine.Index = i;
                        break;
                    }
                    return;
                default:
                    return;
            }*/

        }

        private void ChangeAnimName_Click(object sender, RoutedEventArgs e)
        {
            if (PromptDialog.Prompt(this, "Enter new name", "Animation Name Change", SelectedAnimation.Name, true) is string newName && newName != "")
            {
                SelectedAnimation.Name = newName;
                SaveChanges();
            }
        }

        private void ImportTracksFromXML_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "*.xml|*.xml",
                CheckFileExists = true,
                CustomPlaces = AppDirectories.GameCustomPlaces
            };
            if (ofd.ShowDialog() == true)
            {
                #region xml import

                var xmlDoc = XElement.Load(ofd.FileName);
                var animations = xmlDoc.Descendants("animation_groups").Descendants("animation_group").Descendants("animation").ToList();
                XElement animationElement;
                if (animations.Count is 0)
                {
                    MessageBox.Show(Window.GetWindow(this), "No animations found in this xml file!");
                    return;
                }
                if (animations.Count > 1)
                {
                    var animNames = animations.Select((x, i) => x.Attribute("name")?.Value ?? i.ToString()).ToList();
                    var chosenName = InputComboBoxDialog.GetValue(this, "Choose which to animation to import.", "Choose Animation", animNames, animNames[0]);
                    if (chosenName is null)
                    {
                        return;
                    }
                    animationElement = animations.Find(x => x.Attribute("name")?.Value == chosenName);
                }
                else
                {
                    animationElement = animations[0];
                }
                var curveNodes = animationElement.Descendants("curves").Descendants();
                var lineSec = new LineSection { animSecs = new Dictionary<string, List<FaceFXControlPoint>>() };
                float firstTime = float.MaxValue;
                float lastTime = float.MinValue;
                foreach (XElement curveNode in curveNodes)
                {
                    string curveName = curveNode.Attribute("name")?.Value;
                    if (curveName is null)
                    {
                        continue;
                    }
                    if (curveNode.Value is string value)
                    {
                        var keys = value.Trim().Split(' ').Select(s =>
                        {
                            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                            {
                                return result;
                            }
                            return 0f;
                        }).ToArray();
                        var points = new List<FaceFXControlPoint>();
                        for (int i = 0; i + 3 < keys.Length; i += 4)
                        {
                            firstTime = MathF.Min(firstTime, keys[i]);
                            lastTime = MathF.Max(firstTime, keys[i]);
                            points.Add(new FaceFXControlPoint
                            {
                                time = keys[i],
                                weight = keys[i + 1],
                                inTangent = keys[i + 2],
                                leaveTangent = keys[i + 3]
                            });
                        }
                        lineSec.animSecs.Add(curveName, points);
                    }
                }
                lineSec.span = MathF.Max(0, lastTime - firstTime);

                #endregion

                var newPoints = new List<FaceFXControlPoint>();
                for (int i = 0, j = 0; i < SelectedLine.AnimationNames.Count; i++)
                {
                    int newNumPoints = 0;
                    string animName = FaceFX.Names[SelectedLine.AnimationNames[i]];
                    if (lineSec.animSecs.TryGetValue(animName, out List<FaceFXControlPoint> points))
                    {
                        newPoints.AddRange(points);
                        newNumPoints += points.Count;
                        lineSec.animSecs.Remove(animName);
                    }
                    else
                    {
                        for (int k = 0; k < SelectedLine.NumKeys[i]; k++)
                        {
                            newPoints.Add(SelectedLine.Points[j + k]);
                            newNumPoints++;
                        }
                    }
                    j += SelectedLine.NumKeys[i];
                    SelectedLine.NumKeys[i] = newNumPoints;
                }
                //add new animations
                if (lineSec.animSecs.Count > 0)
                {
                    foreach ((string name, List<FaceFXControlPoint> points) in lineSec.animSecs)
                    {
                        SelectedLine.AnimationNames.Add(FaceFX.Names.FindOrAdd(name));
                        SelectedLine.NumKeys.Add(points.Count);
                        newPoints.AddRange(points);
                    }
                }
                SelectedLineEntry.Points = newPoints;
                CurrentLoadedExport?.WriteBinary(FaceFX.Binary);
                UpdateAnimListBox();
            }
        }
    }

    public class Animation : NotifyPropertyChangedBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private LinkedList<CurvePoint> _points;
        public LinkedList<CurvePoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        private bool _isReferenceAnim = false;
        public bool IsReferenceAnim
        {
            get => _isReferenceAnim;
            set => SetProperty(ref _isReferenceAnim, value);
        }

        public Curve ToCurve(Action SaveChanges)
        {
            return new Curve(Name, Points) { SaveChanges = SaveChanges };
        }
    }

    /// <summary>
    /// UI wrapper for FaceFXLine
    /// </summary>
    public class FaceFXLineEntry : NotifyPropertyChangedBase
    {
        private FaceFXLine _line;
        public FaceFXLine Line
        {
            get => _line;
            set => SetProperty(ref _line, value);
        }

        private string _tlkString;
        public string TLKString
        {
            get => _tlkString;
            set => SetProperty(ref _tlkString, value);
        }

        private float _length;
        public float Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private string _lengthAsString;
        public string LengthAsString
        {
            get => _lengthAsString;
            set => SetProperty(ref _lengthAsString, value);
        }

        public List<FaceFXControlPoint> Points
        {
            get => Line?.Points;
            set
            {
                if (Line != null)
                {
                    Line.Points = value;
                    OnPropertyChanged(nameof(Line));
                    UpdateLength();
                }
            }
        }

        /// <summary>
        /// The ID of the TLK, which is used for some lookups by name
        /// </summary>
        public int TLKID { get; set; }
        /// <summary>
        /// If the line is _M or _F
        /// </summary>
        public bool IsMale { get; set; }

        public FaceFXLineEntry(FaceFXLine faceFX)
        {
            Line = faceFX;
            UpdateLength();
        }

        public void UpdateLength()
        {
            if (Line == null || Line.Points.Count == 0)
            {
                Length = 0f;
            }
            else
            {
                Length = Line.Points.Max((l) => l.time);
            }
            LengthAsString = TimeSpan.FromSeconds(Length).ToString(@"mm\:ss\:fff");
        }
    }
}
