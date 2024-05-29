using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Converters;
using LegendaryExplorer.UserControls.SharedToolControls.Curves;
using LegendaryExplorerCore.Unreal;
using BezierSegment = LegendaryExplorer.UserControls.SharedToolControls.Curves.BezierSegment;

namespace LegendaryExplorer.UserControls.SharedToolControls
{
    public class ExtraCurveGraphLine
    {
        public double Position { get; set; }
        public string Label { get; set; }
        public SolidColorBrush Color { get; set; }
        public double LabelOffset { get; set; }
    }

    /// <summary>
    /// Interaction logic for CurveGraph.xaml
    /// </summary>
    public partial class CurveGraph : NotifyPropertyChangedControlBase, IDisposable
    {
        private const int LINE_SPACING = 50;

        private bool dragging;
        private bool scrolling;
        private Point dragPos;

        public event RoutedPropertyChangedEventHandler<CurvePoint> SelectedPointChanged;
        public static bool TrackLoading;
        public Curve SelectedCurve
        {
            get => (Curve)GetValue(SelectedCurveProperty);
            set => SetValue(SelectedCurveProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedCurve.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedCurveProperty =
            DependencyProperty.Register(nameof(SelectedCurve), typeof(Curve), typeof(CurveGraph), new PropertyMetadata(new Curve(), OnSelectedCurveChanged));

        private static void OnSelectedCurveChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is CurveGraph c)
            {
                TrackLoading = true;
                c.SelectedPoint = c.SelectedCurve.CurvePoints.FirstOrDefault();
                TrackLoading = false;
            }
        }

        public Curve ComparisonCurve
        {
            get => (Curve)GetValue(ComparisonCurveProperty);
            set => SetValue(ComparisonCurveProperty, value);
        }

        // Using a DependencyProperty as the backing store for ComparisonCurve.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ComparisonCurveProperty =
            DependencyProperty.Register(nameof(ComparisonCurve), typeof(Curve), typeof(CurveGraph), new PropertyMetadata());

        public CurvePoint SelectedPoint
        {
            get => (CurvePoint)GetValue(SelectedPointProperty);
            set => SetValue(SelectedPointProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedPoint.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedPointProperty =
            DependencyProperty.Register(nameof(SelectedPoint), typeof(CurvePoint), typeof(CurveGraph), new PropertyMetadata(new CurvePoint(0, 0, 0, 0, EInterpCurveMode.CIM_Linear), OnSelectedPointChanged));

        private static void OnSelectedPointChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is CurveGraph c)
            {
                foreach (Anchor a in c.Anchors)
                {
                    if (a.Point.Value != e.NewValue as CurvePoint)
                    {
                        a.IsSelected = false;
                    }
                }
                c.SelectedPointChanged?.Invoke(c, new RoutedPropertyChangedEventArgs<CurvePoint>(e.OldValue as CurvePoint, e.NewValue as CurvePoint));
            }
        }
        public double VerticalScale
        {
            get => (double)GetValue(VerticalScaleProperty);
            set => SetValue(VerticalScaleProperty, value);
        }

        public List<ExtraCurveGraphLine> ExtraXLines { get; } = new();
        public List<ExtraCurveGraphLine> ExtraYLines { get; } = new();

        // Using a DependencyProperty as the backing store for VerticalScale.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerticalScaleProperty =
            DependencyProperty.Register(nameof(VerticalScale), typeof(double), typeof(CurveGraph), new PropertyMetadata(50.0, OnVerticalScaleChanged));

        public double HorizontalScale
        {
            get => (double)GetValue(HorizontalScaleProperty);
            set => SetValue(HorizontalScaleProperty, value);
        }

        // Using a DependencyProperty as the backing store for HorizontalScale.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HorizontalScaleProperty =
            DependencyProperty.Register(nameof(HorizontalScale), typeof(double), typeof(CurveGraph), new PropertyMetadata(50.0, OnHorizontalScaleChanged));

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }

