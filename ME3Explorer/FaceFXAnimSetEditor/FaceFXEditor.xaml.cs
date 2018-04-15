using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ME3Explorer.FaceFX
{
    /// <summary>
    /// Interaction logic for FaceFXEditor.xaml
    /// </summary>
    public partial class FaceFXEditor : WPFBase
    {
        struct Animation
        {
            public string Name { get; set; }
            public LinkedList<CurvePoint> points;
        }
        
        public List<IExportEntry> animSets;
        public IFaceFXAnimSet FaceFX;
        public ME3FaceFXLine selectedLine;
        private Point dragStart;
        private bool dragEnabled;

        public FaceFXEditor()
        {
            InitializeComponent();
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog{ Filter = "*.pcc|*.pcc"};
            if (d.ShowDialog() == true)
            {
                try
                {
                    LoadMEPackage(d.FileName);
                    if (pcc.Game == MEGame.ME1)
                    {
                        pcc?.Release(wpfWindow: this);
                        pcc = null;
                        throw new FormatException("FaceFXEditor does not work on ME1 files.");
                    }
                    selectedLine = null;
                    FaceFX = null;
                    treeView.Nodes.Clear();
                    linesListBox.ItemsSource = null;
                    animationListBox.ItemsSource = null;
                    graph.Clear();
                    RefreshComboBox();
                }
                catch (Exception ex)
                {
                    pcc?.Release(wpfWindow: this);
                    pcc = null;
                    MessageBox.Show("Error:\n" + ex.Message);
                }
            }
        }

        private void RefreshComboBox()
        {
            var item = FaceFXAnimSetComboBox.SelectedItem as IExportEntry;
            animSets = new List<IExportEntry>();
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].ClassName == "FaceFXAnimSet")
                    animSets.Add(pcc.Exports[i]);
            FaceFXAnimSetComboBox.ItemsSource = animSets;
            FaceFXAnimSetComboBox.SelectedIndex = 0;
            if (animSets.Contains(item))
            {
                FaceFXAnimSetComboBox.SelectedItem = item;
            }
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (pcc != null)
            {
                SaveChanges();
                pcc.save();
                MessageBox.Show("Done!");
            }
        }

        private void Open_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = pcc != null;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            SaveChanges();
            loadFaceFXAnimset();
        }

        private void loadFaceFXAnimset()
        {
            if (pcc.Game == MEGame.ME3)
            {
                FaceFX = new ME3FaceFXAnimSet(pcc, FaceFXAnimSetComboBox.SelectedItem as IExportEntry);
                linesListBox.ItemsSource = FaceFX.Data.Data;
            }
            else
            {
                FaceFX = new ME2FaceFXAnimSet(pcc, FaceFXAnimSetComboBox.SelectedItem as IExportEntry);
                linesListBox.ItemsSource = (FaceFX.Data as ME2DataAnimSetStruct).Data;
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

            Curve curve = new Curve(a.Name, a.points);
            curve.SaveChanges = SaveChanges;
            graph.SelectedCurve = curve;
            graph.Paint(true);
        }

        private void linesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            animationListBox.ItemsSource = null;
            lineText.Text = null;

            if (e.AddedItems.Count != 1)
            {
                return;
            }
            selectedLine = (ME3FaceFXLine)e.AddedItems[0];
            updateAnimListBox();
            if (int.TryParse(selectedLine.ID, out int tlkID))
            {
                if (pcc.Game == MEGame.ME3)
                {
                    lineText.Text = ME3TalkFiles.findDataById(tlkID);
                }
                else
                {
                    lineText.Text = ME2Explorer.ME2TalkFiles.findDataById(tlkID);
                }
            }
            treeView.Nodes.Clear();
            System.Windows.Forms.TreeNode[] treeNodes = FaceFX.DataToTree2(selectedLine);
            treeView.Nodes.AddRange(treeNodes);
        }

        private void updateAnimListBox()
        {
            List<CurvePoint> points = new List<CurvePoint>();

            foreach (var p in selectedLine.points)
            {
                points.Add(new CurvePoint(p.time, p.weight, p.inTangent, p.leaveTangent));
            }
            List<Animation> anims = new List<Animation>();
            int pos = 0;
            int animLength;
            for (int i = 0; i < selectedLine.animations.Length; i++)
            {
                animLength = selectedLine.numKeys[i];
                anims.Add(new Animation
                {
                    Name = FaceFX.Header.Names[selectedLine.animations[i].index],
                    points = new LinkedList<CurvePoint>(points.Skip(pos).Take(animLength))
                });
                pos += animLength;
            }
            animationListBox.ItemsSource = anims;
            graph.Clear();
        }

        private void SaveChanges()
        {
            if (selectedLine != null && animationListBox.ItemsSource != null)
            {
                List<CurvePoint> curvePoints = new List<CurvePoint>();
                List<int> numKeys = new List<int>();
                if (pcc.Game == MEGame.ME3)
                {
                    List<ME3NameRef> animations = new List<ME3NameRef>();
                    foreach (Animation anim in animationListBox.ItemsSource)
                    {
                        animations.Add(new ME3NameRef { index = FaceFX.Header.Names.IndexOf(anim.Name), unk2 = 0 });
                        curvePoints.AddRange(anim.points);
                        numKeys.Add(anim.points.Count);
                    }
                    selectedLine.animations = animations.ToArray();
                }
                else
                {
                    List<ME2NameRef> animations = new List<ME2NameRef>();
                    foreach (Animation anim in animationListBox.ItemsSource)
                    {
                        animations.Add(new ME2NameRef { index = FaceFX.Header.Names.IndexOf(anim.Name), unk2 = 0, unk1 = 1 });
                        curvePoints.AddRange(anim.points);
                        numKeys.Add(anim.points.Count);
                    }
                    selectedLine.animations = animations.ToArray();
                }
                selectedLine.points = curvePoints.Select(x => new ControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }).ToArray();
                selectedLine.numKeys = numKeys.ToArray();
                FaceFX.Save();
            }
        }

        #region Line dragging

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
                    try
                    {
                        dragStart = new Point(0, 0);
                        if (!(e.OriginalSource is ScrollViewer) && linesListBox.SelectedItem != null)
                        {
                            ME3FaceFXLine d = (linesListBox.SelectedItem as ME3FaceFXLine).Clone();
                            DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXLine", new { line = d, sourceNames = FaceFX.Header.Names }), DragDropEffects.Copy);
                        }
                    }
                    catch (Exception)
                    {

                        throw;
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
            this.RestoreAndBringToFront();
            if (e.Data.GetDataPresent("FaceFXLine"))
            {
                dynamic d = e.Data.GetData("FaceFXLine");
                string[] sourceNames = d.sourceNames;
                List<string> names = FaceFX.Header.Names.ToList();
                ME3FaceFXLine line = d.line;
                line.Name = names.FindOrAdd(sourceNames[line.Name]);
                if (pcc.Game == MEGame.ME3)
                {
                    line.animations = line.animations.Select(x => new ME3NameRef
                    {
                        index = names.FindOrAdd(sourceNames[x.index]),
                        unk2 = x.unk2
                    }).ToArray();
                    FaceFX.Data.Data = FaceFX.Data.Data.Concat(line).ToArray();
                    FaceFX.Header.Names = names.ToArray();
                    linesListBox.ItemsSource = FaceFX.Data.Data;
                }
                else
                {
                    if (!(line is ME2FaceFXLine))
                    {
                        MessageBox.Show("Cannot add ME3 FaceFX lines to ME2 FaceFXAnimsets. If you require this feature, plase make an issue on the project's Github page");
                        return;
                    }
                    line.animations = line.animations.Select(x => new ME2NameRef
                    {
                        index = names.FindOrAdd(sourceNames[x.index]),
                        unk2 = x.unk2
                    }).ToArray();
                    ME2DataAnimSetStruct me2DataAnimSetStruct = (FaceFX.Data as ME2DataAnimSetStruct);
                    me2DataAnimSetStruct.Data = me2DataAnimSetStruct.Data.Concat(line as ME2FaceFXLine).ToArray();
                    FaceFX.Header.Names = names.ToArray();
                    linesListBox.ItemsSource = me2DataAnimSetStruct.Data;
                }
            }
            SaveChanges();
        }

        #endregion

        #region anim dragging
        private void animationListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(null);
        }

        private void animationListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !dragStart.Equals(new Point(0,0)))
            {
                System.Windows.Vector diff = dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        dragStart = new Point(0,0);
                        if (!(e.OriginalSource is ScrollViewer) && animationListBox.SelectedItem != null)
                        {
                            Animation a = (Animation)animationListBox.SelectedItem;
                            DragDrop.DoDragDrop(linesListBox, new DataObject("FaceFXAnim", new { anim = a, group = selectedLine.numKeys[animationListBox.SelectedIndex] }), DragDropEffects.Copy);
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
            this.RestoreAndBringToFront();
            if (e.Data.GetDataPresent("FaceFXAnim"))
            {
                dynamic d = e.Data.GetData("FaceFXAnim");
                int group = d.group;
                Animation a = d.anim;
                List<string> names = FaceFX.Header.Names.ToList();
                if (pcc.Game == MEGame.ME3)
                {
                    selectedLine.animations = selectedLine.animations.Concat(new ME3NameRef { index = names.FindOrAdd(a.Name), unk2 = 0 }).ToArray();
                }
                else
                {
                    selectedLine.animations = selectedLine.animations.Concat(new ME2NameRef { index = names.FindOrAdd(a.Name), unk1 = 1 }).ToArray();
                }
                FaceFX.Header.Names = names.ToArray();
                selectedLine.points = selectedLine.points.Concat(a.points.Select(x => new ControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                })).ToArray();
                selectedLine.numKeys = selectedLine.numKeys.Concat(group).ToArray();
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
            ME3FaceFXLine line = (ME3FaceFXLine)linesListBox.SelectedItem;
            if (pcc.Game == MEGame.ME3)
            {
                List<ME3FaceFXLine> lines = FaceFX.Data.Data.ToList();
                lines.Remove(line);
                FaceFX.Data.Data = lines.ToArray();
                linesListBox.ItemsSource = FaceFX.Data.Data; 
            }
            else
            {
                ME2DataAnimSetStruct me2DataAnimSetStruct = (FaceFX.Data as ME2DataAnimSetStruct);
                List<ME2FaceFXLine> lines = me2DataAnimSetStruct.Data.ToList();
                lines.Remove(line as ME2FaceFXLine);
                me2DataAnimSetStruct.Data = lines.ToArray();
                linesListBox.ItemsSource = me2DataAnimSetStruct.Data;
            }
            SaveChanges();
        }

        private void treeView_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)

        {
            var t = treeView.SelectedNode;
            if (t == null)
                return;
            string result; int i; float f = 0;
            int subidx = t.Index;
            switch (subidx)
            {
                case 0://Name
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", FaceFX.Header.Names.ElementAtOrDefault(selectedLine.Name), 0, 0);
                    if (result == string.Empty)
                    {
                        break;
                    }
                    if (FaceFX.Header.Names.Contains(result))
                    {
                        selectedLine.Name = FaceFX.Header.Names.IndexOf(result);
                    }
                    else if (MessageBoxResult.Yes == MessageBox.Show($"The names list does not contain the name \"{result}\", do you want to add it?", "", MessageBoxButton.YesNo))
                    {
                        FaceFX.Header.Names = FaceFX.Header.Names.Concat(result).ToArray();
                        selectedLine.Name = FaceFX.Header.Names.Length - 1;
                    }
                    break;
                case 1://FadeInTime
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.FadeInTime.ToString(), 0, 0);
                    if (float.TryParse(result, out f))
                        selectedLine.FadeInTime = f;
                    break;
                case 2://FadeInTime
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.FadeOutTime.ToString(), 0, 0);
                    if (float.TryParse(result, out f))
                        selectedLine.FadeOutTime = f;
                    break;
                case 3://unk2
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.unk2.ToString(), 0, 0);
                    i = -1;
                    if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                        selectedLine.unk2 = i;
                    break;
                case 4://Path
                    selectedLine.path = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.path, 0, 0);
                    break;
                case 5://ID
                    selectedLine.ID = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.ID, 0, 0);
                    break;
                case 6://unk3
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.index.ToString(), 0, 0);
                    i = -1;
                    if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                        selectedLine.index = i;
                    break;
                default:
                    return;
            }
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(FaceFX.DataToTree2(selectedLine));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                graph.DeleteSelectedKey();
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            List<int> updatedExports = relevantUpdates.Select(x => x.index).ToList();
            if (FaceFX != null && updatedExports.Contains(FaceFX.Export.Index))
            {
                int index = FaceFX.Export.Index;
                //loaded FaceFXAnimset is no longer a FaceFXAnimset
                if (FaceFX.Export.ClassName != "FaceFXAnimSet")
                {
                    selectedLine = null;
                    FaceFX = null;
                    treeView.Nodes.Clear();
                    linesListBox.ItemsSource = null;
                    animationListBox.ItemsSource = null;
                    graph.Clear();
                }
                else if (!this.IsForegroundWindow())
                {
                    loadFaceFXAnimset();
                }
                updatedExports.Remove(index);
            }
            if (updatedExports.Intersect(animSets.Select(x => x.Index)).Any())
            {
                RefreshComboBox();
            }
            else
            {
                foreach (var i in updatedExports)
                {
                    if (pcc.getExport(i).ClassName == "FaceFXAnimSet")
                    {
                        RefreshComboBox();
                        break;
                    }
                }
            }
        }

        (float start, float end, float span) getTimeRange()
        {
            string startS = Microsoft.VisualBasic.Interaction.InputBox("Please enter start time:");
            string endS = Microsoft.VisualBasic.Interaction.InputBox("Please enter end time:");
            if (!(float.TryParse(startS, out float start) && float.TryParse(endS, out float end)))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return (0, 0, -1);
            }
            float span = end - start;
            if(span <= 0)
            {
                MessageBox.Show("The end time must be after the start time!");
                return (0, 0, -1);
            }
            return (start, end, span);
        }

        private void DelLineSec_Click(object sender, RoutedEventArgs e)
        {
            var (start, end, span) = getTimeRange();
            if(span < 0)
            {
                return;
            }
            var newPoints = new List<ControlPoint>();
            ControlPoint tmp;
            int keptPoints;
            for (int i = 0, j = 0; i < selectedLine.numKeys.Length; i++)
            {
                keptPoints = 0;
                for(int k = 0; k < selectedLine.numKeys[i]; k++)
                {
                    tmp = selectedLine.points[j + k];
                    if(tmp.time < start)
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
                j += selectedLine.numKeys[i];
                selectedLine.numKeys[i] = keptPoints;
            }
            selectedLine.points = newPoints.ToArray();
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
            string startS = Microsoft.VisualBasic.Interaction.InputBox("Please enter the time to insert at");
            if (!float.TryParse(startS, out float start))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return;
            }
            var ofd = new OpenFileDialog();
            ofd.Filter = $"*.json|*.json|All Files (*.*)|*.*";
            ofd.CheckFileExists = true;
            if(ofd.ShowDialog() == true)
            {
                var lineSec = JsonConvert.DeserializeObject<LineSection>(File.ReadAllText(ofd.FileName));
                ControlPoint tmp;
                var newPoints = new List<ControlPoint>();
                int newNumPoints;
                string animName;
                for (int i = 0, j = 0, k = 0; i < selectedLine.animations.Length; i++)
                {
                    k = 0;
                    newNumPoints = 0;
                    for (; k < selectedLine.numKeys[i]; k++)
                    {
                        tmp = selectedLine.points[j + k];
                        if (tmp.time >= start)
                        {
                            break;
                        }
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    animName = FaceFX.Header.Names[selectedLine.animations[i].index];
                    if (lineSec.animSecs.TryGetValue(animName, out var points))
                    {
                        newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                        newNumPoints += points.Count;
                        lineSec.animSecs.Remove(animName);
                    }
                    for (; k < selectedLine.numKeys[i]; k++)
                    {
                        tmp = selectedLine.points[j + k];
                        tmp.time += lineSec.span;
                        newPoints.Add(tmp);
                        newNumPoints++;
                    }
                    j += selectedLine.numKeys[i];
                    selectedLine.numKeys[i] = newNumPoints;
                }
                //if the line we are importing from had more animations than this one, we need to add some animations
                if(lineSec.animSecs.Count > 0)
                {
                    var newNumKeys = selectedLine.numKeys.ToList();
                    var newAnims = selectedLine.animations.ToList();
                    List<string> names = FaceFX.Header.Names.ToList();
                    foreach (var animSec in lineSec.animSecs)
                    {
                        if (pcc.Game == MEGame.ME3)
                        {
                            newAnims.Add(new ME3NameRef { index = names.FindOrAdd(animSec.Key), unk2 = 0 });
                        }
                        else
                        {
                            newAnims.Add(new ME2NameRef { index = names.FindOrAdd(animSec.Key), unk1 = 1 });
                        }
                        newNumKeys.Add(animSec.Value.Count);
                        newPoints.AddRange(animSec.Value.Select(p => { p.time += start; return p; }));
                    }
                    selectedLine.animations = newAnims.ToArray();
                    selectedLine.numKeys = newNumKeys.ToArray();
                    FaceFX.Header.Names = names.ToArray();
                }
                selectedLine.points = newPoints.ToArray();
                FaceFX.Save();
                updateAnimListBox();
                return;
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
            ControlPoint tmp;
            List<ControlPoint> points;
            for (int i = 0, j = 0; i < selectedLine.animations.Length; i++)
            {
                points = new List<ControlPoint>();
                for (int k = 0; k < selectedLine.numKeys[i]; k++)
                {
                    tmp = selectedLine.points[j + k];
                    if (tmp.time >= start && tmp.time <= end)
                    {
                        tmp.time -= start;
                        points.Add(tmp);
                    }
                }
                j += selectedLine.numKeys[i];
                animSecs.Add(FaceFX.Header.Names[selectedLine.animations[i].index], points);
            }
            string output = JsonConvert.SerializeObject(new LineSection { span = span + 0.01f, animSecs = animSecs });
            var sfd = new SaveFileDialog
            {
                Filter = $"*.json|*.json",
                AddExtension = true
            };
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, output);
            }
        }

        private void OffsetKeysAfterTime_Click(object sender, RoutedEventArgs e)
        {
            string startS = Microsoft.VisualBasic.Interaction.InputBox("Please enter the start time (keys at or after this time will be offset):");
            string offsetS = Microsoft.VisualBasic.Interaction.InputBox("Please enter offset amount:");
            if (!(float.TryParse(startS, out float start) && float.TryParse(offsetS, out float offset)))
            {
                MessageBox.Show("You must enter two valid time values. For example, 3 and a half seconds would be entered as: 3.5");
                return;
            }
            for (int i = 0, j = 0; i < selectedLine.numKeys.Length; i++)
            {
                for (int k = 0; k < selectedLine.numKeys[i]; k++)
                {
                    if (k > 0 && (selectedLine.points[j + k].time + offset <= selectedLine.points[j + k - 1].time))
                    {
                        MessageBox.Show($"Offsetting every key after {start} by {offset} would lead to reordering " +
                            $"in at least one animation", "Cannot Reorder keys", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }
            for (int i = 0; i < selectedLine.points.Length; i++)
            {
                if (selectedLine.points[i].time >= start)
                {
                    selectedLine.points[i].time += offset;
                }
            }
            FaceFX.Save();
            updateAnimListBox();
        }
    }
}
