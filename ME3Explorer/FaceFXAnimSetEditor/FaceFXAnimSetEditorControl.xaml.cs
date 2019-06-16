using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.CurveEd;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.TlkManagerNS;
using Microsoft.Win32;
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


        public FaceFXAnimSetEditorControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        public IFaceFXAnimSet FaceFX;

        ME3FaceFXLine _selectedLine;

        public ME3FaceFXLine SelectedLine
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
                    treeView.Nodes.AddRange(FaceFX.DataToTree2(SelectedLine));
                }
            }
        }

        #region ExportLoaderControl

        public override bool CanParse(IExportEntry exportEntry) => exportEntry.ClassName == "FaceFXAnimSet";

        public override void LoadExport(IExportEntry exportEntry)
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
                    Title = $"FaceFX - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.GetFullPath}_{CurrentLoadedExport.indexValue} - {Pcc.FileName}"
                };
                elhw.Show();
            }
        }

        public override void Dispose()
        {
            //
        }

        #endregion


        private void loadFaceFXAnimset()
        {
            switch (Pcc.Game)
            {
                case MEGame.ME1:
                    FaceFX = new ME1FaceFXAnimSet(Pcc, CurrentLoadedExport);
                    linesListBox.ItemsSource = FaceFX.Data.Data;
                    break;
                case MEGame.ME2:
                    FaceFX = new ME2FaceFXAnimSet(Pcc, CurrentLoadedExport);
                    linesListBox.ItemsSource = FaceFX.Data.Data;
                    break;
                case MEGame.ME3:
                    FaceFX = new ME3FaceFXAnimSet(Pcc, CurrentLoadedExport);
                    linesListBox.ItemsSource = FaceFX.Data.Data;
                    break;
            }
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
            List<CurvePoint> points = SelectedLine.points.Select(p => new CurvePoint(p.time, p.weight, p.inTangent, p.leaveTangent)).ToList();

            var anims = new List<Animation>();
            int pos = 0;
            for (int i = 0; i < SelectedLine.animations.Length; i++)
            {
                int animLength = SelectedLine.numKeys[i];
                anims.Add(new Animation
                {
                    Name = FaceFX.Header.Names[SelectedLine.animations[i].index],
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
                if (Pcc.Game == MEGame.ME3)
                {
                    var animations = new List<ME3NameRef>();
                    foreach (Animation anim in animationListBox.ItemsSource)
                    {
                        animations.Add(new ME3NameRef { index = FaceFX.Header.Names.IndexOf(anim.Name), unk2 = 0 });
                        curvePoints.AddRange(anim.points);
                        numKeys.Add(anim.points.Count);
                    }
                    SelectedLine.animations = animations.ToArray();
                }
                else
                {
                    var animations = new List<ME2NameRef>();
                    foreach (Animation anim in animationListBox.ItemsSource)
                    {
                        animations.Add(new ME2NameRef { index = FaceFX.Header.Names.IndexOf(anim.Name), unk2 = 0, unk1 = 1 });
                        curvePoints.AddRange(anim.points);
                        numKeys.Add(anim.points.Count);
                    }
                    SelectedLine.animations = animations.ToArray();
                }
                SelectedLine.points = curvePoints.Select(x => new ControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }).ToArray();
                SelectedLine.numKeys = numKeys.ToArray();
                FaceFX.Save();
            }
        }

        public void SelectLineByName(string name)
        {
            if (FaceFX != null)
            {
                foreach (ME3FaceFXLine line in FaceFX.Data.Data)
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
            public ME3FaceFXLine line;
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
                System.Windows.Vector diff = dragStart - e.GetPosition(linesListBox);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    dragStart = new Point(0, 0);
                    if (!(e.OriginalSource is ScrollViewer) && SelectedLine != null)
                    {
                        ME3FaceFXLine d = SelectedLine.Clone();
                        DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXLine", new FaceFXLineDragDropObject{ line = d, sourceNames = FaceFX.Header.Names }), DragDropEffects.Copy);
                    }
                }
            }
        }

        private void linesListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FaceFxLine"))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void linesListBox_Drop(object sender, DragEventArgs e)
        {
            Window.GetWindow(this).RestoreAndBringToFront();
            if (e.Data.GetDataPresent("FaceFXLine"))
            {
                FaceFXLineDragDropObject d = (FaceFXLineDragDropObject)e.Data.GetData("FaceFXLine");
                string[] sourceNames = d.sourceNames;
                List<string> names = FaceFX.Header.Names.ToList();
                ME3FaceFXLine line = d.line;
                line.Name = names.FindOrAdd(sourceNames[line.Name]);
                if (Pcc.Game == MEGame.ME3)
                {
                    ((ME3FaceFXAnimSet)FaceFX).FixNodeTable();
                    line.animations = line.animations.Select(x => new ME3NameRef
                    {
                        index = names.FindOrAdd(sourceNames[x.index]),
                        unk2 = x.unk2
                    }).ToArray();
                    FaceFX.Data.Data = FaceFX.Data.Data.Append(line).ToArray();
                    FaceFX.Header.Names = names.ToArray();
                    linesListBox.ItemsSource = FaceFX.Data.Data;
                }
                else
                {
                    if (!(line is ME2FaceFXLine))
                    {
                        var result = MessageBox.Show("Cannot add ME3 FaceFX lines to ME2 FaceFXAnimsets. " +
                                                     "If you require this feature, please make an issue on the project's Github page. Would you like to go the Github page now?",
                                                     "Feature Not Implemented", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(App.BugReportURL);
                        }
                        return;
                    }
                    line.animations = line.animations.Select(x => new ME2NameRef
                    {
                        index = names.FindOrAdd(sourceNames[x.index]),
                        unk2 = x.unk2
                    }).ToArray();
                    ME2DataAnimSetStruct me2DataAnimSetStruct = (ME2DataAnimSetStruct)FaceFX.Data;
                    me2DataAnimSetStruct.Data = me2DataAnimSetStruct.Data.Append((ME2FaceFXLine)line).ToArray();
                    FaceFX.Header.Names = names.ToArray();
                    linesListBox.ItemsSource = me2DataAnimSetStruct.Data;
                }
            }
            SaveChanges();
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
                System.Windows.Vector diff = dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        dragStart = new Point(0, 0);
                        if (!(e.OriginalSource is ScrollViewer) && animationListBox.SelectedItem != null)
                        {
                            Animation a = (Animation)animationListBox.SelectedItem;
                            DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXAnim", new FaceFXAnimDragDropObject {anim = a, group = SelectedLine.numKeys[animationListBox.SelectedIndex]}), DragDropEffects.Copy);
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
            if (e.Data.GetDataPresent("FaceFXAnim"))
            {
                FaceFXAnimDragDropObject d = (FaceFXAnimDragDropObject)e.Data.GetData("FaceFXAnim");
                int group = d.group;
                Animation a = d.anim;
                List<string> names = FaceFX.Header.Names.ToList();
                if (Pcc.Game == MEGame.ME3)
                {
                    SelectedLine.animations = SelectedLine.animations.Append(new ME3NameRef { index = names.FindOrAdd(a.Name), unk2 = 0 }).ToArray();
                }
                else
                {
                    SelectedLine.animations = SelectedLine.animations.Append(new ME2NameRef { index = names.FindOrAdd(a.Name), unk1 = 1 }).ToArray();
                }
                FaceFX.Header.Names = names.ToArray();
                SelectedLine.points = SelectedLine.points.Concat(a.points.Select(x => new ControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                })).ToArray();
                SelectedLine.numKeys = SelectedLine.numKeys.Append(group).ToArray();
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
            List<ME3FaceFXLine> lines = FaceFX.Data.Data.ToList();
            lines.Remove(SelectedLine);
            FaceFX.Data.Data = lines.ToArray();
            linesListBox.ItemsSource = FaceFX.Data.Data;
            SaveChanges();
        }

        private void treeView_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)

        {
            var t = treeView.SelectedNode;
            if (t == null)
                return;
            string result; int i; float f;
            int subidx = t.Index;
            switch (subidx)
            {
                case 0://Name
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", FaceFX.Header.Names.ElementAtOrDefault(SelectedLine.Name), true);
                    if (result == string.Empty)
                    {
                        break;
                    }
                    if (FaceFX.Header.Names.Contains(result))
                    {
                        SelectedLine.Name = FaceFX.Header.Names.IndexOf(result);
                    }
                    else if (MessageBoxResult.Yes == MessageBox.Show($"The names list does not contain the name \"{result}\", do you want to add it?", "", MessageBoxButton.YesNo))
                    {
                        FaceFX.Header.Names = FaceFX.Header.Names.Append(result).ToArray();
                        SelectedLine.Name = FaceFX.Header.Names.Length - 1;
                    }
                    break;
                case 1://FadeInTime
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.FadeInTime.ToString(), true);
                    if (float.TryParse(result, out f))
                        SelectedLine.FadeInTime = f;
                    break;
                case 2://FadeInTime
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.FadeOutTime.ToString(), true);
                    if (float.TryParse(result, out f))
                        SelectedLine.FadeOutTime = f;
                    break;
                case 3://unk2
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.unk2.ToString(), true);
                    if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                        SelectedLine.unk2 = i;
                    break;
                case 4://Path
                    SelectedLine.path = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.path, true);
                    break;
                case 5://ID
                    SelectedLine.ID = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.ID, true);
                    break;
                case 6://unk3
                    result = PromptDialog.Prompt(this, "Please enter new value", "ME3Explorer", SelectedLine.index.ToString(), true);
                    if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                        SelectedLine.index = i;
                    break;
                default:
                    return;
            }
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(FaceFX.DataToTree2(SelectedLine));
        }

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
            var newPoints = new List<ControlPoint>();
            for (int i = 0, j = 0; i < SelectedLine.numKeys.Length; i++)
            {
                int keptPoints = 0;
                for (int k = 0; k < SelectedLine.numKeys[i]; k++)
                {
                    ControlPoint tmp = SelectedLine.points[j + k];
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
                j += SelectedLine.numKeys[i];
                SelectedLine.numKeys[i] = keptPoints;
            }
            SelectedLine.points = newPoints.ToArray();
            FaceFX.Save();
            updateAnimListBox();
        }

        private struct LineSection
        {
            public float span;
            public Dictionary<string, List<ControlPoint>> animSecs;
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
                var newPoints = new List<ControlPoint>();
                for (int i = 0, j = 0; i < SelectedLine.animations.Length; i++)
                {
                    int k = 0;
                    int newNumPoints = 0;
                    ControlPoint tmp;
                    for (; k < SelectedLine.numKeys[i]; k++)
                    {
                        tmp = SelectedLine.points[j + k];
                        if (tmp.time >= start)
                        {
                            break;
                        }
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    string animName = FaceFX.Header.Names[SelectedLine.animations[i].index];
                    if (lineSec.animSecs.TryGetValue(animName, out List<ControlPoint> points))
                    {
                        newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                        newNumPoints += points.Count;
                        lineSec.animSecs.Remove(animName);
                    }
                    for (; k < SelectedLine.numKeys[i]; k++)
                    {
                        tmp = SelectedLine.points[j + k];
                        tmp.time += lineSec.span;
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    j += SelectedLine.numKeys[i];
                    SelectedLine.numKeys[i] = newNumPoints;
                }
                //if the line we are importing from had more animations than this one, we need to add some animations
                if (lineSec.animSecs.Count > 0)
                {
                    List<int> newNumKeys = SelectedLine.numKeys.ToList();
                    List<ME3NameRef> newAnims = SelectedLine.animations.ToList();
                    List<string> names = FaceFX.Header.Names.ToList();
                    foreach ((string name, List<ControlPoint> points) in lineSec.animSecs)
                    {
                        newAnims.Add(Pcc.Game == MEGame.ME3
                                         ? new ME3NameRef { index = names.FindOrAdd(name), unk2 = 0 }
                                         : new ME2NameRef { index = names.FindOrAdd(name), unk1 = 1 });
                        newNumKeys.Add(points.Count);
                        newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                    }
                    SelectedLine.animations = newAnims.ToArray();
                    SelectedLine.numKeys = newNumKeys.ToArray();
                    FaceFX.Header.Names = names.ToArray();
                }
                SelectedLine.points = newPoints.ToArray();
                FaceFX.Save();
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
            var animSecs = new Dictionary<string, List<ControlPoint>>();
            for (int i = 0, j = 0; i < SelectedLine.animations.Length; i++)
            {
                var points = new List<ControlPoint>();
                for (int k = 0; k < SelectedLine.numKeys[i]; k++)
                {
                    ControlPoint tmp = SelectedLine.points[j + k];
                    if (tmp.time >= start && tmp.time <= end)
                    {
                        tmp.time -= start;
                        points.Add(tmp);
                    }
                }
                j += SelectedLine.numKeys[i];
                animSecs.Add(FaceFX.Header.Names[SelectedLine.animations[i].index], points);
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
            for (int i = 0, j = 0; i < SelectedLine.numKeys.Length; i++)
            {
                for (int k = 0; k < SelectedLine.numKeys[i]; k++)
                {
                    if (k > 0 && (SelectedLine.points[j + k].time + offset <= SelectedLine.points[j + k - 1].time))
                    {
                        MessageBox.Show($"Offsetting every key after {start} by {offset} would lead to reordering in at least one animation",
                                        "Cannot Reorder keys", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }
            for (int i = 0; i < SelectedLine.points.Length; i++)
            {
                if (SelectedLine.points[i].time >= start)
                {
                    SelectedLine.points[i].time += offset;
                }
            }
            FaceFX.Save();
            updateAnimListBox();
        }
    }
}