        // Using a DependencyProperty as the backing store for VerticalOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(nameof(VerticalOffset), typeof(double), typeof(CurveGraph), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public double HorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        // Using a DependencyProperty as the backing store for HorizontalOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register(nameof(HorizontalOffset), typeof(double), typeof(CurveGraph), new PropertyMetadata(0.0, OnHorizontalOffsetChanged));

        public CurveGraph()
        {
            InitializeComponent();
        }

        private static void OnVerticalScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        private static void OnVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private static void OnHorizontalScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        public double toLocalX(double x)
        {
            return HorizontalScale * (x - HorizontalOffset);
        }

        public double toLocalY(double y)
        {
            return VerticalScale * (y - VerticalOffset);
        }

        public double toUnrealX(double x)
        {
            return x / HorizontalScale + HorizontalOffset;
        }

        public double toUnrealY(double y)
        {
            return y / VerticalScale + VerticalOffset;
        }

        public void Paint(bool recomputeView = false)
        {
            TrackLoading = true;
            graph.Children.Clear();
            Anchors.Clear();

            LinkedList<CurvePoint> points = SelectedCurve.CurvePoints;

            // Set size and scale of graph
            if (points.Count > 0 && recomputeView)
            {
                if (UseFixedTimeSpan)
                {
                    UpdateScalingFromFixedTimeSpan();
                }
                else
                {
                    float timeSpan = points.Last().InVal - points.First().InVal;
                    timeSpan = timeSpan > 0 ? timeSpan : 2;
                    HorizontalOffset = points.First().InVal - (timeSpan * 0.2);
                    double hSpan = Math.Ceiling(timeSpan * 1.2);
                    if (hSpan + HorizontalOffset <= timeSpan)
                    {
                        hSpan += 1;
                    }
                    HorizontalScale = graph.ActualWidth / hSpan;
                    if (HorizontalOffset >= points.First().InVal - (hSpan / 10))
                    {
                        HorizontalOffset = points.First().InVal - (hSpan / 10);
                    }
                    else if (HorizontalOffset + hSpan <= points.Last().InVal + (hSpan / 10))
                    {
                        HorizontalOffset += hSpan / 10;
                    }
                }

                float max = points.Max(x => x.OutVal);
                float min = points.Min(x => x.OutVal);
                float valSpan = max - min;
                valSpan = valSpan > 0 ? valSpan : 2;
                VerticalOffset = Math.Round((min - Math.Ceiling(valSpan * 0.1)) * 10) / 10;
                double vSpan = Math.Ceiling(valSpan * 1.2);
                if (vSpan + VerticalOffset <= max)
                {
                    vSpan += 1;
                }
                VerticalScale = graph.ActualHeight / vSpan;
            }

            // Render grid

            int numXLines = Convert.ToInt32(Math.Ceiling(ActualWidth / LINE_SPACING));
            int numYLines = Convert.ToInt32(Math.Ceiling(ActualHeight / LINE_SPACING));
            double upperXBound = toUnrealX(ActualWidth);
            double upperYBound = toUnrealY(ActualHeight);
            double lineXSpacing = (upperXBound - HorizontalOffset) / numXLines;
            int xGranularity = lineXSpacing > 0.75 ? 1 : (lineXSpacing > 0.25 ? 2 : 10);
            lineXSpacing = Math.Ceiling(lineXSpacing * xGranularity) / xGranularity;
            double lineYSpacing = (upperYBound - VerticalOffset) / numYLines;
            int yGranularity = lineYSpacing > 0.75 ? 1 : (lineYSpacing > 0.25 ? 2 : 10);
            lineYSpacing = Math.Ceiling(lineYSpacing * yGranularity) / yGranularity;

            double firstHorizontalLine = HorizontalOffset - (HorizontalOffset % lineXSpacing);
            for (int i = 0; i < numXLines; i++)
            {
                RenderXGridLine(firstHorizontalLine + (lineXSpacing * i));
            }

            double firstVerticalLine = VerticalOffset - (VerticalOffset % lineYSpacing);
            for (int i = 0; i < numYLines; i++)
            {
                RenderYGridLine(firstVerticalLine + (lineYSpacing * i));
            }

            // Extra lines
            foreach (ExtraCurveGraphLine extraLine in ExtraXLines)
            {
                RenderXGridLine(extraLine.Position, extraLine.Label, extraLine.LabelOffset, extraLine.Color);
            }

            if (ShowReferenceCurve && ComparisonCurve != null && ComparisonCurve.CurvePoints.Count > 0)
            {
                graph.Children.Add(new StaticCurve(this, ComparisonCurve.CurvePoints, true, true)
                {
                    Style = FindResource("CompareCurve") as Style
                });
            }

            RenderCurve(points);

            TrackLoading = false;
        }

