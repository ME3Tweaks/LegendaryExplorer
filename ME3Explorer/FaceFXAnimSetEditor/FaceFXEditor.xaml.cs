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
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;

namespace ME3Explorer.FaceFXAnimSetEditor
{
    /// <summary>
    /// Interaction logic for FaceFXEditor.xaml
    /// </summary>
    public partial class FaceFXEditor : Window
    {
        struct Animation
        {
            public string Name { get; set; }
            public LinkedList<CurvePoint> points;
        }

        IMEPackage pcc;
        public List<IExportEntry> animSets;
        public FaceFXAnimSet FaceFX;
        public FaceFXAnimSet.FaceFXLine selectedLine;
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
                    pcc = new ME3Package(d.FileName);
                    animSets = new List<IExportEntry>();
                    for (int i = 0; i < pcc.Exports.Count; i++)
                        if (pcc.Exports[i].ClassName == "FaceFXAnimSet")
                            animSets.Add(pcc.Exports[i]);
                    FaceFXAnimSetComboBox.ItemsSource = animSets;
                    FaceFXAnimSetComboBox.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
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
            FaceFX = new FaceFXAnimSet(pcc, FaceFXAnimSetComboBox.SelectedItem as IExportEntry);
            linesListBox.ItemsSource = FaceFX.Data.Data;
            treeView.Nodes.Clear();
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
            selectedLine = (FaceFXAnimSet.FaceFXLine)e.AddedItems[0];
            updateAnimListBox();
            int tlkID = 0;
            if (int.TryParse(selectedLine.ID, out tlkID))
            {
                lineText.Text = TalkFiles.findDataById(tlkID);
            }
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(FaceFX.DataToTree2(selectedLine));
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
            graph.SelectedCurve = new Curve();
            graph.Paint(true);
        }

        private void SaveChanges()
        {
            if (selectedLine != null && animationListBox.ItemsSource != null)
            {
                List<CurvePoint> curvePoints = new List<CurvePoint>();
                List<FaceFXAnimSet.NameRef> animations = new List<FaceFXAnimSet.NameRef>();
                List<int> numKeys = new List<int>();
                foreach (Animation anim in animationListBox.ItemsSource)
                {
                    animations.Add(new FaceFXAnimSet.NameRef { index = FaceFX.Header.Names.IndexOf(anim.Name), unk2 = 0 });
                    curvePoints.AddRange(anim.points);
                    numKeys.Add(anim.points.Count);
                }
                selectedLine.animations = animations.ToArray();
                selectedLine.points = curvePoints.Select(x => new FaceFXAnimSet.ControlPoint
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
                            FaceFXAnimSet.FaceFXLine d = (linesListBox.SelectedItem as FaceFXAnimSet.FaceFXLine).Clone();
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
                FaceFXAnimSet.FaceFXLine line = d.line;
                line.Name = names.FindOrAdd(sourceNames[line.Name]);
                line.animations = line.animations.Select(x => new FaceFXAnimSet.NameRef
                {
                    index = names.FindOrAdd(sourceNames[x.index]),
                    unk2 = x.unk2
                }).ToArray();
                FaceFX.Header.Names = names.ToArray();
                FaceFX.Data.Data = FaceFX.Data.Data.Concat(line).ToArray();
                linesListBox.ItemsSource = FaceFX.Data.Data;
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
                selectedLine.animations = selectedLine.animations.Concat(new FaceFXAnimSet.NameRef { index = names.FindOrAdd(a.Name), unk2 = 0 }).ToArray();
                FaceFX.Header.Names = names.ToArray();
                selectedLine.points = selectedLine.points.Concat(a.points.Select(x => new FaceFXAnimSet.ControlPoint
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
            FaceFXAnimSet.FaceFXLine line = (FaceFXAnimSet.FaceFXLine)linesListBox.SelectedItem;
            List<FaceFXAnimSet.FaceFXLine> lines = FaceFX.Data.Data.ToList();
            lines.Remove(line);
            FaceFX.Data.Data = lines.ToArray();
            linesListBox.ItemsSource = FaceFX.Data.Data;
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
                    result = Microsoft.VisualBasic.Interaction.InputBox("Please enter new value", "ME3Explorer", selectedLine.unk3.ToString(), 0, 0);
                    i = -1;
                    if (int.TryParse(result, out i) && i >= 0 && i < FaceFX.Header.Names.Length)
                        selectedLine.unk3 = i;
                    break;
                default:
                    return;
            }
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(FaceFX.DataToTree2(selectedLine));
        }
    }
}
