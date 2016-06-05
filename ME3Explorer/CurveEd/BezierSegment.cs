using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ME3Explorer.CurveEd
{
    class BezierSegment : Shape
    {
        public CurveGraph graph;

        public BezierSegment()
        {

        }

        public BezierSegment(CurveGraph g)
        {
            graph = g;
        }

        public double X1
        {
            get { return (double)GetValue(X1Property); }
            set { SetValue(X1Property, value); }
        }
        
        public static readonly DependencyProperty X1Property =
            DependencyProperty.Register("X1", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Y1
        {
            get { return (double)GetValue(Y1Property); }
            set { SetValue(Y1Property, value); }
        }
        
        public static readonly DependencyProperty Y1Property =
            DependencyProperty.Register("Y1", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double X2
        {
            get { return (double)GetValue(X2Property); }
            set { SetValue(X2Property, value); }
        }
        
        public static readonly DependencyProperty X2Property =
            DependencyProperty.Register("X2", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Y2
        {
            get { return (double)GetValue(Y2Property); }
            set { SetValue(Y2Property, value); }
        }
        
        public static readonly DependencyProperty Y2Property =
            DependencyProperty.Register("Y2", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Slope1
        {
            get { return (double)GetValue(Slope1Property); }
            set { SetValue(Slope1Property, value); }
        }
        
        public static readonly DependencyProperty Slope1Property =
            DependencyProperty.Register("Slope1", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));

        public double Slope2
        {
            get { return (double)GetValue(Slope2Property); }
            set { SetValue(Slope2Property, value); }
        }
        
        public static readonly DependencyProperty Slope2Property =
            DependencyProperty.Register("Slope2", typeof(double), typeof(BezierSegment), new PropertyMetadata(0.0, OnChanged));
        
        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            BezierSegment bez = d as BezierSegment;
            if (bez?.graph != null)
            {
                bez.InvalidateVisual();
            }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                StreamGeometry geom = new StreamGeometry();
                using (StreamGeometryContext ctxt = geom.Open())
                {
                    if (graph != null)
                    {
                        double handleLength = (graph.globalX(X2) - graph.globalX(X1)) / 4;
                        double h1x = handleLength;// / Math.Sqrt(Math.Pow(Slope1, 2) + 1);
                        double h1y = Slope1 * h1x;
                        h1x = graph.HorizontalScale * h1x + X1;
                        h1y = graph.ActualHeight - (graph.VerticalScale * h1y + Y1);
                        double h2x = -handleLength;// / Math.Sqrt(Math.Pow(Slope2, 2) + 1);
                        double h2y = Slope2 * h2x;
                        h2x = graph.HorizontalScale * h2x + X2;
                        h2y = graph.ActualHeight - (graph.VerticalScale * h2y + Y2);

                        ctxt.BeginFigure(new Point(X1, graph.ActualHeight - Y1), false, false);
                        ctxt.BezierTo(new Point(h1x, h1y), new Point(h2x, h2y), new Point(X2, graph.ActualHeight - Y2), true, true);  
                    }
                }
                return geom;
            }
        }
    }
}