        private void RenderXGridLine(double position, string content = null, double yoffset = 0, SolidColorBrush color = null)
        {
            var line = new Line();
            Canvas.SetLeft(line, toLocalX(position));
            line.Style = FindResource("VerticalLine") as Style;
            if (color != null) line.Stroke = color;
            else if (position == 0.0) line.Stroke = FindResource("ZeroGridLineStroke") as SolidColorBrush;
            graph.Children.Add(line);

            var label = new Label();
            Canvas.SetLeft(label, toLocalX(position));
            Canvas.SetBottom(label, yoffset);
            label.Content = content ?? position.ToString("0.00");
            graph.Children.Add(label);
        }

        private void RenderYGridLine(double position)
        {
            var line = new Line();
            Canvas.SetBottom(line, toLocalY(position));
            line.Style = FindResource("HorizontalLine") as Style;
            if (position == 0.0) line.Stroke = FindResource("ZeroGridLineStroke") as SolidColorBrush;
            graph.Children.Add(line);

            var label = new Label();
            Canvas.SetBottom(label, toLocalY(position));
            label.Content = position.ToString("0.00");
            graph.Children.Add(label);
        }

        internal readonly List<Anchor> Anchors = new();
        private void RenderCurve(LinkedList<CurvePoint> points)
        {
            Anchor lastAnchor = null;

            for (LinkedListNode<CurvePoint> node = points.First; node != null; node = node.Next)
            {
                switch (node.Value.InterpMode)
                {
                    case EInterpCurveMode.CIM_CurveAuto:
                    case EInterpCurveMode.CIM_CurveUser:
                    case EInterpCurveMode.CIM_CurveAutoClamped:
                        node.Value.LeaveTangent = node.Value.ArriveTangent;
                        break;
                    case EInterpCurveMode.CIM_CurveBreak:
                    case EInterpCurveMode.CIM_Constant:
                    case EInterpCurveMode.CIM_Linear:
                    default:
                        break;
                }

                var a = new Anchor(this, node);
                if (node.Value == SelectedPoint)
                {
                    a.IsSelected = true;
                }

                a.AnchorIndex = Anchors.Count;
                Anchors.Add(a);
                graph.Children.Add(a);

                Line line;
                if (node.Previous == null)
                {
                    line = new Line { X1 = -10 };
                    line.bind(Line.Y1Property, a, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    line.bind(Line.X2Property, a, nameof(Anchor.X));
                    line.bind(Line.Y2Property, a, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    graph.Children.Add(line);
                }
                else
                {
                    PathBetween(lastAnchor, a, node.Previous.Value.InterpMode);
                }

                if (node.Next == null)
                {
                    line = new Line();
                    line.bind(Line.X1Property, a, nameof(Anchor.X));
                    line.bind(Line.Y1Property, a, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    line.X2 = ActualWidth + 10;
                    line.bind(Line.Y2Property, a, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    graph.Children.Add(line);
                }
                lastAnchor = a;
            }
        }

        private void PathBetween(Anchor a1, Anchor a2, EInterpCurveMode interpMode = EInterpCurveMode.CIM_Linear)
        {
            Line line;
            switch (interpMode)
            {
                case EInterpCurveMode.CIM_Linear:
                    line = new Line();
                    line.bind(Line.X1Property, a1, nameof(Anchor.X));
                    line.bind(Line.Y1Property, a1, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    line.bind(Line.X2Property, a2, nameof(Anchor.X));
                    line.bind(Line.Y2Property, a2, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    graph.Children.Add(line);
                    break;
                case EInterpCurveMode.CIM_Constant:
                    line = new Line();
                    line.bind(Line.X1Property, a1, nameof(Anchor.X));
                    line.bind(Line.Y1Property, a1, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    line.bind(Line.X2Property, a2, nameof(Anchor.X));
                    line.bind(Line.Y2Property, a1, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    graph.Children.Add(line);
                    line = new Line();
                    line.bind(Line.X1Property, a2, nameof(Anchor.X));
                    line.bind(Line.Y1Property, a1, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    line.bind(Line.X2Property, a2, nameof(Anchor.X));
                    line.bind(Line.Y2Property, a2, nameof(Anchor.Y), CurveEdSubtractionConverter.Instance, ActualHeight);
                    graph.Children.Add(line);
                    break;
                case EInterpCurveMode.CIM_CurveAuto:
                case EInterpCurveMode.CIM_CurveUser:
                case EInterpCurveMode.CIM_CurveBreak:
                case EInterpCurveMode.CIM_CurveAutoClamped:
                    var bez = new BezierSegment(this)
                    {
                        Slope1 = a1.Point.Value.LeaveTangent,
                        Slope2 = a2.Point.Value.ArriveTangent
                    };
                    bez.bind(BezierSegment.X1Property, a1, nameof(Anchor.X));
                    bez.bind(BezierSegment.Y1Property, a1, nameof(Anchor.Y));
                    bez.bind(BezierSegment.X2Property, a2, nameof(Anchor.X));
                    bez.bind(BezierSegment.Y2Property, a2, nameof(Anchor.Y));
                    graph.Children.Add(bez);
                    a1.RightBez = bez;
                    a2.LeftBez = bez;
                    break;
                default:
                    break;
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Paint(true);
        }

        public void invokeSelectedPointChanged()
        {
            SelectedPointChanged?.Invoke(this, new RoutedPropertyChangedEventArgs<CurvePoint>(null, SelectedPoint));
        }

        private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            scrolling = true;
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                if (UseFixedTimeSpan)
                {
                    FixedStartTime *= 1 + ((float)e.Delta / 8000);
                    FixedEndTime *= 1 + ((float)e.Delta / 8000);
                    UpdateScalingFromFixedTimeSpan();
                }
                else
                {
                    HorizontalScale *= 1 + ((double)e.Delta / 4000);
                }
            }
            else
            {
                VerticalScale *= 1 + ((double)e.Delta / 4000);
            }
            //VerticalOffset += (graph.ActualHeight / VerticalScale) * 0.1 * Math.Sign(e.Delta);
            Paint();
            scrolling = false;
        }

        private void graph_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ReferenceEquals(e.OriginalSource, graph) && e.RightButton == MouseButtonState.Released)
            {
                dragging = true;
                dragPos = e.GetPosition(graph);
                Cursor = Keyboard.Modifiers == ModifierKeys.Shift ? Cursors.ScrollWE : Cursors.ScrollNS;
            }
            else if (ReferenceEquals(e.OriginalSource, graph) && e.ChangedButton == MouseButton.Right)
            {
                var cm = new ContextMenu();

                var addKey = new MenuItem
                {
                    Header = "Add Key",
                    Tag = e.GetPosition(graph)
                };
                addKey.Click += AddKey_Click;
                cm.Items.Add(addKey);

                var addKeyZero = new MenuItem
                {
                    Header = "Add Key with 0 Weight",
                    Tag = e.GetPosition(graph)
                };
                addKeyZero.Click += AddKeyAtZero_Click;
                cm.Items.Add(addKeyZero);

                var offsetKeys = new MenuItem
                {
                    Header = "Offset All Keys After This Point",
                    Tag = e.GetPosition(graph)
                };
                offsetKeys.Click += OffsetKeys_Click;
                cm.Items.Add(offsetKeys);

                cm.PlacementTarget = sender as Canvas;
                cm.IsOpen = true;
            }
        }

        private void OffsetKeys_Click(object sender, RoutedEventArgs e)
        {
            Point pos = (Point)((MenuItem)sender).Tag;
            double inVal = toUnrealX(pos.X);
            string res = Dialogs.PromptDialog.Prompt(this, "Seconds to offset keys by", "Curve Editor", "0.0", true);
            if (float.TryParse(res, out var delta))
            {
                LinkedListNode<CurvePoint> node = SelectedCurve.CurvePoints.First;
                while (node?.Next != null && node.Value.InVal < inVal)
                {
                    node = node.Next;
                }
                if (node?.Previous?.Value.InVal - node?.Value.InVal >= delta)
                {
                    MessageBox.Show("Cannot re-order keys");
                }
                else
                {
                    while (node != null)
                    {
                        node.Value.InVal += delta;
                        node = node.Next;
                    }
                    Paint(true);
                }
            }
        }

        private void AddKey_Click(object sender, RoutedEventArgs e)
        {
            var pos = (Point)((MenuItem)sender).Tag;
            double inVal = toUnrealX(pos.X);
            AddKey((float)inVal, (float)toUnrealY(ActualHeight - pos.Y));
        }

        private void AddKeyAtZero_Click(object sender, RoutedEventArgs e)
        {
            var pos = (Point)((MenuItem)sender).Tag;
            double inVal = toUnrealX(pos.X);
            AddKey((float)inVal, 0);
        }

        public void AddKeyAtZero_MousePosition()
        {
            Point pos = Mouse.GetPosition(this);
            if (pos.X < 0 || pos.X > ActualWidth || pos.Y < 0 || pos.Y > ActualHeight) return;
            double inVal = toUnrealX(pos.X);
            AddKey((float)inVal, 0);
        }

        private void AddKey(float time, float y)
        {
            LinkedListNode<CurvePoint> node;
            CurvePoint pointBefore = SelectedCurve.CurvePoints.FirstOrDefault(x => x.InVal > time);
            if (pointBefore is not null)
            {
                node = SelectedCurve.CurvePoints.Find(pointBefore);
                SelectedCurve.AddPoint(new CurvePoint(time, y, 0, 0, pointBefore.InterpMode), node);
            }
            else
            {
                node = SelectedCurve.CurvePoints.Last;
                SelectedCurve.AddPoint(new CurvePoint(time, y, 0, 0, node?.Value.InterpMode ?? EInterpCurveMode.CIM_CurveAutoClamped), node, false);
            }

            Paint(true);
        }

        private void graph_MouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;
            Cursor = Cursors.Arrow;
        }

        private void graph_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point newPos = e.GetPosition(graph);
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    double xDiff = newPos.X - dragPos.X;
                    if (UseFixedTimeSpan)
                    {
                        FixedStartTime -= (float)(xDiff / HorizontalScale);
                        FixedEndTime -= (float)(xDiff / HorizontalScale);
                        UpdateScalingFromFixedTimeSpan();
                    }
                    else
                    {
                        HorizontalOffset -= xDiff / HorizontalScale;
                    }
                }
                else
                {
                    double yDiff = newPos.Y - dragPos.Y;
                    VerticalOffset += yDiff / VerticalScale;
                }
                Paint();
                dragPos = newPos;
            }
        }

        private void FloatTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var b = (TextBox)sender;
            string result;
            if (b.IsSelectionActive)
            {
                result = b.Text.Remove(b.SelectionStart, b.SelectionLength).Insert(b.SelectionStart, e.Text);
            }
            else
            {
                result = b.Text.Insert(b.CaretIndex, e.Text);
            }

            if (!float.TryParse(result, out _))
            {
                e.Handled = true;
            }
        }
        
        private void PointTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var b = (TextBox)sender;
            //SirCxyrtyx: doing a stack trace to resolve a circular calling situation is horrible, I know. I'm so sorry about this.
            if (double.TryParse(b.Text, out double d) && b.IsFocused && b.IsKeyboardFocused && !FindInStack(nameof(Anchor)))
            {
                Anchor a = Anchors.FirstOrDefault(x => x.IsSelected);
                if (a != null && b.Name == nameof(xTextBox))
                {
                    float next = a.Point.Next?.Value.InVal ?? float.MaxValue;
                    float prev = a.Point.Previous?.Value.InVal ?? float.MinValue;
                    if (d > prev && d < next)
                    {
                        a.X = toLocalX(d);
                        Paint(true);
                    }
                }
                else if (a != null && b.Name == nameof(yTextBox))
                {
                    a.Y = toLocalY(d);
                    Paint(true);
                }
            }
        }

