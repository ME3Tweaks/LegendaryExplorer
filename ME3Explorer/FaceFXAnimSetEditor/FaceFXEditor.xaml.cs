using System;
using System.Collections.Generic;
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

        public FaceFXEditor()
        {
            InitializeComponent();
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
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
                    animSets = new List<IExportEntry>();
                    for (int i = 0; i < pcc.Exports.Count; i++)
                        if (pcc.Exports[i].ClassName == "FaceFXAnimSet")
                            animSets.Add(pcc.Exports[i]);
                    FaceFXAnimSetComboBox.ItemsSource = animSets;
                    FaceFXAnimSetComboBox.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    pcc?.Release(wpfWindow: this);
                    pcc = null;
                    MessageBox.Show("Error:\n" + ex.Message);
                }
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

            graph.SelectedCurve = new Curve(a.Name, a.points);
            graph.Paint(true);
        }

        private void linesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveChanges();

            animationListBox.ItemsSource = null;
            lineText.Text = null;

            if (e.AddedItems.Count != 1)
            {
                return;
            }
            selectedLine = (ME3FaceFXLine)e.AddedItems[0];
            updateAnimListBox();
            int tlkID = 0;
            if (int.TryParse(selectedLine.ID, out tlkID))
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
        private void linesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(null);
        }

        private void linesListBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !dragStart.Equals(new Point(0, 0)))
            {
                System.Windows.Vector diff = dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    try
                    {
                        dragStart = new Point(0, 0);
                        if (!(e.OriginalSource is ScrollViewer) && linesListBox.SelectedItem != null)
                        {
                            SaveChanges();
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
            SaveChanges();
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
                            SaveChanges();
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
            SaveChanges();
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
            SaveChanges();
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
    }
}
