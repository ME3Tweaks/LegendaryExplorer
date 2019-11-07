using System;
using System.Collections.Generic;
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
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;

namespace ME3Explorer.Matinee
{
    /// <summary>
    /// Interaction logic for Timeline.xaml
    /// </summary>
    public partial class Timeline : NotifyPropertyChangedControlBase
    {
        public IMEPackage Pcc => InterpDataExport?.FileRef;

        private ExportEntry _interpDataExport;
        public ExportEntry InterpDataExport
        {
            get => _interpDataExport;
            set
            {
                if (SetProperty(ref _interpDataExport, value))
                {
                    LoadGroups();
                }
            }
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

        public event Action<ExportEntry> SelectionChanged;

        public ObservableCollectionExtended<InterpGroup> InterpGroups { get; } = new ObservableCollectionExtended<InterpGroup>();

        public Timeline()
        {
            InitializeComponent();
            ResetView();
        }

        private void LoadGroups()
        {
            ResetView();
            InterpGroups.ClearEx();
            var groupsProp = InterpDataExport.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
            if (groupsProp != null)
            {
                var groupExports = groupsProp.Where(prop => Pcc.IsUExport(prop.Value)).Select(prop => Pcc.GetUExport(prop.Value));
                InterpGroups.AddRange(groupExports.Select(exp => new InterpGroup(exp)));
            }
        }

        public void RefreshInterpData(ExportEntry changedExport)
        {
            if (changedExport.ClassName == "InterpGroup")
            {
                if (InterpGroups.FirstOrDefault(g => g.Export == changedExport) is InterpGroup group)
                {
                    int idx = InterpGroups.IndexOf(group);
                    InterpGroups.RemoveAt(idx);
                    InterpGroups.Insert(idx, new InterpGroup(changedExport));
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
                        interpGroup.RefreshTracks();
                    }
                }
            }
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
            else if(Scale > 110)
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
    }
}