        /// <summary>
        /// Find a string in the stackframes.
        /// </summary>
        /// <param name="FrameName">FrameName to find.</param>
        /// <returns>True if FrameName is found in stack.</returns>
        private static bool FindInStack(string FrameName)
        {
            var st = new StackTrace();
            for (int i = 0; i < st.FrameCount; i++)
            {
                string name = st.GetFrame(i).GetMethod().ReflectedType.FullName;
                if (name.Contains(FrameName))
                    return true;
            }
            return false;
        }

        public void DeleteSelectedKey()
        {
            LinkedListNode<CurvePoint> point = SelectedCurve.CurvePoints.Find(SelectedPoint);
            if (point != null)
            {
                if (point.Previous != null)
                {
                    SelectedPoint = point.Previous.Value;
                }
                else if (point.Next != null)
                {
                    SelectedPoint = point.Next.Value;
                }
                SelectedCurve.RemovePoint(point);
                Paint();
            }
        }

        public void Clear()
        {
            SelectedCurve = new Curve();
            ComparisonCurve = null;
            Paint(true);
            xTextBox.Clear();
            yTextBox.Clear();
        }

        private void UpdateScalingFromFixedTimeSpan()
        {
            if (UseFixedTimeSpan)
            {
                HorizontalOffset = FixedStartTime;
                float diff = Math.Abs(FixedEndTime - FixedStartTime); //No negative values!
                if (diff < 0.1f)
                {
                    diff = 0.1f; //No super tiny values!
                }
                HorizontalScale = graph.ActualWidth / diff;
            }
        }

