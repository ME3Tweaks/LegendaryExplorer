using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClosedXML.Excel;
using ME3Explorer.CurveEd;
using ME3Explorer.SharedUI;
using ME3Explorer.TlkManagerNS;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;

namespace ME3Explorer.FaceFX
{
    struct Animation
    {
        public string Name { get; set; }
        public LinkedList<CurvePoint> points;
    }

    /// <summary>
    /// Interaction logic for FaceFXAnimSetEditorControl.xaml
    /// </summary>
    public partial class FaceFXAnimSetEditorControl : ExportLoaderControl
    {

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


        public FaceFXAnimSetEditorControl() : base("FaceFXAnimSetEditor")
        {
            InitializeComponent();
            DataContext = this;
        }

        public FaceFXAnimSet FaceFX;

        FaceFXLine _selectedLine;

        public FaceFXLine SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (SetProperty(ref _selectedLine, value) && _selectedLine != null)
                {
                    animationListBox.ItemsSource = null;
                    lineText.Text = null;
                    updateAnimListBox();
                    if (int.TryParse(SelectedLine.ID, out int tlkID))
                    {
                        lineText.Text = TLKManagerWPF.GlobalFindStrRefbyID(tlkID, Pcc);
                    }
                    treeView.Nodes.Clear();
                    treeView.Nodes.AddRange(DataToTree2(FaceFX, SelectedLine));
                }
            }
        }

        #region ExportLoaderControl

        public override bool CanParse(ExportEntry exportEntry) => exportEntry.ClassName == "FaceFXAnimSet";

        public override void LoadExport(ExportEntry exportEntry)
        {
            if (CurrentLoadedExport != exportEntry || !IsKeyboardFocusWithin)
            {
                UnloadExport();
                CurrentLoadedExport = exportEntry;
                loadFaceFXAnimset();
            }
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            FaceFX = null;
            animationListBox.ItemsSource = null;
            lineText.Text = null;
            treeView.Nodes.Clear();
            graph.Clear();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new FaceFXAnimSetEditorControl(), CurrentLoadedExport)
                {
                    Title = $"FaceFX - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {Pcc.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            if (treeView_WinFormsHost != null)
            {
                if (treeView_WinFormsHost.Child != null)
                {
                    treeView_WinFormsHost.Child.MouseDoubleClick -= treeView_MouseDoubleClick;
                    treeView_WinFormsHost.Child.Dispose();
                    treeView_WinFormsHost.Child = null;
                }
                treeView_WinFormsHost.Dispose();
                treeView_WinFormsHost = null;
            }
        }

        #endregion


        private void loadFaceFXAnimset()
        {
            FaceFX = CurrentLoadedExport.GetBinaryData<FaceFXAnimSet>();
            linesListBox.ItemsSource = null;
            linesListBox.ItemsSource = FaceFX.Lines;
            treeView.Nodes.Clear();
            graph.Clear();
        }

        private void animationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1)
            {
                return;
            }
            Animation a = (Animation)e.AddedItems[0];

            Curve curve = new Curve(a.Name, a.points)
            {
                SaveChanges = SaveChanges
            };
            graph.SelectedCurve = curve;
            graph.Paint(true);
        }

        private void updateAnimListBox()
        {
            List<CurvePoint> points = SelectedLine.Points.Select(p => new CurvePoint(p.time, p.weight, p.inTangent, p.leaveTangent)).ToList();

            var anims = new List<Animation>();
            int pos = 0;
            for (int i = 0; i < SelectedLine.AnimationNames.Count; i++)
            {
                int animLength = SelectedLine.NumKeys[i];
                anims.Add(new Animation
                {
                    Name = FaceFX.Names[SelectedLine.AnimationNames[i]],
                    points = new LinkedList<CurvePoint>(points.Skip(pos).Take(animLength))
                });
                pos += animLength;
            }
            animationListBox.ItemsSource = anims;
            graph.Clear();
        }

        public void SaveChanges()
        {
            if (SelectedLine != null && animationListBox.ItemsSource != null)
            {
                var curvePoints = new List<CurvePoint>();
                var numKeys = new List<int>();
                var animations = new List<int>();
                foreach (Animation anim in animationListBox.ItemsSource)
                {
                    animations.Add(FaceFX.Names.IndexOf(anim.Name));
                    curvePoints.AddRange(anim.points);
                    numKeys.Add(anim.points.Count);
                }
                SelectedLine.AnimationNames = animations;
                SelectedLine.Points = curvePoints.Select(x => new FaceFXControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }).ToList();
                SelectedLine.NumKeys = numKeys;
            }
            CurrentLoadedExport?.WriteBinary(FaceFX);
        }

        public void SelectLineByName(string name)
        {
            if (FaceFX != null)
            {
                foreach (var line in FaceFX.Lines)
                {
                    if (line.NameAsString == name)
                    {
                        SelectedLine = line;
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
                        DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXLine", new FaceFXLineDragDropObject{ line = d, sourceNames = FaceFX.Names.ToArray() }), DragDropEffects.Copy);
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
                string[] sourceNames = d.sourceNames;
                FaceFXLine line = d.line;
                line.NameIndex = FaceFX.Names.FindOrAdd(sourceNames[line.NameIndex]);
                FaceFX.FixNodeTable();
                line.AnimationNames = line.AnimationNames.Select(idx => FaceFX.Names.FindOrAdd(sourceNames[idx])).ToList();
                FaceFX.Lines.Add(line);
                linesListBox.ItemsSource = null;
                linesListBox.ItemsSource = FaceFX.Lines;
                SaveChanges();
            }
        }

        #endregion

        #region anim dragging

        private struct FaceFXAnimDragDropObject
        {
            public Animation anim;
            public int group;
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
                        if (!(e.OriginalSource is ScrollViewer) && animationListBox.SelectedItem != null)
                        {
                            Animation a = (Animation)animationListBox.SelectedItem;
                            DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXAnim", new FaceFXAnimDragDropObject {anim = a, group = SelectedLine.NumKeys[animationListBox.SelectedIndex]}), DragDropEffects.Copy);
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
                int group = d.group;
                Animation a = d.anim;
                SelectedLine.AnimationNames.Add(FaceFX.Names.FindOrAdd(a.Name));
                SelectedLine.Points.AddRange(a.points.Select(x => new FaceFXControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }));
                SelectedLine.NumKeys.Add(group);
                updateAnimListBox();
            }
            SaveChanges();
        }
        #endregion

        private void DeleteAnim_Click(object sender, RoutedEventArgs e)
        {
            Animation a = (Animation)animationListBox.SelectedItem;
            List<Animation> anims = animationListBox.ItemsSource.Cast<Animation>().ToList();
            anims.Remove(a);
            animationListBox.ItemsSource = anims;
            SaveChanges();
        }

        private void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            FaceFX.Lines.Remove(SelectedLine);
            linesListBox.ItemsSource = null;
            linesListBox.ItemsSource = FaceFX.Lines;
            SaveChanges();
        }

        private void treeView_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)

        {
            var t = treeView.SelectedNode;
            if (t == null)
                return;
            string result;
            float f;
            int subidx = t.Index;
            switch (subidx)
            {
                case 0://Name
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", FaceFX.Names.ElementAtOrDefault(SelectedLine.NameIndex), true);
                    if (result == string.Empty || result is null)
                    {
                        return;
                    }
                    if (FaceFX.Names.Contains(result))
                    {
                        SelectedLine.NameIndex = FaceFX.Names.IndexOf(result);
                        SelectedLine.NameAsString = result;
                        break;
                    }
                    else if (MessageBoxResult.Yes == MessageBox.Show($"The names list does not contain the name \"{result}\", do you want to add it?", "", MessageBoxButton.YesNo))
                    {
                        FaceFX.Names.Add(result);
                        SelectedLine.NameIndex = FaceFX.Names.Count - 1;
                        SelectedLine.NameAsString = result;
                        break;
                    }
                    return;
                case 1://FadeInTime
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.FadeInTime.ToString(), true);
                    if (float.TryParse(result, out f))
                    {
                        SelectedLine.FadeInTime = f;
                        break;
                    }
                    return;
                case 2://FadeInTime
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.FadeOutTime.ToString(), true);
                    if (float.TryParse(result, out f))
                    {
                        SelectedLine.FadeOutTime = f;
                        break;
                    }
                    return;
                case 3://Path
                    if (PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.Path, true) is string path)
                    {
                        SelectedLine.Path = path;
                        break;
                    }
                    return;
                case 4://ID
                    if (PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.ID, true) is string id)
                    {
                        SelectedLine.ID = id;
                        break;
                    }
                    return;
                case 5://index
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.Index.ToString(), true);
                    if (int.TryParse(result, out int i))
                    {
                        SelectedLine.Index = i;
                        break;
                    }
                    return;
                default:
                    return;
            }
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(DataToTree2(FaceFX, SelectedLine));
            linesListBox.Focus();
            SaveChanges();
        }

        static System.Windows.Forms.TreeNode[] DataToTree2(FaceFXAnimSet animSet, FaceFXLine d) =>
            new[]
            {
                new System.Windows.Forms.TreeNode($"Name : 0x{d.NameIndex:X8} \"{animSet.Names[d.NameIndex].Trim()}\""),
                new System.Windows.Forms.TreeNode($"FadeInTime : {d.FadeInTime}"),
                new System.Windows.Forms.TreeNode($"FadeOutTime : {d.FadeOutTime}"),
                new System.Windows.Forms.TreeNode($"Path : {d.Path}"),
                new System.Windows.Forms.TreeNode($"ID : {d.ID}"),
                new System.Windows.Forms.TreeNode($"Index : 0x{d.Index:X8}")
            };

        private void Graph_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                graph.DeleteSelectedKey();
            }
        }

        (float start, float end, float span) getTimeRange()
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
            var (start, end, span) = getTimeRange();
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
            SelectedLine.Points = newPoints;
            CurrentLoadedExport?.WriteBinary(FaceFX);
            updateAnimListBox();
        }

        private struct LineSection
        {
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
                Filter = "*.json|*.json|All Files (*.*)|*.*",
                CheckFileExists = true
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
                SelectedLine.Points = newPoints;
                CurrentLoadedExport?.WriteBinary(FaceFX);
                updateAnimListBox();
            }
        }

        private void ExpLineSec_Click(object sender, RoutedEventArgs e)
        {
            var (start, end, span) = getTimeRange();
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
            CurrentLoadedExport?.WriteBinary(FaceFX);
            updateAnimListBox();
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
    }
}
