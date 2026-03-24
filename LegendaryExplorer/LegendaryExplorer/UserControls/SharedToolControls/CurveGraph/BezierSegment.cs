using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using LegendaryExplorer.Misc;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    internal class BezierSegment : Shape
    {
        private readonly CurveGraph graph;

        public BezierSegment()
        {
        }

        public BezierSegment(CurveGraph g)
        {
            graph = g;
        }

        public double X1
        {
            get => (double)GetValue(X1Property);
            set => SetValue(X1Property, value);
        }
        
        public static readonly DependencyProperty X1Property =
            DependencyProperty.Register(nameof(X1), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Y1
        {
            get => (double)GetValue(Y1Property);
            set => SetValue(Y1Property, value);
        }
        
        public static readonly DependencyProperty Y1Property =
            DependencyProperty.Register(nameof(Y1), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double X2
        {
            get => (double)GetValue(X2Property);
            set => SetValue(X2Property, value);
        }
        
        public static readonly DependencyProperty X2Property =
            DependencyProperty.Register(nameof(X2), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Y2
        {
            get => (double)GetValue(Y2Property);
            set => SetValue(Y2Property, value);
        }
        
        public static readonly DependencyProperty Y2Property =
            DependencyProperty.Register(nameof(Y2), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Slope1
        {
            get => (double)GetValue(Slope1Property);
            set => SetValue(Slope1Property, value);
        }
        
        public static readonly DependencyProperty Slope1Property =
            DependencyProperty.Register(nameof(Slope1), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Slope2
        {
            get => (double)GetValue(Slope2Property);
            set => SetValue(Slope2Property, value);
        }
        
        public static readonly DependencyProperty Slope2Property =
            DependencyProperty.Register(nameof(Slope2), typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));
        
        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bez = d as BezierSegment;
            if (bez?.graph != null)
            {
                bez.InvalidateVisual();
            }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                if (graph != null)
                {
                    var geom = new StreamGeometry();
                    using StreamGeometryContext ctxt = geom.Open();
                    ctxt.BeginFigure(new Point(X1, graph.ActualHeight - Y1), false, false);
                    BezierTo(graph, ctxt, X1, Y1, Slope1, X2, Y2, Slope2);
                    return geom;
                }
                return Geometry.Empty;
            }
        }

        public static void BezierTo(CurveGraph graph, StreamGeometryContext ctxt, double X1, double Y1, double Slope1, double X2, double Y2, double Slope2)
        {
            double x2 = graph.toUnrealX(X2);
            double x1 = graph.toUnrealX(X1);
            double y2 = graph.toUnrealY(Y2);
            double y1 = graph.toUnrealY(Y1);

            double dx = x2 - x1;
            double h1x = x1 + dx / 3.0f;
            double h1y = y1 + (dx * Slope1) / 3.0f;
            h1x = graph.toLocalX(h1x);
            h1y = graph.toLocalY(h1y);
            double h2x = x2 - dx / 3.0f;
            double h2y = y2 - (dx * Slope2) / 3.0f;
            h2x = graph.toLocalX(h2x);
            h2y = graph.toLocalY(h2y);

            ctxt.BezierTo(new Point(h1x, graph.ActualHeight - h1y), new Point(h2x, graph.ActualHeight - h2y), new Point(X2, graph.ActualHeight - Y2), true, true);
        }
    }
}