        private bool _useFixedTimeSpan;
        public bool UseFixedTimeSpan
        {
            get => _useFixedTimeSpan;
            set
            {
                if (SetProperty(ref _useFixedTimeSpan, value))
                {
                    Paint(true);
                }
            }
        }

        private float _fixedStartTime = -0.5f;
        public float FixedStartTime
        {
            get => _fixedStartTime;
            set
            {
                if (SetProperty(ref _fixedStartTime, value) && !dragging && !scrolling)
                {
                    UpdateScalingFromFixedTimeSpan();
                    Paint();
                }
            }
        }

        private float _fixedEndTime = 10;
        public float FixedEndTime
        {
            get => _fixedEndTime;
            set
            {
                if (SetProperty(ref _fixedEndTime, value) && !dragging && !scrolling)
                {
                    UpdateScalingFromFixedTimeSpan();
                    Paint();
                }
            }
        }

        private bool _showReferenceCurve = true;
        public bool ShowReferenceCurve
        {
            get => _showReferenceCurve;
            set
            {
                if (SetProperty(ref _showReferenceCurve, value))
                {
                    Paint();
                }
            }
        }

        public void Dispose()
        {
            graph?.Children.Clear();
            Anchors.Clear();
        }
    }

    public static class CurveBrushes
    {
        public static readonly SolidColorBrush Primary;
        public static readonly SolidColorBrush Border;

        static CurveBrushes()
        {
            Primary = new SolidColorBrush(Color.FromRgb(0xFF, 0xC6, 0x03));
            Primary.Freeze();
            Border = new SolidColorBrush(Color.FromRgb(0xCC, 0x98, 0x00));
            Border.Freeze();
        }
    }
}
