using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer.CurveEd
{
    /// <summary>
    /// Interaction logic for CurveGraph.xaml
    /// </summary>
    public partial class CurveGraph : UserControl
    {
        private const int LINE_SPACING = 50;

        private bool dragging = false;
        private Point dragPos;

        public event RoutedPropertyChangedEventHandler<CurvePoint> SelectedPointChanged;

        public Curve SelectedCurve
        {
            get { return (Curve)GetValue(SelectedCurveProperty); }
            set { SetValue(SelectedCurveProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedCurve.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedCurveProperty =
            DependencyProperty.Register("SelectedCurve", typeof(Curve), typeof(CurveGraph), new PropertyMetadata(new Curve(), OnSelectedCurveChanged));

        private static void OnSelectedCurveChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            CurveGraph c = sender as CurveGraph;
            if (c != null)
            {
                c.SelectedPoint = c.SelectedCurve.CurvePoints.FirstOrDefault();
            }
        }

        public CurvePoint SelectedPoint
        {
            get { return (CurvePoint)GetValue(SelectedPointProperty); }
            set { SetValue(SelectedPointProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedPoint.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedPointProperty =
            DependencyProperty.Register("SelectedPoint", typeof(CurvePoint), typeof(CurveGraph), new PropertyMetadata(new CurvePoint(0, 0, 0, 0, CurveMode.CIM_Linear), OnSelectedPointChanged));

        private static void OnSelectedPointChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            CurveGraph c = sender as CurveGraph;
            if (c != null)
            {
                foreach (var o in c.graph.Children)
                {
                    Anchor a;
                    if (o is Anchor)
                    {
                        a = o as Anchor;
                        if (a.point.Value != e.NewValue as CurvePoint)
                        {
                            a.IsSelected = false;
                        }
                    }
                }
                c.SelectedPointChanged?.Invoke(c, new RoutedPropertyChangedEventArgs<CurvePoint>(e.OldValue as CurvePoint, e.NewValue as CurvePoint));
            }
        }
        public double VerticalScale
        {
            get { return (double)GetValue(VerticalScaleProperty); }
            set { SetValue(VerticalScaleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for VerticalScale.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerticalScaleProperty =
            DependencyProperty.Register("VerticalScale", typeof(double), typeof(CurveGraph), new PropertyMetadata(50.0, OnVerticalScaleChanged));

        public double HorizontalScale
        {
            get { return (double)GetValue(HorizontalScaleProperty); }
            set { SetValue(HorizontalScaleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HorizontalScale.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HorizontalScaleProperty =
            DependencyProperty.Register("HorizontalScale", typeof(double), typeof(CurveGraph), new PropertyMetadata(50.0, OnHorizontalScaleChanged));

        public double VerticalOffset
        {
            get { return (double)GetValue(VerticalOffsetProperty); }
            set { SetValue(VerticalOffsetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for VerticalOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register("VerticalOffset", typeof(double), typeof(CurveGraph), new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public double HorizontalOffset
        {
            get { return (double)GetValue(HorizontalOffsetProperty); }
            set { SetValue(HorizontalOffsetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for HorizontalOffset.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register("HorizontalOffset", typeof(double), typeof(CurveGraph), new PropertyMetadata(0.0, OnHorizontalOffsetChanged));

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

        public double localX(double x)
        {
            return HorizontalScale * (x - HorizontalOffset);
        }

        public double localY(double y)
        {
            return VerticalScale * (y - VerticalOffset);
        }

        public double unrealX(double x)
        {
            return x / HorizontalScale + HorizontalOffset;
        }

        public double unrealY(double y)
        {
            return y / VerticalScale + VerticalOffset;
        }

        public void Paint(bool recomputeView = false)
        {
            graph.Children.Clear();

            LinkedList<CurvePoint> points = SelectedCurve.CurvePoints;
            if (points.Count > 0 && recomputeView)
            {
                float timeSpan = points.Last().InVal - points.First().InVal;
                timeSpan = timeSpan > 0 ? timeSpan : 2;
                HorizontalOffset = Math.Round(points.First().InVal - (timeSpan * 0.2));
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

                float max = points.Max(x => x.OutVal);
                float min = points.Min(x => x.OutVal);
                float valSpan = max - min;
                valSpan = valSpan > 0 ? valSpan : 2;
                VerticalOffset = Math.Round(min - Math.Ceiling(valSpan * 0.1));
                double vSpan = Math.Ceiling(valSpan * 1.2);
                if (vSpan + VerticalOffset <= max)
                {
                    vSpan += 1;
                }
                VerticalScale = graph.ActualHeight / vSpan;
            }

            int numXLines = Convert.ToInt32(Math.Ceiling(ActualWidth / LINE_SPACING));
            int numYLines = Convert.ToInt32(Math.Ceiling(ActualHeight / LINE_SPACING));
            double upperXBound = unrealX(ActualWidth);
            double upperYBound = unrealY(ActualHeight);
            double lineXSpacing = (upperXBound - HorizontalOffset) / numXLines;
            int xGranularity = lineXSpacing > 0.75 ? 1 : (lineXSpacing > 0.25 ? 2 : 10);
            lineXSpacing = Math.Ceiling(lineXSpacing * xGranularity) / xGranularity;
            double lineYSpacing = (upperYBound - VerticalOffset) / numYLines;
            int yGranularity = lineYSpacing > 0.75 ? 1 : (lineYSpacing > 0.25 ? 2 : 10);
            lineYSpacing = Math.Ceiling(lineYSpacing * yGranularity) / yGranularity;

            Line line;
            Label label;
            double linepos;
            for (int i = 0; i < numXLines; i++)
            {
                linepos = HorizontalOffset + (lineXSpacing * (i + 1));
                line = new Line();
                Canvas.SetLeft(line, localX(linepos));
                line.Style = FindResource("VerticalLine") as Style;
                graph.Children.Add(line);

                label = new Label();
                Canvas.SetLeft(label, localX(linepos));
                Canvas.SetBottom(label, 0);
                label.Content = linepos.ToString("0.00");
                graph.Children.Add(label);
            }

            for (int i = 0; i < numYLines; i++)
            {
                linepos = VerticalOffset + (lineYSpacing * (i + 1));
                line = new Line();
                Canvas.SetBottom(line, localY(linepos));
                line.Style = FindResource("HorizontalLine") as Style;
                graph.Children.Add(line);

                label = new Label();
                Canvas.SetBottom(label, localY(linepos));
                label.Content = linepos.ToString("0.00");
                graph.Children.Add(label);
            }

            Anchor lastAnchor = null;
            for (LinkedListNode<CurvePoint> node = points.First; node != null; node = node.Next)
            {
                switch (node.Value.InterpMode)
                {
                    case CurveMode.CIM_CurveAuto:
                    case CurveMode.CIM_CurveUser:
                        node.Value.LeaveTangent = node.Value.ArriveTangent;
                        break;
                    case CurveMode.CIM_CurveAutoClamped:
                        node.Value.ArriveTangent = node.Value.LeaveTangent = 0f;
                        break;
                    case CurveMode.CIM_CurveBreak:
                    case CurveMode.CIM_Constant:
                    case CurveMode.CIM_Linear:
                    default:
                        break;
                }

                Anchor a = new Anchor(this, node);
                if (node.Value == SelectedPoint)
                {
                    a.IsSelected = true;
                }
                graph.Children.Add(a);

                if (node.Previous == null)
                {
                    line = new Line();
                    line.X1 = -10;
                    line.bind(Line.Y1Property, a, "Y", new YConverter(), ActualHeight);
                    line.bind(Line.X2Property, a, "X");
                    line.bind(Line.Y2Property, a, "Y", new YConverter(), ActualHeight);
                    graph.Children.Add(line);
                }
                else
                {
                    PathBetween(lastAnchor, a, node.Previous.Value.InterpMode);
                }

                if (node.Next == null)
                {
                    line = new Line();
                    line.bind(Line.X1Property, a, "X");
                    line.bind(Line.Y1Property, a, "Y", new YConverter(), ActualHeight);
                    line.X2 = ActualWidth + 10;
                    line.bind(Line.Y2Property, a, "Y", new YConverter(), ActualHeight);
                    graph.Children.Add(line);
                }
                lastAnchor = a;
            }
        }

        private void PathBetween(Anchor a1, Anchor a2, CurveMode interpMode = CurveMode.CIM_Linear)
        {
            Line line;
            BezierSegment bez;
            switch (interpMode)
            {
                case CurveMode.CIM_Linear:
                    line = new Line();
                    line.bind(Line.X1Property, a1, "X");
                    line.bind(Line.Y1Property, a1, "Y", new YConverter(), ActualHeight);
                    line.bind(Line.X2Property, a2, "X");
                    line.bind(Line.Y2Property, a2, "Y", new YConverter(), ActualHeight);
                    graph.Children.Add(line);
                    break;
                case CurveMode.CIM_Constant:
                    line = new Line();
                    line.bind(Line.X1Property, a1, "X");
                    line.bind(Line.Y1Property, a1, "Y", new YConverter(), ActualHeight);
                    line.bind(Line.X2Property, a2, "X");
                    line.bind(Line.Y2Property, a1, "Y", new YConverter(), ActualHeight);
                    graph.Children.Add(line);
                    line = new Line();
                    line.bind(Line.X1Property, a2, "X");
                    line.bind(Line.Y1Property, a1, "Y", new YConverter(), ActualHeight);
                    line.bind(Line.X2Property, a2, "X");
                    line.bind(Line.Y2Property, a2, "Y", new YConverter(), ActualHeight);
                    graph.Children.Add(line);
                    break;
                case CurveMode.CIM_CurveAuto:
                case CurveMode.CIM_CurveUser:
                case CurveMode.CIM_CurveBreak:
                case CurveMode.CIM_CurveAutoClamped:
                    bez = new BezierSegment(this);
                    bez.Slope1 = a1.point.Value.LeaveTangent;
                    bez.Slope2 = a2.point.Value.ArriveTangent;
                    bez.bind(BezierSegment.X1Property, a1, "X");
                    bez.bind(BezierSegment.Y1Property, a1, "Y");
                    bez.bind(BezierSegment.X2Property, a2, "X");
                    bez.bind(BezierSegment.Y2Property, a2, "Y");
                    graph.Children.Add(bez);
                    a1.rightBez = bez;
                    a2.leftBez = bez;
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
            VerticalScale *= 1 + ((double)e.Delta / 4000);
            //VerticalOffset += (graph.ActualHeight / VerticalScale) * 0.1 * Math.Sign(e.Delta);
            Paint();
        }

        private void graph_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == graph && e.RightButton == MouseButtonState.Released)
            {
                dragging = true;
                dragPos = e.GetPosition(graph);
                Cursor = Cursors.ScrollNS;
            }
            else if (e.OriginalSource == graph && e.ChangedButton == MouseButton.Right)
            {
                ContextMenu cm = new ContextMenu();
                MenuItem addKey = new MenuItem();
                addKey.Header = "Add Key";
                addKey.Click += AddKey_Click;
                addKey.Tag = e.GetPosition(graph);
                cm.Items.Add(addKey);
                cm.PlacementTarget = sender as Canvas;
                cm.IsOpen = true;
            }
        }

        private void AddKey_Click(object sender, RoutedEventArgs e)
        {
            Point pos = (Point)(sender as MenuItem).Tag;
            double inVal = unrealX(pos.X);
            LinkedListNode<CurvePoint> node;
            try
            {
                node = SelectedCurve.CurvePoints.Find(SelectedCurve.CurvePoints.First(x => x.InVal > inVal));
                SelectedCurve.AddPoint(new CurvePoint((float)inVal, (float)unrealY(ActualHeight - pos.Y), 0, 0, node.Value.InterpMode), node);
            }
            catch (Exception)
            {
                node = SelectedCurve.CurvePoints.Last;
                SelectedCurve.AddPoint(new CurvePoint((float)inVal, (float)unrealY(ActualHeight - pos.Y), 0, 0, node.Value.InterpMode), node, false);
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
                double yDiff = newPos.Y - dragPos.Y;
                VerticalOffset += yDiff / VerticalScale;
                Paint();
                dragPos = newPos;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            Anchor a = ((sender as MenuItem).Parent as ContextMenu).Tag as Anchor;
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox b = sender as TextBox;
            string result;
            if (b.IsSelectionActive)
            {
                result = b.Text.Remove(b.SelectionStart, b.SelectionLength).Insert(b.SelectionStart, e.Text);
            }
            else
            {
                result = b.Text.Insert(b.CaretIndex, e.Text);
            }
            float f = 0;
            if (!float.TryParse(result, out f))
            {
                e.Handled = true;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox b = sender as TextBox;
            double d = 0;
            //SirCxyrtyx: doing a stack trace to resolve a circular calling situation is horrible, I know. I'm so sorry about this.
            if (double.TryParse(b.Text, out d) && b.IsFocused && b.IsKeyboardFocused && !KFreonLib.Misc.Methods.FindInStack("Anchor"))
            {
                Anchor a = graph.Children.OfType<Anchor>().FirstOrDefault(x => x.IsSelected);
                if (a != null && b.Name == "xTextBox")
                {
                    float next = a.point.Next?.Value.InVal ?? float.MaxValue;
                    float prev = a.point.Previous?.Value.InVal ?? float.MinValue;
                    if (d > prev && d < next)
                    {
                        a.point.Value.InVal = (float)d;
                        a.X = localX(d);
                        Paint(true);
                    }
                }
                else if (a != null && b.Name == "yTextBox")
                {
                    a.Y = localY(d);
                    Paint(true);
                }
            }
        }
    }

    [ValueConversion(typeof(double), typeof(double))]
    public class YConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)parameter - (double)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (double)parameter - (double)value;
        }
    }
}
